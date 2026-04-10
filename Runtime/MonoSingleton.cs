using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting;

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
                return null;

            lock (_lock)
            {
                if (_instance != null)
                    return _instance;

                var instances = FindObjectsByType<T>(FindObjectsSortMode.None);
                if (instances.Length > 0)
                {
                    _instance = instances[0];
                    return _instance;
                }

                var go = new GameObject($"[{typeof(T).Name}]");
                _instance = go.AddComponent<T>();
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            // DontDestroyOnLoad(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
