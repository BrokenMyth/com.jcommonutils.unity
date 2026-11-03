using System;
using System.Collections.Generic;

namespace JCommonUtils.Editor.Diagnostics
{
    /// <summary>离线统计快照；空样本返回 null，不把缺失测量伪装为零。</summary>
    public sealed class PerformanceStatistics
    {
        public int Samples { get; }
        public double Mean { get; }
        public double P50 { get; }
        public double P95 { get; }
        public double Max { get; }

        private PerformanceStatistics(int samples, double mean, double p50, double p95, double max)
        {
            Samples = samples;
            Mean = mean;
            P50 = p50;
            P95 = p95;
            Max = max;
        }

        /// <summary>
        /// 复制后排序，不修改调用者的数据。沿用项目历史报告的 floor(N*p) 零基索引分位定义，
        /// 偶数样本的 P50 取上中位数；不是插值或 nearest-rank 分位数。
        /// 冷路径 O(N log N)，分配 N 个 double；采样器限制 N 不超过 MaxSamples。
        /// </summary>
        public static PerformanceStatistics Calculate(IReadOnlyList<double> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count == 0) return null;
            var sorted = new double[values.Count];
            double mean = 0;
            for (int i = 0; i < sorted.Length; ++i)
            {
                double value = values[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentException("Samples must contain only finite values.", nameof(values));
                sorted[i] = value;
                // 加权求均值避免总和或 value-mean 在有限大数/异号输入下溢出为 Infinity。
                mean = mean * ((double)i / (i + 1)) + value / (i + 1);
            }
            Array.Sort(sorted);
            return new PerformanceStatistics(sorted.Length, mean, sorted[sorted.Length / 2],
                sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * .95))], sorted[sorted.Length - 1]);
        }
    }
}
