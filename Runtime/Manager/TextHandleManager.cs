using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Extensions;
using UnityEngine;

namespace Manager
{
     /// <summary>
    /// 文本内容处理器
    /// </summary>
    public sealed class TextHandleManager : Singleton<TextHandleManager>
    {
        /// <summary>
        /// 通用文本处理 K M B（保留两位小数，向下取整）
        /// </summary>
        /// <param name="number">数量</param>
        /// <param name="keepDecimal">是否保留两位小数(向下取整)</param>
        /// <returns>格式化后的字符串</returns>
        public static string HandleNumber(double number, bool keepDecimal = true)
        {
            if (number < 10000)
            {
                return number.ToString(keepDecimal ? "0.##" : "0");
            }

            string[] units = { "", "K", "M", "B" };
            double num = number;
            int unitIndex = 0;

            while (num >= 1000 && unitIndex < units.Length - 1)
            {
                num /= 1000;
                unitIndex++;
            }


            string formattedNumber = (num.ToString(keepDecimal ? "0.##" : "0"));
            return $"{formattedNumber}{units[unitIndex]}";
        }

        /// <summary>
        /// 
        /// </summary>
        public static string HandleNumber(long number, bool keepDecimal = true)
        {
            return HandleNumber((double)number, keepDecimal);
        }

        /// <summary>
        /// 将字符串转换为百分比格式
        /// </summary>
        public static string ConvertToPercentage(string input)
        {
            if (input.IsNullOrEmpty())
            {
                return "";
            }

            if (double.TryParse(input, out double value))
            {
                return (value * 100).ToString("0.##") + "%";
            }
            else
            {
                throw new ArgumentException("输入的字符串不是有效的数字");
            }
        }
        
        /// <summary>
        /// IsChinese 方法
        /// </summary>
        /// <param name="c">参数 c</param>
        /// <returns>bool 类型的返回值</returns>
        public static bool IsChinese(char c)
        {
            return Regex.IsMatch(c.ToString(), @"[\u4e00-\u9fa5]");
        }
    }
}