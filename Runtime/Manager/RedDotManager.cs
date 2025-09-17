using System;
using System.Collections.Generic;
using CommonUtils;

namespace Manager
{
    public class RedDotNode<T>
    {
        public T UI;
        public T RedUI;
        public string Key { get; }
        public int Count { get; private set; }

        public bool IsRoot;
        public RedDotNode<T> Parent { get; }
        public Dictionary<T, RedDotNode<T>> Children { get; } = new();

        public Action<RedDotNode<T>, int> OnValueChanged; // 监听值变化（UI 会绑定这个）

        public void SetRoot(bool isRoot)
        {
            IsRoot = isRoot;
        }
        
        public RedDotNode(T key, RedDotNode<T> parent = null)
        {
            UI = key;
            Parent = parent;
        }
        public RedDotNode<T> GetOrAddChild(T key)
        {
            if (!Children.TryGetValue(key, out var child))
            {
                child = new RedDotNode<T>(key, this);
                child.OnValueChanged = OnValueChanged;
                Children[key] = child;
            }

            return child;
        }

        public void SetCount(int count)
        {
            Count = count;
            OnValueChanged?.Invoke(this, Count);
            Parent?.RefreshFromChildren();
        }

        public void RefreshFromChildren()
        {
            if (IsRoot) return;
            var total = 0;
            foreach (var child in Children.Values)
                total += child.Count;

            Count = total;
            OnValueChanged?.Invoke(this, Count);
            Parent?.RefreshFromChildren();
        }
    }

    public class RedDotManager<T> : Singleton<RedDotManager<T>>
    {
        private RedDotNode<T> _root; //根节点,一般是画布,不会有红点
        public Action<RedDotNode<T>, int> onValueChanged; // 监听值变化（UI 会绑定这个）
        public Dictionary<T, RedDotNode<T>> RedDotNodeCache = new(); //节点缓存列表,只缓存最后一层子节点

        /// <summary>
        ///     设置根节点
        /// </summary>
        /// <param name="PARAM">PARAM_COMMENT</param>
        /// <returns>RETURNS</returns>
        public RedDotNode<T> SetRootUI(T ui, Action<RedDotNode<T>, int> onValueChanged)
        {
            _root = new RedDotNode<T>(ui);
            _root.OnValueChanged = onValueChanged;
            _root.SetRoot(true);
            RedDotNodeCache[ui] = _root;
            return _root;
        }

        /// <summary>
        ///     刷新根节点的UI
        /// </summary>
        public bool RefreshNode(T ui, int count)
        {
            if (RedDotNodeCache.TryGetValue(ui, out var node))
            {
                node.SetCount(count);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     注册并刷新红点
        /// </summary>
        /// <param name="p">父 UI</param>
        /// <param name="ui">子 UI</param>
        /// <param name="ui">子 UI 的红点数</param>
        /// <returns>RETURNS</returns>
        public RedDotNode<T> Register(T p, T ui, int count = 0)
        {
            var node = _root;
            if (RedDotNodeCache.TryGetValue(p, out var nodeCache))
                node = nodeCache;
            else
                node = node.GetOrAddChild(p);

            RedDotNodeCache[p] = node;
            node = node.GetOrAddChild(ui);
            RedDotNodeCache[ui] = node;
            node.SetCount(count);
            return node;
        }
    }
}