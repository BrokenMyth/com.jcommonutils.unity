using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace JCommonUtils.Editor.Diagnostics
{
    /// <summary>一个原生计数器的选择；数值保持原始单位，不推测 CPU/GPU 含义。</summary>
    public readonly struct PerformanceCounterDefinition
    {
        public ProfilerCategory Category { get; }
        public string Name { get; }
        public string Unit { get; }

        public PerformanceCounterDefinition(ProfilerCategory category, string name, string unit)
        {
            Category = category;
            Name = name;
            Unit = unit;
        }
    }

    /// <summary>启动时复制的采样配置；不是自动注册或常驻组件。</summary>
    public sealed class PerformanceCaptureOptions
    {
        public string Label { get; set; } = "Capture";
        public string Context { get; set; } = string.Empty;
        public double WarmupSeconds { get; set; } = 2;
        public double DurationSeconds { get; set; } = 30;
        public int MaxSamples { get; set; } = 16384;
        public IReadOnlyList<PerformanceCounterDefinition> Counters { get; set; } = new[]
        {
            new PerformanceCounterDefinition(ProfilerCategory.Memory, "GC Allocated In Frame", "bytes"),
            new PerformanceCounterDefinition(ProfilerCategory.Render, "Draw Calls Count", "count"),
            new PerformanceCounterDefinition(ProfilerCategory.Render, "Triangles Count", "count")
        };

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Label)) throw new ArgumentException("A capture label is required.");
            if (!IsFinite(WarmupSeconds) || WarmupSeconds < 0 || WarmupSeconds > 3600)
                throw new ArgumentOutOfRangeException(nameof(WarmupSeconds));
            if (!IsFinite(DurationSeconds) || DurationSeconds <= 0 || DurationSeconds > 3600)
                throw new ArgumentOutOfRangeException(nameof(DurationSeconds));
            if (MaxSamples < 1 || MaxSamples > 1000000)
                throw new ArgumentOutOfRangeException(nameof(MaxSamples));
            if (Counters == null || Counters.Count > 16)
                throw new ArgumentException("Select between zero and sixteen counters.", nameof(Counters));
            for (int i = 0; i < Counters.Count; ++i)
            {
                if (string.IsNullOrWhiteSpace(Counters[i].Name) || string.IsNullOrWhiteSpace(Counters[i].Unit))
                    throw new ArgumentException("Counter names and units must not be empty.", nameof(Counters));
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
