using UnityEngine;

namespace JCommonUtils.Hierarchy
{
    /// <summary>按层级顺序查找名称；包含失活节点，不保存会随层级变化失效的跨帧缓存。</summary>
    public static class TransformSearch
    {
        /// <summary>
        /// 在后代中返回深度优先的首个精确名称匹配，不检查 root 自身；空或已销毁的 root 返回 null。
        /// 复杂度 O(N)，N 不超过 root 的后代数量，递归栈 O(H)，H 为层级深度。
        /// 适用于初始化或重绑定；不创建临时集合/枚举器，但 Unity 的名称读取不承诺零分配。
        /// </summary>
        public static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindSelfOrChildRecursive(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 先检查 root，再按深度优先顺序查找后代；名称匹配区分大小写，失活节点仍参与查找。
        /// 复杂度 O(N)，N 不超过包含 root 的子树节点数，递归栈 O(H)，不创建临时集合/枚举器。
        /// </summary>
        public static Transform FindSelfOrChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            return FindChildRecursive(root, name);
        }
    }
}
