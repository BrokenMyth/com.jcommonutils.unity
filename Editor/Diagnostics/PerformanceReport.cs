using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace JCommonUtils.Editor.Diagnostics
{
    /// <summary>计数器快照；Statistics 为 null 时没有可用测量，不等价于测得零。</summary>
    public sealed class PerformanceMetric
    {
        public string Name { get; }
        public string Category { get; }
        public string Unit { get; }
        public string Availability { get; }
        public PerformanceStatistics Statistics { get; }

        internal PerformanceMetric(string name, string category, string unit, string availability,
            PerformanceStatistics statistics)
        {
            Name = name;
            Category = category;
            Unit = unit;
            Availability = availability;
            Statistics = statistics;
        }
    }

    /// <summary>只读采样报告；只有显式 Export 调用写文件，默认拒绝覆盖已有报告。</summary>
    public sealed class PerformanceReport
    {
        public const string MeasurementScope = "Unity Editor Play-mode observed game-frame intervals, not GPU time or device FPS. " +
            "Profiler counters are global Editor metrics from the last completed profiler frame; they are not isolated to one camera or scene. " +
            "Editor callbacks may miss game frames. Results include Editor overhead.";
        public string Label { get; }
        public string Context { get; }
        public string StartedUtc { get; }
        public string UnityVersion { get; }
        public double WarmupSeconds { get; }
        public double RequestedSeconds { get; }
        public double ElapsedSeconds { get; }
        public int Capacity { get; }
        public int MissedFrames { get; }
        public PerformanceCaptureEndReason EndReason { get; }
        public PerformanceStatistics FrameTimeMs { get; }
        public IReadOnlyList<PerformanceMetric> Metrics { get; }

        internal PerformanceReport(string label, string context, string startedUtc, string unityVersion,
            double warmupSeconds, double requestedSeconds, double elapsedSeconds, int capacity, int missedFrames,
            PerformanceCaptureEndReason endReason, PerformanceStatistics frameTimeMs, PerformanceMetric[] metrics)
        {
            Label = label;
            Context = context;
            StartedUtc = startedUtc;
            UnityVersion = unityVersion;
            WarmupSeconds = warmupSeconds;
            RequestedSeconds = requestedSeconds;
            ElapsedSeconds = elapsedSeconds;
            Capacity = capacity;
            MissedFrames = missedFrames;
            EndReason = endReason;
            FrameTimeMs = frameTimeMs;
            Metrics = Array.AsReadOnly(metrics);
        }

        /// <summary>显式保留 null 统计值；无需引入 JSON SDK，也不受当前系统的小数格式影响。</summary>
        public string ToJson()
        {
            var output = new StringBuilder(2048);
            output.Append("{\n  \"schemaVersion\":1,\n  \"label\":").Append(JsonString(Label));
            output.Append(",\n  \"context\":").Append(JsonString(Context));
            output.Append(",\n  \"startedUtc\":").Append(JsonString(StartedUtc));
            output.Append(",\n  \"unityVersion\":").Append(JsonString(UnityVersion));
            output.Append(",\n  \"scope\":").Append(JsonString(MeasurementScope));
            output.Append(",\n  \"warmupSeconds\":").Append(Number(WarmupSeconds));
            output.Append(",\n  \"requestedSeconds\":").Append(Number(RequestedSeconds));
            output.Append(",\n  \"elapsedSeconds\":").Append(Number(ElapsedSeconds));
            output.Append(",\n  \"maxSamples\":").Append(Capacity);
            output.Append(",\n  \"missedFrames\":").Append(MissedFrames);
            output.Append(",\n  \"endReason\":").Append(JsonString(EndReason.ToString()));
            output.Append(",\n  \"percentileMethod\":\"sorted zero-based floor(N*p), clamped to N-1\"");
            output.Append(",\n  \"frameTimeMs\":");
            AppendStatistics(output, FrameTimeMs);
            output.Append(",\n  \"metrics\":[");
            for (int i = 0; i < Metrics.Count; ++i)
            {
                if (i > 0) output.Append(',');
                PerformanceMetric metric = Metrics[i];
                output.Append("\n    {\"name\":").Append(JsonString(metric.Name));
                output.Append(",\"category\":").Append(JsonString(metric.Category));
                output.Append(",\"unit\":").Append(JsonString(metric.Unit));
                output.Append(",\"availability\":").Append(JsonString(metric.Availability));
                output.Append(",\"statistics\":");
                AppendStatistics(output, metric.Statistics);
                output.Append('}');
            }
            return output.Append("\n  ]\n}\n").ToString();
        }

        /// <summary>生成不依赖脚本、外网、图片或样式库的报告；所有调用者文本都按 HTML 文本转义。</summary>
        public string ToHtml()
        {
            var output = new StringBuilder(4096);
            output.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>");
            output.Append(Html(Label)).Append(" — Performance report</title><style>");
            output.Append("body{font:16px/1.55 system-ui,sans-serif;color:#e6edf3;background:#101923;margin:0}main{max-width:1000px;margin:auto;padding:36px 22px}h1{line-height:1.2}p{overflow-wrap:anywhere}.muted{color:#a9bac9}.panel{padding:20px;background:#192736;border:1px solid #33485b;border-radius:12px;margin:20px 0}.table{overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:12px;border-bottom:1px solid #33485b;white-space:nowrap}th{color:#9cd9cd}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}code{color:#f0c791}@media print{body{color:#182432;background:white}.panel{background:white;border-color:#bbb}.muted,th{color:#314a60}td,th{border-color:#bbb}}");
            output.Append("</style></head><body><main><p class=\"muted\">JCommonUtils · Editor diagnostics</p><h1>");
            output.Append(Html(Label)).Append("</h1><p class=\"muted\">Unity ").Append(Html(UnityVersion));
            output.Append(" · Started ").Append(Html(StartedUtc)).Append("</p><div class=\"panel\"><strong>Measurement scope</strong><p>");
            output.Append(Html(MeasurementScope)).Append("</p></div><p>End reason: <code>").Append(EndReason);
            output.Append("</code> · Warmup: ").Append(DisplayNumber(WarmupSeconds)).Append(" s · Requested: ").Append(DisplayNumber(RequestedSeconds));
            output.Append(" s · Elapsed after warmup: ").Append(DisplayNumber(ElapsedSeconds)).Append(" s</p><p>Missed game frames: ");
            output.Append(MissedFrames).Append(" · Sample capacity: ").Append(Capacity).Append("</p>");
            if (EndReason != PerformanceCaptureEndReason.Completed)
                output.Append("<p><strong>Partial capture:</strong> the requested duration did not complete.</p>");
            if (FrameTimeMs == null) output.Append("<p><strong>No frame samples were captured.</strong></p>");
            output.Append("<div class=\"panel table\"><table><thead><tr><th>Metric</th><th>Availability</th><th>Samples</th><th>Mean</th><th>P50</th><th>P95</th><th>Max</th></tr></thead><tbody>");
            AppendRow(output, "Observed frame interval (ms)", FrameTimeMs == null ? "no_samples" : "available", FrameTimeMs);
            for (int i = 0; i < Metrics.Count; ++i)
            {
                PerformanceMetric metric = Metrics[i];
                AppendRow(output, metric.Name + " (" + metric.Unit + ")", metric.Availability, metric.Statistics);
            }
            output.Append("</tbody></table></div><p class=\"muted\">Unavailable counters are not zero. Percentiles use sorted floor(N × p), clamped to N − 1; P50 is the upper middle sample for even N.</p>");
            if (!string.IsNullOrEmpty(Context)) output.Append("<div class=\"panel\"><strong>Capture context</strong><pre>").Append(Html(Context)).Append("</pre></div>");
            return output.Append("</main></body></html>").ToString();
        }

        public void ExportJson(string path, bool overwrite = false) => Write(path, ToJson(), overwrite);
        public void ExportHtml(string path, bool overwrite = false) => Write(path, ToHtml(), overwrite);

        private static void Write(string path, string content, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An explicit output path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var stream = new FileStream(fullPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(content);
        }

        private static void AppendStatistics(StringBuilder output, PerformanceStatistics statistics)
        {
            if (statistics == null) { output.Append("null"); return; }
            output.Append("{\"samples\":").Append(statistics.Samples);
            output.Append(",\"mean\":").Append(Number(statistics.Mean));
            output.Append(",\"p50\":").Append(Number(statistics.P50));
            output.Append(",\"p95\":").Append(Number(statistics.P95));
            output.Append(",\"max\":").Append(Number(statistics.Max)).Append('}');
        }

        private static void AppendRow(StringBuilder output, string label, string availability, PerformanceStatistics statistics)
        {
            output.Append("<tr><td>").Append(Html(label)).Append("</td><td>").Append(Html(availability)).Append("</td>");
            if (statistics == null) { output.Append("<td colspan=\"5\">Unavailable</td></tr>"); return; }
            output.Append("<td>").Append(statistics.Samples).Append("</td><td>").Append(DisplayNumber(statistics.Mean));
            output.Append("</td><td>").Append(DisplayNumber(statistics.P50)).Append("</td><td>").Append(DisplayNumber(statistics.P95));
            output.Append("</td><td>").Append(DisplayNumber(statistics.Max)).Append("</td></tr>");
        }

        private static string Html(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        // HTML 保留便于阅读的精度；极小非零值不能显示为零，原始精度仍保留在 JSON 中。
        private static string DisplayNumber(double value)
        {
            double magnitude = Math.Abs(value);
            string format = magnitude >= 10000000 || (magnitude > 0 && magnitude < .001) ? "0.###E+0" : "0.###";
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            var output = new StringBuilder(value.Length + 2).Append('"');
            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32 || char.IsSurrogate(character))
                            output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
            }
            return output.Append('"').ToString();
        }
    }
}
