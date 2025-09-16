using System;

namespace CommonUtils.Helper
{
    public static class TimerHelper
    {
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