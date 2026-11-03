using System;
using System.Collections;
using System.Globalization;
using System.IO;
using JCommonUtils.Editor.Diagnostics;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace JCommonUtils.Tests
{
    public sealed class PerformanceCaptureTests
    {
        private PerformanceCapture capture;

        [UnityTest]
        public IEnumerator Capture_StopsAtCapacityAndExportsUnavailableCountersSafely()
        {
            yield return new EnterPlayMode();
            bool profilerEnabled = UnityEngine.Profiling.Profiler.enabled;
            capture = PerformanceCapture.Start(new PerformanceCaptureOptions
            {
                Label = "<script>alert(\"x\")</script>", Context = "line one\n<&>\"\\\u0001",
                WarmupSeconds = 0, DurationSeconds = 30, MaxSamples = 3,
                Counters = new[] { new PerformanceCounterDefinition(ProfilerCategory.Memory, "JCommonUtils.Missing." + Guid.NewGuid().ToString("N"), "bytes") }
            });
            AssertReportRejectedWhileRunning(capture);
            double timeout = EditorApplication.timeSinceStartup + 10;
            while (capture.IsRunning && EditorApplication.timeSinceStartup < timeout) yield return null;
            Assert.That(capture.IsRunning, Is.False, "Explicit sample capacity must stop the sampler.");
            PerformanceReport report = capture.CreateReport();
            Assert.That(report.EndReason, Is.EqualTo(PerformanceCaptureEndReason.CapacityReached));
            Assert.That(report.FrameTimeMs.Samples, Is.EqualTo(3));
            Assert.That(report.Metrics[0].Availability, Is.EqualTo("unavailable"));
            Assert.That(report.Metrics[0].Statistics, Is.Null);
            Assert.That(UnityEngine.Profiling.Profiler.enabled, Is.EqualTo(profilerEnabled));
            AssertReportExportsSafely(report);
            int samples = capture.Samples;
            capture.Dispose();
            capture.Dispose();
            yield return null;
            yield return null;
            Assert.That(capture.Samples, Is.EqualTo(samples));
        }

        // EnterPlayMode 会重载域；闭包必须在重载后的同步方法里创建，避免迭代器恢复时闭包引用丢失。
        private static void AssertReportRejectedWhileRunning(PerformanceCapture runningCapture)
        {
            Assert.Throws<InvalidOperationException>(() => runningCapture.CreateReport());
        }

        private static void AssertReportExportsSafely(PerformanceReport report)
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            string path = Path.Combine(Path.GetTempPath(), "commonutils-report-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string json = report.ToJson();
                Assert.That(json, Does.Contain("\"statistics\":null"));
                Assert.That(json, Does.Contain("\\u0001"));
                var parsed = JsonUtility.FromJson<ParsedReport>(json);
                Assert.That(parsed.label, Is.EqualTo(report.Label));
                Assert.That(parsed.context, Is.EqualTo(report.Context));
                string html = report.ToHtml();
                Assert.That(html, Does.Not.Contain("<script>"));
                Assert.That(html, Does.Contain("&lt;script&gt;"));
                Assert.That(html, Does.Contain("Unavailable"));
                Assert.That(html, Does.Contain("Partial capture"));
                report.ExportJson(path);
                Assert.That(File.ReadAllText(path), Is.EqualTo(json));
                Assert.Throws<IOException>(() => report.ExportJson(path));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator Capture_ExplicitStopDuringWarmupKeepsMissingStatistics()
        {
            yield return new EnterPlayMode();
            capture = PerformanceCapture.Start(new PerformanceCaptureOptions { WarmupSeconds = 100, DurationSeconds = 1 });
            capture.Stop();
            PerformanceReport report = capture.CreateReport();
            Assert.That(report.EndReason, Is.EqualTo(PerformanceCaptureEndReason.Stopped));
            Assert.That(report.FrameTimeMs, Is.Null);
            Assert.That(report.ToJson(), Does.Contain("\"frameTimeMs\":null"));
            yield return null;
            Assert.That(capture.Samples, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Capture_AutomaticallyCompletesRequestedWindow()
        {
            yield return new EnterPlayMode();
            capture = PerformanceCapture.Start(new PerformanceCaptureOptions
                { WarmupSeconds = .1, DurationSeconds = 1, MaxSamples = 4096, Counters = Array.Empty<PerformanceCounterDefinition>() });
            double timeout = EditorApplication.timeSinceStartup + 10;
            while (capture.IsRunning && EditorApplication.timeSinceStartup < timeout)
            {
                // 只限制本测试驱动的主线程循环；无图形批处理可能超过 4096 FPS，不能让容量条件抢先触发。
                System.Threading.Thread.Sleep(10);
                yield return null;
            }
            Assert.That(capture.IsRunning, Is.False);
            Assert.That(capture.CreateReport().EndReason, Is.EqualTo(PerformanceCaptureEndReason.Completed));
            Assert.That(capture.Samples, Is.GreaterThan(0));
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            capture?.Dispose();
            capture = null;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        [Serializable]
        private sealed class ParsedReport
        {
            public string label;
            public string context;
        }
    }
}
