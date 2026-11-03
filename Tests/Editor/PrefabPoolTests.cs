using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using JCommonUtils.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JCommonUtils.Tests
{
    public sealed class PrefabPoolTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<PrefabPool> _pools = new List<PrefabPool>();

        private GameObject CreateObject(string name, bool active = false)
        {
            var value = new GameObject(name);
            value.SetActive(active);
            _objects.Add(value);
            return value;
        }

        private PrefabPool CreatePool(GameObject prefab, int capacity = 2, PrefabPoolPolicy policy = null)
        {
            var pool = new PrefabPool(prefab, maximumCached: capacity, policy: policy);
            _pools.Add(pool);
            return pool;
        }

        [Test]
        public void Lease_RejectsOldGenerationAndDuplicateReturn()
        {
            PrefabPool pool = CreatePool(CreateObject("Lease template"));
            PoolLease first = pool.Rent(Vector3.zero, Quaternion.identity);
            GameObject instance = first.Instance;
            Assert.That(first.TryReturn(), Is.True);
            Assert.That(first.IsValid, Is.False);
            Assert.That(first.Instance, Is.Null);
            Assert.That(first.TryReturn(), Is.False);
            PoolLease second = pool.Rent(Vector3.one, Quaternion.identity);
            Assert.That(second.Instance, Is.SameAs(instance));
            Assert.That(second.Version, Is.GreaterThan(first.Version));
            Assert.That(first.TryReturn(), Is.False);
            Assert.That(first.ReturnAfter(0f), Is.False);
            Assert.That(second.IsValid, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void DefaultPolicy_DoesNotResetUnrelatedComponentsOrChildTransforms()
        {
            GameObject prefab = CreateObject("Plain template");
            prefab.AddComponent<BoxCollider>();
            var child = new GameObject("Mutable child");
            child.transform.SetParent(prefab.transform, false);
            PrefabPool pool = CreatePool(prefab);
            PoolLease first = pool.Rent(Vector3.zero, Quaternion.identity);
            first.Instance.GetComponent<BoxCollider>().enabled = false;
            first.Instance.transform.GetChild(0).localPosition = Vector3.up * 7;
            first.TryReturn();
            PoolLease second = pool.Rent(Vector3.right, Quaternion.identity);
            Assert.That(second.Instance.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(second.Instance.transform.GetChild(0).localPosition, Is.EqualTo(Vector3.up * 7));
            Assert.That(second.Instance.transform.position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void Spawn_PreservesWorldAndLocalPoseAndCanRemainInactive()
        {
            GameObject parent = CreateObject("Scaled parent", true);
            parent.transform.SetPositionAndRotation(Vector3.up * 2, Quaternion.Euler(0, 90, 0));
            parent.transform.localScale = Vector3.one * 3;
            GameObject prefab = CreateObject("Pose template");
            prefab.transform.localScale = Vector3.one * 2;
            PrefabPool pool = CreatePool(prefab);
            PoolLease world = pool.Rent(new PoolSpawn(Vector3.right, Quaternion.identity, parent.transform, activate: false));
            Assert.That(world.Instance.activeSelf, Is.False);
            // 旋转且非单位缩放的父节点转挂会有浮点往返误差，按世界距离验收姿态。
            Assert.That(Vector3.Distance(world.Instance.transform.position, Vector3.right), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(world.Instance.transform.lossyScale, Vector3.one * 2), Is.LessThan(.0001f));
            PoolLease local = pool.Rent(new PoolSpawn(Vector3.up, Quaternion.identity, parent.transform, localPose: true));
            Assert.That(local.Instance.transform.localPosition, Is.EqualTo(Vector3.up));
            Assert.That(local.Instance.transform.localScale, Is.EqualTo(Vector3.one * 2));
        }

        [Test]
        public void RepeatedWorldRent_UnderScaledParentKeepsInitialRootScale()
        {
            GameObject parent = CreateObject("Repeated world parent", true);
            parent.transform.localScale = Vector3.one * 3;
            GameObject prefab = CreateObject("Repeated world template");
            Vector3 initial = new Vector3(2, 3, 4);
            prefab.transform.localScale = initial;
            PrefabPool pool = CreatePool(prefab);
            GameObject previous = null;
            for (int index = 0; index < 5; index++)
            {
                PoolLease lease = pool.Rent(Vector3.right, Quaternion.identity, parent.transform);
                if (previous != null) Assert.That(lease.Instance, Is.SameAs(previous));
                Assert.That(Vector3.Distance(lease.Instance.transform.lossyScale, initial), Is.LessThan(.0001f),
                    "父节点导致的逆缩放不能在重复世界租用时累积。");
                previous = lease.Instance;
                Assert.That(lease.TryReturn(), Is.True);
            }
        }

        [Test]
        public void WorldAndLocalRentSwitch_RestoresInitialScaleWithDefaultPolicyBase()
        {
            GameObject parent = CreateObject("Mixed pose parent", true);
            parent.transform.localScale = Vector3.one * 3;
            GameObject prefab = CreateObject("Mixed pose template");
            Vector3 initial = new Vector3(2, 3, 4);
            prefab.transform.localScale = initial;
            // 验证仅实现生命周期通知的策略，仍得到默认 Prepare 的根缩放保护。
            PrefabPool pool = CreatePool(prefab, policy: new TestPolicy());
            GameObject previous = null;
            for (int index = 0; index < 4; index++)
            {
                PoolLease world = pool.Rent(Vector3.zero, Quaternion.identity, parent.transform);
                if (previous != null) Assert.That(world.Instance, Is.SameAs(previous));
                Assert.That(Vector3.Distance(world.Instance.transform.lossyScale, initial), Is.LessThan(.0001f));
                previous = world.Instance;
                world.TryReturn();
                PoolLease local = pool.Rent(new PoolSpawn(Vector3.up, Quaternion.identity, parent.transform, localPose: true));
                Assert.That(local.Instance, Is.SameAs(previous));
                Assert.That(Vector3.Distance(local.Instance.transform.localScale, initial), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(local.Instance.transform.lossyScale, initial * 3), Is.LessThan(.0001f));
                local.TryReturn();
            }
        }

        [Test]
        public void CustomPrepare_RemainsResponsibleForItsOwnRootScale()
        {
            GameObject parent = CreateObject("Custom scale parent", true);
            parent.transform.localScale = Vector3.one * 3;
            GameObject prefab = CreateObject("Custom scale template");
            prefab.transform.localScale = Vector3.one * 2;
            PrefabPool pool = CreatePool(prefab, policy: new RootScalePolicy());
            PoolLease world = pool.Rent(Vector3.zero, Quaternion.identity, parent.transform);
            Assert.That(Vector3.Distance(world.Instance.transform.lossyScale, Vector3.one * 7), Is.LessThan(.0001f));
            world.TryReturn();
            PoolLease local = pool.Rent(new PoolSpawn(Vector3.zero, Quaternion.identity, parent.transform, localPose: true));
            Assert.That(Vector3.Distance(local.Instance.transform.localScale, Vector3.one * 7), Is.LessThan(.0001f));
        }

        [Test]
        public void Cache_IsBoundedAndSkipsExternallyDestroyedInstances()
        {
            PrefabPool pool = CreatePool(CreateObject("Bounded template"));
            PoolLease a = pool.Rent(Vector3.zero, Quaternion.identity);
            PoolLease b = pool.Rent(Vector3.zero, Quaternion.identity);
            PoolLease c = pool.Rent(Vector3.zero, Quaternion.identity);
            GameObject destroyedCached = a.Instance;
            GameObject overflow = c.Instance;
            a.TryReturn(); b.TryReturn(); c.TryReturn();
            Assert.That(pool.CachedCount, Is.EqualTo(2));
            Assert.That(overflow == null, Is.True);
            Object.DestroyImmediate(destroyedCached);
            PoolLease next = pool.Rent(Vector3.zero, Quaternion.identity);
            PoolLease replacement = pool.Rent(Vector3.zero, Quaternion.identity);
            Assert.That(next.IsValid && replacement.IsValid, Is.True);
            Assert.That(next.Instance, Is.Not.SameAs(replacement.Instance));
            Object.DestroyImmediate(next.Instance);
            pool.Tick();
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void Policy_CanBlockOrdinaryReturnButCannotBlockDisposal()
        {
            var policy = new TestPolicy { AllowReturn = false };
            PrefabPool pool = CreatePool(CreateObject("Blocked template"), policy: policy);
            PoolLease lease = pool.Rent(Vector3.zero, Quaternion.identity);
            GameObject instance = lease.Instance;
            Assert.That(lease.TryReturn(), Is.False);
            Assert.That(lease.IsValid, Is.True);
            pool.Dispose();
            Assert.That(lease.IsValid, Is.False);
            Assert.That(instance == null, Is.True);
            Assert.That(policy.Returned, Is.EqualTo(1));
            Assert.DoesNotThrow(pool.Dispose);
        }

        [Test]
        public void PrepareFailure_RemovesLeaseAndDestroysIncompleteInstance()
        {
            var policy = new TestPolicy { ThrowPrepare = true };
            PrefabPool pool = CreatePool(CreateObject("Fail prepare"), policy: policy);
            Assert.Throws<InvalidOperationException>(() => pool.Rent(Vector3.zero, Quaternion.identity));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.CachedCount, Is.Zero);
            Assert.That(policy.LastCreated == null, Is.True);
            Assert.That(policy.Returned, Is.EqualTo(1));
            policy.ThrowPrepare = false;
            Assert.That(pool.Rent(Vector3.zero, Quaternion.identity).IsValid, Is.True);
        }

        [Test]
        public void ReturnFailure_DestroysInstanceAndLeavesPoolUsable()
        {
            var policy = new TestPolicy { ThrowReturn = true };
            PrefabPool pool = CreatePool(CreateObject("Fail return"), policy: policy);
            PoolLease lease = pool.Rent(Vector3.zero, Quaternion.identity);
            GameObject instance = lease.Instance;
            Assert.Throws<InvalidOperationException>(() => lease.TryReturn());
            Assert.That(instance == null, Is.True);
            Assert.That(lease.IsValid, Is.False);
            Assert.That(pool.ActiveCount, Is.Zero);
            policy.ThrowReturn = false;
            Assert.That(pool.Rent(Vector3.zero, Quaternion.identity).IsValid, Is.True);
        }

        [Test]
        public void ReturnAndDispose_ReentryCannotDoubleReleaseOrResurrectCachedInstance()
        {
            var policy = new TestPolicy();
            PrefabPool pool = CreatePool(CreateObject("Reentry template"), policy: policy);
            PoolLease lease = pool.Rent(Vector3.zero, Quaternion.identity);
            GameObject instance = lease.Instance;
            policy.ReturnAction = () => { Assert.That(lease.TryReturn(), Is.False); pool.Dispose(); };
            Assert.That(lease.TryReturn(), Is.True);
            Assert.That(instance == null, Is.True);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.CachedCount, Is.Zero);
        }

        [Test]
        public void Dispose_CleansAllInstancesBeforeReportingCallbackFailures()
        {
            var policy = new TestPolicy { ThrowReturn = true };
            PrefabPool pool = CreatePool(CreateObject("Dispose template"), policy: policy);
            GameObject a = pool.Rent(Vector3.zero, Quaternion.identity).Instance;
            GameObject b = pool.Rent(Vector3.zero, Quaternion.identity).Instance;
            AggregateException error = Assert.Throws<AggregateException>(pool.Dispose);
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(a == null && b == null, Is.True);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.DoesNotThrow(pool.Dispose);
        }

        [Test]
        public void LeaseOperations_RejectOtherThreadsBeforeTouchingUnityObjects()
        {
            PrefabPool pool = CreatePool(CreateObject("Thread template"));
            PoolLease lease = pool.Rent(Vector3.zero, Quaternion.identity);
            Exception failure = null;
            var worker = new Thread(() => { try { lease.TryReturn(); } catch (Exception exception) { failure = exception; } });
            worker.Start();
            Assert.That(worker.Join(3000), Is.True);
            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(lease.IsValid, Is.True);
        }

        [Test]
        public void ExternalStorage_MustRemainInactiveAndIsNotOwnedByThePool()
        {
            GameObject storage = CreateObject("External storage");
            var pool = new PrefabPool(CreateObject("Storage template"), storage.transform);
            _pools.Add(pool);
            storage.SetActive(true);
            Assert.Throws<InvalidOperationException>(() => pool.Rent(Vector3.zero, Quaternion.identity));
            Assert.That(pool.ActiveCount, Is.Zero);
            storage.SetActive(false);
            PoolLease lease = pool.Rent(Vector3.zero, Quaternion.identity);
            Assert.That(lease.IsValid, Is.True);
            pool.Dispose();
            Assert.That(storage != null, Is.True);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DeferredReturnAndExpiry_ProtectNewLeaseAndWorkForHiddenObjects()
        {
            yield return new EnterPlayMode();
            GameObject owner = CreateObject("Disabling owner", true);
            GameObject prefab = CreateObject("Deferred template");
            prefab.AddComponent<PoolActivationProbe>();
            PrefabPool pool = CreatePool(prefab);
            PoolLease first = pool.Rent(Vector3.right * 5, Quaternion.identity, owner.transform);
            GameObject instance = first.Instance;
            Assert.That(instance.GetComponent<PoolActivationProbe>().EnabledAt, Is.EqualTo(Vector3.right * 5),
                "真实 OnEnable 必须观察到租用后的姿态，不能在创建到准备之间提前激活。");
            PoolTestOwner release = owner.AddComponent<PoolTestOwner>();
            release.Lease = first;
            owner.SetActive(false);
            Assert.That(release.ReturnAccepted, Is.True);
            Assert.That(first.IsReturnPending, Is.True);
            // 模拟同一帧另一个拥有者 Tick 已完成回收；旧 Post 到达时不得操作新租用。
            pool.Tick();
            Assert.That(first.IsValid, Is.False);
            PoolLease second = pool.Rent(Vector3.one, Quaternion.identity);
            Assert.That(second.Instance, Is.SameAs(instance));
            yield return null;
            yield return null;
            Assert.That(second.IsValid, Is.True);
            Assert.That(first.ReturnAfter(0f), Is.False);
            Assert.That(second.ReturnAfter(.03f), Is.True);
            second.Instance.SetActive(false);
            double until = Time.timeAsDouble + .08;
            while (Time.timeAsDouble < until) { pool.Tick(); yield return null; }
            Assert.That(second.IsValid, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PrefabPool pool in _pools) pool.Dispose();
            _pools.Clear();
            foreach (GameObject item in _objects) if (item != null) Object.DestroyImmediate(item);
            _objects.Clear();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private sealed class TestPolicy : PrefabPoolPolicy
        {
            internal bool AllowReturn = true, ThrowPrepare, ThrowReturn;
            internal int Returned;
            internal GameObject LastCreated;
            internal Action ReturnAction;
            public override void OnCreated(GameObject instance) => LastCreated = instance;
            public override void Prepare(PoolLease lease, PoolSpawn spawn)
            {
                if (ThrowPrepare) throw new InvalidOperationException("Expected preparation failure.");
                base.Prepare(lease, spawn);
            }
            public override bool CanReturn(GameObject instance) => AllowReturn;
            public override void OnReturn(GameObject instance)
            {
                Returned++;
                if (ThrowReturn) throw new InvalidOperationException("Expected return failure.");
                ReturnAction?.Invoke();
            }
        }

        private sealed class RootScalePolicy : PrefabPoolPolicy
        {
            public override void Prepare(PoolLease lease, PoolSpawn spawn)
            {
                lease.Instance.transform.localScale = Vector3.one * 7;
                spawn.ApplyTo(lease.Instance.transform);
            }
        }
    }

    public sealed class PoolTestOwner : MonoBehaviour
    {
        internal PoolLease Lease;
        internal bool ReturnAccepted;
        private void OnDisable() => ReturnAccepted = Lease.TryReturn();
    }

    public sealed class PoolActivationProbe : MonoBehaviour
    {
        internal Vector3 EnabledAt;
        private void OnEnable() => EnabledAt = transform.position;
    }
}
