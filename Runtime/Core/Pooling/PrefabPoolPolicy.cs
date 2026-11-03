using UnityEngine;

namespace JCommonUtils.Pooling
{
    /// <summary>
    /// 可选的实例状态适配。一个策略由一个池复用，所有回调都在创建池的 Unity 主线程执行。
    /// 默认设置根姿态并恢复创建时的根缩放；SDK 清理、粒子和业务重置由消费项目显式实现。
    /// </summary>
    public abstract class PrefabPoolPolicy
    {
        /// <summary>实例首次创建且尚未激活；只缓存或初始化状态，不得激活、重新挂接或销毁实例。</summary>
        public virtual void OnCreated(GameObject instance) { }

        /// <summary>租用有效且实例尚未激活；覆盖时由调用方负责设置姿态与全部复用状态。</summary>
        public virtual void Prepare(PoolLease lease, PoolSpawn spawn) => lease.ApplyDefaultPose(spawn);

        /// <summary>普通回收资格。池销毁和准备失败不会被此条件阻止。</summary>
        public virtual bool CanReturn(GameObject instance) => true;

        /// <summary>可选的自定义寿命，例如粒子全部结束。仅在池 Tick 中调用。</summary>
        public virtual bool ShouldReturn(GameObject instance) => false;

        /// <summary>租用身份已撤销且实例已停用。不得再次激活或把实例交给其他池。</summary>
        public virtual void OnReturn(GameObject instance) { }
    }
}
