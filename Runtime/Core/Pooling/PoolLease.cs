using UnityEngine;

namespace JCommonUtils.Pooling
{
    /// <summary>值类型租用凭据；旧凭据不能回收或设置下一次租用的寿命。仅在 Unity 主线程使用。</summary>
    public readonly struct PoolLease
    {
        private readonly PrefabPool.Entry _entry;

        internal PoolLease(PrefabPool.Entry entry)
        {
            _entry = entry;
            Version = entry.Version;
        }

        public ulong Version { get; }
        public bool IsValid => _entry != null && _entry.Owner.IsCurrent(_entry, Version);
        public GameObject Instance => IsValid ? _entry.Instance : null;
        public bool IsReturnPending => IsValid && _entry.PendingVersion == Version;

        /// <summary>返回是否已接受普通回收（包括安全延后）；资格不足或旧凭据返回 false。</summary>
        public bool TryReturn() => _entry != null && _entry.Owner.Return(_entry, Version, false);

        /// <summary>按 Time.timeAsDouble 的缩放时间设置当前租用寿命，需要拥有者继续调用池 Tick。</summary>
        public bool ReturnAfter(float seconds) =>
            _entry != null && _entry.Owner.ScheduleReturn(_entry, Version, seconds);

        // 默认租用恢复创建时的根缩放，避免世界挂接产生的逆父缩放跨租用累积；不涉及任何子节点。
        internal void ApplyDefaultPose(PoolSpawn spawn)
        {
            if (!IsValid) throw new System.InvalidOperationException("The pool lease is no longer valid.");
            spawn.ApplyToWithScale(_entry.Instance.transform, _entry.InitialLocalScale);
        }
    }
}
