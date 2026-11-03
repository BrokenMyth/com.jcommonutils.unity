using JCommonUtils.Pooling;
using UnityEngine;

namespace JCommonUtils.Samples
{
    /// <summary>挂到任意空物体即可运行；创建三个方块，两秒后回收，退出时清理所有实例与模板。</summary>
    public sealed class PoolExample : MonoBehaviour
    {
        private PrefabPool _pool;
        private GameObject _template;

        private void Start()
        {
            _template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template.name = "Pool example template";
            _template.SetActive(false);
            _pool = new PrefabPool(_template, maximumCached: 3);
            for (int index = 0; index < 3; index++)
            {
                PoolLease lease = _pool.Rent(transform.position + Vector3.right * index * 1.5f, Quaternion.identity);
                lease.ReturnAfter(2f);
            }
        }

        private void Update()
        {
            if (_pool != null) _pool.Tick();
        }

        private void OnDestroy()
        {
            if (_pool != null) _pool.Dispose();
            if (_template != null) Destroy(_template);
        }
    }
}
