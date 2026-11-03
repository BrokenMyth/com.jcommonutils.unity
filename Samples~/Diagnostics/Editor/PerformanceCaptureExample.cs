using JCommonUtils.Editor.Diagnostics;

namespace JCommonUtils.Samples.Editor
{
    /// <summary>由自己的编辑器窗口、测试或自动化入口显式调用；不注册菜单，不自动启动采样。</summary>
    public static class PerformanceCaptureExample
    {
        private static PerformanceCapture capture;

        public static void Begin()
        {
            capture?.Dispose();
            capture = PerformanceCapture.Start(new PerformanceCaptureOptions
            {
                Label = "Fixed camera baseline",
                Context = "Record your camera pose, entity count, quality and hardware here.",
                WarmupSeconds = 2,
                DurationSeconds = 30
            });
        }

        /// <summary>采样完成后调用；路径由调用方决定，已有文件默认不覆盖。</summary>
        public static void Save(string jsonPath, string htmlPath)
        {
            if (capture == null) throw new System.InvalidOperationException("Call Begin in Play mode first.");
            PerformanceReport report = capture.CreateReport();
            report.ExportJson(jsonPath);
            report.ExportHtml(htmlPath);
        }

        public static void Cancel()
        {
            capture?.Dispose();
            capture = null;
        }
    }
}
