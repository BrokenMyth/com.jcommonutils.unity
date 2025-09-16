using System;
using System.Collections.Generic;
using CommonUtils;

namespace Manager
{
    //TODO: 子节点单独刷新接口,map子节点缓存
    public class RedDotNode<T>
    {
        public T UI;
        public T RedUI;
        public string Key { get; }
        public int Count { get; private set; }
        public RedDotNode<T> Parent { get; }
        public Dictionary<T, RedDotNode<T>> Children { get; } = new();

        public Action<RedDotNode<T>, int> OnValueChanged; // 监听值变化（UI 会绑定这个）

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
        private RedDotNode<T> _root;
        public Action<RedDotNode<T>, int> onValueChanged; // 监听值变化（UI 会绑定这个）

        public RedDotNode<T> SetRootUI(T ui, Action<RedDotNode<T>, int> onValueChanged)
        {
            _root = new RedDotNode<T>(ui);
            _root.OnValueChanged = onValueChanged;
            return _root;
        }

        public RedDotNode<T> Register(T p, T ui, int count = 0)
        {
            var node = _root;
            node = node.GetOrAddChild(p);
            node = node.GetOrAddChild(ui);
            node.SetCount(count);
            return node;
        }
    }
}