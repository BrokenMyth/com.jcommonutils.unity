using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Extensions;
using UnityEngine;

namespace System
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InstructionAttribute : Attribute
    {
        /// <summary>
        ///     操作指令名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     初始化
        /// </summary>
        /// <param name="name">操作指令名称。全局必须唯一</param>
        public InstructionAttribute(string name)
        {
            Name = name;
        }
    }

    public class InstructionSystem : Singleton<InstructionSystem>
    {
        public class Context
        {
            public List<object> Arguments = new();

            /// <summary>
            /// </summary>
            public bool TryParseArgument<T>(out T count)
            {
                count = default;
                if (Arguments.Count <= 0) return false;

                return TryParse(Arguments[0].ToString(), out count);
            }

            /// <summary>
            /// </summary>
            public bool TryParseArgument<T>(out T count, out T count1)
            {
                count = default;
                count1 = default;
                if (Arguments.Count <= 1) return false;

                return TryParse(Arguments[0].ToString(), out count) && TryParse(Arguments[1].ToString(), out count1);
            }

            /// <summary>
            ///     TryParseArguments 方法
            /// </summary>
            public bool TryParseArguments<T>(out List<T> values)
            {
                values = new List<T>();
                if (Arguments.Count == 0) return false;
                for (var i = 0; i < Arguments.Count; i++)
                    if (TryParse(Arguments[i].ToString(), out T parsedValue))
                        values.Add(parsedValue);
                    else
                        return false;

                return true;
            }

            private bool TryParse<T>(string input, out T value)
            {
                value = default;
                try
                {
                    if (typeof(T) == typeof(int))
                    {
                        if (int.TryParse(input, out var intValue))
                        {
                            value = (T)(object)intValue;
                            return true;
                        }
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        if (long.TryParse(input, out var intValue))
                        {
                            value = (T)(object)intValue;
                            return true;
                        }
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        if (double.TryParse(input, out var doubleValue))
                        {
                            value = (T)(object)doubleValue;
                            return true;
                        }
                    }
                    else if (typeof(T) == typeof(bool))
                    {
                        if (bool.TryParse(input, out var boolValue))
                        {
                            value = (T)(object)boolValue;
                            return true;
                        }
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }
        }

        public Dictionary<Instruction, bool> Instructions = new();

        //原始方法名:P
        public Dictionary<string, Func<Context, object>> InstructionsActions = new();

        /// <summary>
        ///     Register 方法
        /// </summary>
        public bool Register(Instruction i)
        {
            //注册并解析方法
            if (Instructions.TryAdd(i, true))
            {
                var type = i.GetType();
                var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var method in methodInfos)
                {
                    var attribute = method.GetCustomAttribute<InstructionAttribute>();
                    if (attribute.IsNull()) continue;
                    if (InstructionsActions.ContainsKey(attribute.Name))
                    {
                        Debug.LogWarning("指令已注册: " + attribute.Name);
                        continue;
                    }

                    InstructionsActions[attribute.Name] = c => method.Invoke(i, new object[] { c });
                }
            }

            return true;
        }

        public (string, List<object>) ParseMethods(string input)
        {
            // 正则表达式：匹配方法名和括号内的内容
            var pattern = @"(\w+)\((.*?)\)";
            var regex = new Regex(pattern);

            // 匹配第一个符合条件的字符串
            var match = regex.Match(input);

            // 如果找到了匹配
            if (match.Success)
            {
                // 方法名
                var methodName = match.Groups[1].Value;
                // 参数部分
                var parameters = match.Groups[2].Value;

                // 将参数按逗号分割成数组
                var args = parameters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                return (methodName, args.Cast<object>().ToList());
            }

            return (null, null);
        }

        public object Execute(string i)
        {
            var (m, objects) = ParseMethods(i);
            if (!InstructionsActions.ContainsKey(m))
            {
                Debug.LogWarning("指令不存在!");
                return null;
            }

            return InstructionsActions[m]?.Invoke(new Context { Arguments = objects });
        }
    }

    public abstract class Instruction
    {
        public Instruction()
        {
            Register();
        }

        /// <summary>
        ///     Register 方法
        /// </summary>
        public bool Register()
        {
            return InstructionSystem.Instance.Register(this);
        }
    }
}