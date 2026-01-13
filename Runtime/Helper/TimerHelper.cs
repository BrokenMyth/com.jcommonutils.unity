using System;
using UnityEngine;

namespace Helper
{
    public static class TimerHelper
    {
        /// <summary>
        ///     设置今日不再弹出,true 不再弹出 , false 可以弹出
        /// </summary>
        public static void SetNotOpenToday(string key, bool open = true)
        {
            var value = -1;
            if (open) value = DateTime.UtcNow.Date.DayOfYear;
            PlayerPrefs.SetInt(key, value);
        }

        /// <summary>
        ///     今日不再弹出?
        /// </summary>
        public static bool IsNotOpenToday(string key)
        {
            var colseDay = PlayerPrefs.GetInt(key, -1);
            if (colseDay == DateTime.UtcNow.Date.DayOfYear) return true;

            return false;
        }
        /// <summary>
        ///     当前UTC 时间 秒时间戳
        /// </summary>
        public static long CurrentUnixTimeSeconds()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        }

        public static int GetCrossDays(long lasTimeSeconds, long currentSeconds)
        {
            // 将时间戳转换为 DateTime 对象
            var lasTime = DateTimeOffset.FromUnixTimeSeconds(lasTimeSeconds).DateTime;
            var currentTime = DateTimeOffset.FromUnixTimeSeconds(currentSeconds).DateTime;

            // 比较日期部分（只关心年、月、日）
            var daysDifference = (currentTime.Date - lasTime.Date).Days;

            // 返回天数差异
            return daysDifference;
        }
    }
}