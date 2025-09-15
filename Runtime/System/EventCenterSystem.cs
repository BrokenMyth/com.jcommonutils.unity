using System;
using System.Collections.Generic;

namespace CommonUtils.System
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
            for (var i = funcs.Count - 1; i >= 0; i--) funcs[i].DynamicInvoke(e);
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
            if (m_EventHandlers.TryGetValue(typeof(T), out var funcs) == false) return;

            funcs.Remove(func);
        }
    }
}