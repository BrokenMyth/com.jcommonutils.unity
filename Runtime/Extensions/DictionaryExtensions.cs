using System;
using System.Collections.Generic;

namespace Extensions
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static void MergeValue<TKey, TValue>(
            this Dictionary<TKey, TValue> source,
            Dictionary<TKey, TValue> other)
        {
            if (other.IsNullOrEmpty())
            {
                return;
            }
            foreach (var kvp in other)
            {
                source.MergeValue(kvp.Key, kvp.Value);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public static void MergeValue<TKey, TValue>(
            this Dictionary<TKey, TValue> dict,
            TKey key,
            TValue value)
        {
            if (dict.TryGetValue(key, out TValue existingValue))
            {
                // 动态检查是否支持加法
                if (existingValue is IConvertible && value is IConvertible)
                {
                    dict[key] = (TValue)Convert.ChangeType(
                        Convert.ToDouble(existingValue) + Convert.ToDouble(value),
                        typeof(TValue)
                    );
                }
                else
                {
                    throw new InvalidOperationException($"Type {typeof(TValue)} does not support addition.");
                }
            }
            else
            {
                dict[key] = value;
            }
        }
    }
}