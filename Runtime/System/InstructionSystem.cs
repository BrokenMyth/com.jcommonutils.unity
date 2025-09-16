using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Extensions;
using UnityEngine;

namespace CommonUtils.System
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
            public List<object> parameters = new();
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

            return InstructionsActions[m]?.Invoke(new Context { parameters = objects });
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