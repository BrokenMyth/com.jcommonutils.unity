using System.Collections.Generic;

namespace System
{
    /// <summary>
    ///     事件接口
    /// </summary>
    public interface IEvent
    {
    }

    public interface IEventSystem
    {
        /// <summary>
        ///     派发事件
        /// </summary>
        /// <param name="e"></param>
        /// <typeparam name="T"></typeparam>
        public void Fire<T>(T e = default) where T : IEvent;

        /// <summary>
        ///     订阅事件
        /// </summary>
        /// <param name="func"></param>
        /// <typeparam name="T"></typeparam>
        public void Subscribe<T>(Action<T> func) where T : IEvent;

        /// <summary>
        ///     取消订阅事件
        /// </summary>
        /// <param name="func"></param>
        /// <typeparam name="T"></typeparam>
        public void Unsubscribe<T>(Action<T> func) where T : IEvent;
    }

    /// <summary>
    ///     事件系统
    /// </summary>
    public sealed class EventCenterSystem : Singleton<EventCenterSystem>, IEventSystem
    {
        private Dictionary<Type, List<Delegate>> m_EventHandlers;

        /// <summary>
        ///     触发事件
        /// </summary>
        public void Fire<T>(T e = default) where T : IEvent
        {
            m_EventHandlers ??= new Dictionary<Type, List<Delegate>>();
            if (!m_EventHandlers.TryGetValue(typeof(T), out var funcs)) return;
            for (var i = funcs.Count - 1; i >= 0; i--)
            {
                // Subscribe<T> 保证同一事件类型只保存 Action<T>；直接调用避免每个监听者的
                // 反射参数数组与值类型事件装箱，仍按原逆序实时遍历，保留重入和取消订阅语义。
                var callback = (Action<T>)funcs[i];
                if (callback == null) throw new NullReferenceException();
                try
                {
                    callback(e);
                }
                catch (Exception exception)
                {
                    // 保持 DynamicInvoke 的异常包装及中止分发行为，避免改变既有调用方的错误处理。
                    throw new Reflection.TargetInvocationException(exception);
                }
            }
        }

        /// <summary>
        ///     注册事件监听器
        /// </summary>
        public void Subscribe<T>(Action<T> func) where T : IEvent
        {
            m_EventHandlers ??= new Dictionary<Type, List<Delegate>>();
            if (!m_EventHandlers.TryGetValue(typeof(T), out var funcs)) m_EventHandlers.TryAdd(typeof(T), funcs = new List<Delegate>());
            if (funcs.Contains(func)) return;
            funcs.Add(func);
        }

        /// <summary>
        ///     移除事件监听器
        /// </summary>
        public void Unsubscribe<T>(Action<T> func) where T : IEvent
        {
            List<Delegate> funcs = null;
            if (m_EventHandlers?.TryGetValue(typeof(T), out funcs) == false) return;

            funcs?.Remove(func);
        }
    }
}
