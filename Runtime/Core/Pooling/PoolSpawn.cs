using UnityEngine;

namespace JCommonUtils.Pooling
{
    /// <summary>一次租用的根节点姿态；本工具不自动恢复子节点、物理或业务状态。</summary>
    public readonly struct PoolSpawn
    {
        public PoolSpawn(Vector3 position, Quaternion rotation, Transform parent = null,
            bool localPose = false, bool activate = true)
        {
            Position = position;
            Rotation = rotation;
            Parent = parent;
            LocalPose = localPose;
            Activate = activate;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Transform Parent { get; }
        public bool LocalPose { get; }
        public bool Activate { get; }

        /// <summary>应用根姿态并保留当前局部缩放；世界姿态挂接保持设置后的世界缩放。</summary>
        public void ApplyTo(Transform target) => ApplyPose(target, false, Vector3.one);

        internal void ApplyToWithScale(Transform target, Vector3 localScale) => ApplyPose(target, true, localScale);

        private void ApplyPose(Transform target, bool restoreScale, Vector3 localScale)
        {
            target.SetParent(LocalPose ? Parent : null, false);
            if (restoreScale) target.localScale = localScale;
            if (LocalPose) target.SetLocalPositionAndRotation(Position, Rotation);
            else
            {
                target.SetPositionAndRotation(Position, Rotation);
                if (Parent != null) target.SetParent(Parent, true);
            }
        }
    }
}
