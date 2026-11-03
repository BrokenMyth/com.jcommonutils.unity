using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace JCommonUtils.Editor.Diagnostics
{
    public enum PerformanceCaptureEndReason
    {
        Completed, Stopped, Disposed, CapacityReached, PlayModeExited, Paused, DomainReload, EditorQuitting
    }

    /// <summary>
    /// 主线程显式启动的 Editor 游戏帧采样。每个 Time.frameCount 最多记录一次；不会开启全局 Profiler、
    /// 改画质、创建相机或连接房间。停止后解除全部回调并释放原生 recorder，报告生成是独立冷路径。
    /// </summary>
    public sealed class PerformanceCapture : IDisposable
    {
        private readonly string label, context, startedUtc, unityVersion;
        private readonly double warmupSeconds, durationSeconds, sampleStart, deadline;
        private readonly int capacity;
        private readonly List<double> frameTimes;
        private readonly CounterCapture[] counters;
        private int lastFrame;
        private double endedAt;
        private PerformanceCaptureEndReason endReason;
        private PerformanceReport report;

        public bool IsRunning { get; private set; }
        public int Samples => frameTimes.Count;
        public int MissedFrames { get; private set; }

        private PerformanceCapture(PerformanceCaptureOptions options)
        {
            label = options.Label;
            context = options.Context ?? string.Empty;
            warmupSeconds = options.WarmupSeconds;
            durationSeconds = options.DurationSeconds;
            capacity = options.MaxSamples;
            startedUtc = DateTime.UtcNow.ToString("O");
            unityVersion = Application.unityVersion;
            frameTimes = new List<double>(capacity);
            counters = new CounterCapture[options.Counters.Count];
            // 可用计数器只在冷路径枚举一次。未知名称不调用原生 StartNew，避免 Unity 把缺失能力打印成错误。
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            try
            {
                for (int i = 0; i < counters.Length; ++i)
                    counters[i] = new CounterCapture(options.Counters[i], capacity, handles);
            }
            catch
            {
                DisposeCounters();
                throw;
            }
            // 预分配和句柄发现是启动冷路径，不能消耗用户要求的预热与测量窗口。
            sampleStart = EditorApplication.timeSinceStartup + warmupSeconds;
            deadline = sampleStart + durationSeconds;
            lastFrame = Time.frameCount;
            IsRunning = true;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>仅在未暂停的 Play 模式从主线程调用；调用方持有并最终 Dispose 返回对象。</summary>
        public static PerformanceCapture Start(PerformanceCaptureOptions options = null)
        {
            options = options ?? new PerformanceCaptureOptions();
            options.Validate();
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Start capture in unpaused Play mode.");
            return new PerformanceCapture(options);
        }

        /// <summary>提前结束并保留已采样结果；重复调用不重新订阅或释放。</summary>
        public void Stop() => Finish(PerformanceCaptureEndReason.Stopped);

        public void Dispose() => Finish(PerformanceCaptureEndReason.Disposed);

        /// <summary>结束后才排序并生成快照，避免报告构建污染采样窗口；可在 Dispose 后读取。</summary>
        public PerformanceReport CreateReport()
        {
            if (IsRunning) throw new InvalidOperationException("Stop capture before creating a report.");
            if (report != null) return report;
            var metrics = new PerformanceMetric[counters.Length];
            for (int i = 0; i < counters.Length; ++i) metrics[i] = counters[i].CreateMetric(frameTimes.Count);
            report = new PerformanceReport(label, context, startedUtc, unityVersion, warmupSeconds, durationSeconds,
                Math.Max(0, endedAt - sampleStart), capacity, MissedFrames, endReason,
                PerformanceStatistics.Calculate(frameTimes), metrics);
            return report;
        }

        /// <summary>
        /// 每个观测帧 O(C)，C 最多 16；List 容量在启动时预留，采样窗口不扩容、不写盘、不格式化字符串。
        /// Editor 更新可能跳过游戏帧，累计 MissedFrames 并保留真实样本数，不插值或补零制造连续帧。
        /// </summary>
        private void Tick()
        {
            if (!IsRunning) return;
            if (!EditorApplication.isPlaying) { Finish(PerformanceCaptureEndReason.PlayModeExited); return; }
            if (EditorApplication.isPaused) { Finish(PerformanceCaptureEndReason.Paused); return; }
            double now = EditorApplication.timeSinceStartup;
            int frame = Time.frameCount;
            if (now < sampleStart) { lastFrame = frame; return; }
            if (now >= deadline) { Finish(PerformanceCaptureEndReason.Completed); return; }
            if (frame == lastFrame) return;
            if (frame > lastFrame + 1) MissedFrames += frame - lastFrame - 1;
            lastFrame = frame;
            frameTimes.Add(Time.unscaledDeltaTime * 1000d);
            for (int i = 0; i < counters.Length; ++i) counters[i].Sample();
            if (frameTimes.Count >= capacity) Finish(PerformanceCaptureEndReason.CapacityReached);
        }

        private void Finish(PerformanceCaptureEndReason reason)
        {
            if (!IsRunning) return;
            IsRunning = false;
            endReason = reason;
            endedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            EditorApplication.quitting -= OnEditorQuitting;
            DisposeCounters();
        }

        private void DisposeCounters()
        {
            for (int i = 0; i < counters.Length; ++i) counters[i]?.Dispose();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Finish(PerformanceCaptureEndReason.PlayModeExited);
        }

        private void OnBeforeReload() => Finish(PerformanceCaptureEndReason.DomainReload);
        private void OnEditorQuitting() => Finish(PerformanceCaptureEndReason.EditorQuitting);

        private sealed class CounterCapture : IDisposable
        {
            private readonly PerformanceCounterDefinition definition;
            private readonly List<double> values;
            private ProfilerRecorder recorder;
            private readonly bool available;
            private readonly ProfilerMarkerDataType dataType;

            internal CounterCapture(PerformanceCounterDefinition definition, int capacity,
                List<ProfilerRecorderHandle> handles)
            {
                this.definition = definition;
                values = new List<double>(capacity);
                for (int i = 0; i < handles.Count; ++i)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handles[i]);
                    if (description.Name != definition.Name || description.Category.Name != definition.Category.Name) continue;
                    dataType = description.DataType;
                    if (dataType != ProfilerMarkerDataType.Int32 && dataType != ProfilerMarkerDataType.Int64 &&
                        dataType != ProfilerMarkerDataType.UInt32 && dataType != ProfilerMarkerDataType.UInt64 &&
                        dataType != ProfilerMarkerDataType.Float && dataType != ProfilerMarkerDataType.Double) break;
                    recorder = new ProfilerRecorder(handles[i], 1, ProfilerRecorderOptions.StartImmediately |
                        ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                    available = recorder.Valid;
                    break;
                }
            }

            internal void Sample()
            {
                if (!recorder.Valid || recorder.Count == 0) return;
                double value = dataType == ProfilerMarkerDataType.Float || dataType == ProfilerMarkerDataType.Double
                    ? recorder.LastValueAsDouble : dataType == ProfilerMarkerDataType.UInt64
                        ? (double)unchecked((ulong)recorder.LastValue) : recorder.LastValue;
                // 不向统计写入非数值，也不把异常计数器值转换为零；报告通过较少样本标为 partial。
                if (!double.IsNaN(value) && !double.IsInfinity(value)) values.Add(value);
            }

            internal PerformanceMetric CreateMetric(int observedFrames)
            {
                string state = !available ? "unavailable" : values.Count == 0 ? "no_samples" :
                    values.Count < observedFrames ? "partial" : "available";
                return new PerformanceMetric(definition.Name, definition.Category.Name, definition.Unit,
                    state, PerformanceStatistics.Calculate(values));
            }

            public void Dispose()
            {
                if (recorder.Valid) recorder.Dispose();
            }
        }
    }
}
