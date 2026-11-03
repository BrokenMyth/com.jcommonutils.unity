using JCommonUtils.Geometry;
using JCommonUtils.Hierarchy;
using UnityEngine;

namespace JCommonUtils.Samples
{
    /// <summary>挂到空物体后运行；演示失活后代查找与水平扇形，不依赖预制体、材质或项目配置。</summary>
    public sealed class HierarchyGeometryExample : MonoBehaviour
    {
        private void Start()
        {
            Transform branch = new GameObject("Sample branch").transform;
            branch.SetParent(transform, false);
            Transform target = new GameObject("Sample target").transform;
            target.SetParent(branch, false);
            target.position = transform.position + Vector3.forward;
            branch.gameObject.SetActive(false);

            // 无需激活节点即可查找；生产调用应在初始化/重绑定时保存需要的引用。
            Transform found = TransformSearch.FindChildRecursive(transform, "Sample target");
            bool inside = HorizontalSector.ContainsWorldPoint(transform, Vector3.zero, 2f, 90f, true, 1f, target.position);
            Debug.Log("Hierarchy match: " + (found == target) + "; inside horizontal sector: " + inside, this);
        }

        private void OnDrawGizmos()
        {
            HorizontalSectorGizmos.Draw(transform, Vector3.zero, 2f, 90f,
                new Color(0f, 1f, 1f, .25f), Color.cyan);
        }
    }
}
