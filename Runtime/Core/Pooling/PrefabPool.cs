using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace JCommonUtils.Pooling
{
    /// <summary>
    /// Unity 主线程预制体池。缓存上限不限制同时活跃的实例数；无隐藏 Update。
    /// 稳定租还与身份查询为 O(1)，Tick 为 O(N)，N 为当前活跃数；扩容和首次实例化允许分配。
    /// </summary>
    public sealed class PrefabPool : IDisposable
    {
        internal sealed class Entry
        {
            internal Entry(PrefabPool owner, GameObject instance)
            {
                Owner = owner;
                Instance = instance;
                InitialLocalScale = instance.transform.localScale;
            }
            internal readonly PrefabPool Owner;
            internal readonly GameObject Instance;
            internal readonly Vector3 InitialLocalScale;
            internal int ActiveIndex = -1;
            internal ulong Version;
            internal ulong PendingVersion;
            internal bool CallbackQueued;
            internal int RentFrame;
            internal double ReturnAt = double.PositiveInfinity;
        }

        private readonly GameObject _prefab;
        private readonly Transform _storage;
        private readonly bool _ownsStorage;
        private readonly bool _deferInactiveParentReturns;
        private readonly PrefabPoolPolicy _policy;
        private readonly ObjectPool<Entry> _cache;
        private readonly List<Entry> _active;
        private readonly SynchronizationContext _context;
        private readonly SendOrPostCallback _deferredReturn;
        private readonly int _threadId;
        private bool _disposed;

        /// <summary>
        /// storage 为空时创建本池专用失活容器，Dispose 时一并释放；外部容器必须在层级中失活。
        /// deferInactiveParentReturns 只供已有生命周期协议的适配使用，普通项目应保持 true。
        /// </summary>
        public PrefabPool(GameObject prefab, Transform storage = null, int maximumCached = 64,
            PrefabPoolPolicy policy = null, bool deferInactiveParentReturns = true)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (maximumCached < 1) throw new ArgumentOutOfRangeException(nameof(maximumCached));
            if (storage != null && storage.gameObject.activeInHierarchy)
                throw new ArgumentException("Pool storage must be inactive in the hierarchy.", nameof(storage));
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _context = SynchronizationContext.Current;
            _prefab = prefab;
            _policy = policy;
            _deferInactiveParentReturns = deferInactiveParentReturns;
            _deferredReturn = ReturnAfterHierarchyChange;
            _ownsStorage = storage == null;
            _storage = storage;
            if (_ownsStorage)
            {
                var container = new GameObject("Prefab pool: " + prefab.name);
                container.SetActive(false);
                _storage = container.transform;
            }
            int capacity = Math.Min(8, maximumCached);
            _active = new List<Entry>(capacity);
            _cache = new ObjectPool<Entry>(Create, null, null, DestroyEntry,
                collectionCheck: false, defaultCapacity: capacity, maxSize: maximumCached);
        }

        public int ActiveCount { get { RequireThread(); return _active.Count; } }
        public int CachedCount { get { RequireThread(); return _cache.CountInactive; } }

        /// <summary>世界姿态租用；有本地姿态或初始失活要求时使用 PoolSpawn 重载。</summary>
        public PoolLease Rent(Vector3 position, Quaternion rotation, Transform parent = null) =>
            Rent(new PoolSpawn(position, rotation, parent));

        /// <summary>先发布租用身份、准备状态，再激活。回调失败会撤销本次租用并销毁实例，不污染缓存。</summary>
        public PoolLease Rent(PoolSpawn spawn)
        {
            RequireThread();
            if (_disposed) throw new ObjectDisposedException(nameof(PrefabPool));
            if (_storage == null) throw new InvalidOperationException("Pool storage has been destroyed.");
            if (_storage.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Pool storage must remain inactive before renting.");
            Entry entry;
            // O(C) 仅发生在外部销毁了缓存对象时，C 不超过 maximumCached。
            do { entry = _cache.Get(); } while (entry.Instance == null);
            if (entry.Version == ulong.MaxValue)
            {
                DestroyEntry(entry);
                throw new InvalidOperationException("Pool lease generation exhausted.");
            }
            entry.Version++;
            entry.PendingVersion = 0;
            entry.ReturnAt = double.PositiveInfinity;
            entry.RentFrame = Time.frameCount;
            entry.ActiveIndex = _active.Count;
            _active.Add(entry);
            var lease = new PoolLease(entry);
            try
            {
                if (_policy == null) lease.ApplyDefaultPose(spawn);
                else _policy.Prepare(lease, spawn);
                RequirePreparedLease(entry, lease.Version);
                entry.Instance.SetActive(spawn.Activate);
                RequirePreparedLease(entry, lease.Version);
                return lease;
            }
            catch
            {
                AbortRent(entry, lease.Version);
                throw;
            }
        }

        private Entry Create()
        {
            if (_prefab == null) throw new InvalidOperationException("Pool prefab has been destroyed.");
            GameObject instance = Object.Instantiate(_prefab, _storage, false);
            try
            {
                instance.SetActive(false);
                if (_policy != null) _policy.OnCreated(instance);
                if (instance == null) throw new InvalidOperationException("OnCreated destroyed the pool instance.");
                return new Entry(this, instance);
            }
            catch
            {
                DestroyObject(instance);
                throw;
            }
        }

        internal bool IsCurrent(Entry entry, ulong version)
        {
            RequireThread();
            return !_disposed && entry.ActiveIndex >= 0 && entry.Version == version && entry.Instance != null;
        }

        internal bool ScheduleReturn(Entry entry, ulong version, float seconds)
        {
            if (!IsCurrent(entry, version)) return false;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            entry.ReturnAt = Time.timeAsDouble + Math.Max(0f, seconds);
            return true;
        }

        internal bool Return(Entry entry, ulong version, bool afterHierarchyChange)
        {
            if (!IsCurrent(entry, version)) return false;
            if (_policy != null && !_policy.CanReturn(entry.Instance)) return false;
            if (!IsCurrent(entry, version)) return false; // 资格回调可能重入、销毁或更换租用。
            Transform parent = entry.Instance.transform.parent;
            if (_deferInactiveParentReturns && Application.isPlaying && !afterHierarchyChange &&
                parent != null && !parent.gameObject.activeInHierarchy)
            {
                QueueReturn(entry, version);
                return true;
            }
            RemoveActive(entry);
            ResetAndStore(entry);
            return true;
        }

        private void QueueReturn(Entry entry, ulong version)
        {
            if (_context == null)
                throw new InvalidOperationException("Deferred return requires the Unity main-thread synchronization context.");
            entry.PendingVersion = version;
            if (entry.CallbackQueued) return;
            entry.CallbackQueued = true;
            try { _context.Post(_deferredReturn, entry); }
            catch { entry.CallbackQueued = false; entry.PendingVersion = 0; throw; }
        }

        private void ReturnAfterHierarchyChange(object state)
        {
            var entry = (Entry)state;
            entry.CallbackQueued = false;
            ulong version = entry.PendingVersion;
            entry.PendingVersion = 0;
            if (version == 0 || !IsCurrent(entry, version)) return;
            Return(entry, version, true);
        }

        /// <summary>
        /// 活跃实例 O(N) 扫描，不创建临时集合；失活实例仍处理寿命。拥有者在正常 Update 调用，不能从层级显隐回调调用。
        /// 限制为本次开始时的租用：策略重入创建的工作留到后续 Tick，不改变新租用的身份或寿命。
        /// </summary>
        public void Tick()
        {
            RequireThread();
            if (_disposed) return;
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                if (index >= _active.Count) continue;
                Entry entry = _active[index];
                if (entry.Instance == null) { RemoveActive(entry); continue; }
                ulong version = entry.Version;
                if (entry.PendingVersion == version)
                {
                    entry.PendingVersion = 0;
                    Return(entry, version, true);
                    continue;
                }
                if (entry.RentFrame == Time.frameCount) continue;
                bool expired = Time.timeAsDouble >= entry.ReturnAt;
                if (expired || (_policy != null && _policy.ShouldReturn(entry.Instance)))
                    Return(entry, version, true);
            }
        }

        private void RemoveActive(Entry entry)
        {
            int last = _active.Count - 1;
            Entry moved = _active[last];
            _active[entry.ActiveIndex] = moved;
            moved.ActiveIndex = entry.ActiveIndex;
            _active.RemoveAt(last);
            entry.ActiveIndex = -1;
            entry.PendingVersion = 0;
            entry.ReturnAt = double.PositiveInfinity;
        }

        private void ResetAndStore(Entry entry)
        {
            try
            {
                entry.Instance.SetActive(false);
                if (_policy != null) _policy.OnReturn(entry.Instance);
                if (entry.Instance == null) return;
                // 防御适配器错误激活；池内对象始终失活，且重入 Dispose 不能再把对象交回缓存。
                entry.Instance.SetActive(false);
                if (_disposed || _storage == null) DestroyEntry(entry);
                else
                {
                    // 转挂会触发 OnTransformParentChanged，可能重入 Dispose；必须在入缓存之前再检查。
                    entry.Instance.transform.SetParent(_storage, false);
                    if (_disposed || _storage == null || entry.Instance == null) DestroyEntry(entry);
                    else _cache.Release(entry);
                }
            }
            catch
            {
                DestroyEntry(entry);
                throw;
            }
        }

        private void AbortRent(Entry entry, ulong version)
        {
            if (entry.ActiveIndex < 0 || entry.Version != version) return;
            RemoveActive(entry);
            try
            {
                if (entry.Instance != null)
                {
                    entry.Instance.SetActive(false);
                    if (_policy != null) _policy.OnReturn(entry.Instance);
                }
            }
            catch (Exception cleanupError)
            {
                // 保留最初的准备异常；清理异常也必须可观察，最后始终销毁不完整实例。
                Debug.LogException(cleanupError);
            }
            finally { DestroyEntry(entry); }
        }

        private void RequirePreparedLease(Entry entry, ulong version)
        {
            if (!IsCurrent(entry, version))
                throw new InvalidOperationException("Pool lease was returned or destroyed during preparation/activation.");
        }

        /// <summary>释放全部活跃实例与缓存；失败回调不阻止其他实例清理，清理后汇总抛出异常。幂等。</summary>
        public void Dispose()
        {
            RequireThread();
            if (_disposed) return;
            _disposed = true;
            List<Exception> failures = null;
            while (_active.Count > 0)
            {
                Entry entry = _active[_active.Count - 1];
                RemoveActive(entry);
                try
                {
                    if (entry.Instance == null) continue;
                    entry.Instance.SetActive(false);
                    if (_policy != null) _policy.OnReturn(entry.Instance);
                }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
                finally { DestroyEntry(entry); }
            }
            _cache.Clear();
            if (_ownsStorage && _storage != null) DestroyObject(_storage.gameObject);
            if (failures != null) throw new AggregateException("Pool cleanup callbacks failed.", failures);
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
                throw new InvalidOperationException("PrefabPool must be used on its creating Unity main thread.");
        }

        private static void DestroyEntry(Entry entry) => DestroyObject(entry.Instance);

        private static void DestroyObject(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }
}
