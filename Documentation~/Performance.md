# Editor performance capture and reports

`JCommonUtils.Editor.Diagnostics` is an Editor-only assembly for Unity 2022.3. It has no dependency on the legacy CommonUtils assembly, Newtonsoft, a rendering pipeline package, a network SDK, or project assets. It does not register menus or start automatically.

## Start, wait, export, dispose

Call from your Editor window, Editor test or automation entry point on the main thread while unpaused Play mode is active:

```csharp
using JCommonUtils.Editor.Diagnostics;

var capture = PerformanceCapture.Start(new PerformanceCaptureOptions
{
    Label = "Outdoor baseline",
    Context = "Camera pose; entity count; quality; hardware; application version",
    WarmupSeconds = 2,
    DurationSeconds = 30,
    MaxSamples = 16384
});

// Return to the Editor. Keep capture in the caller's field until it finishes.
```

In the caller's existing completion/update path, after `capture.IsRunning` becomes false:

```csharp
if (!capture.IsRunning)
{
    PerformanceReport report = capture.CreateReport();
    report.ExportJson("Library/Performance/outdoor.json");
    report.ExportHtml("Library/Performance/outdoor.html");
    capture.Dispose();
    capture = null; // Do not export the same completed capture every update.
}
```

Guard that completion path with `capture != null`. Never block the Editor thread while waiting. The importable Diagnostics sample provides separate `Begin`, `Save` and `Cancel` methods with no automatic scene hooks.

`Stop()` ends a partial capture and retains its data. `Dispose()` is idempotent and also retains already collected samples for `CreateReport()`. Sampling automatically stops at duration/capacity, when Play mode exits or pauses, before domain reload, or when the Editor quits. Callbacks and native recorders are then released. In-memory results do not survive domain reload: export before leaving the domain if a durable report is required. A new instance is required for each capture.

Both exporters require an explicit path and refuse to overwrite an existing file by default. Pass `overwrite: true` only when replacement is intended. Export can fail with an I/O exception; the caller retains the report and decides how to report or retry the failure. HTML is standalone, has no scripts/network dependencies and escapes caller text. HTML shows up to three decimal places, using scientific notation for very small nonzero or large values. JSON retains round-trip numeric precision, invariant numbers and explicit `null` for missing measurements.

## What the numbers mean

- Frame interval comes from `Time.unscaledDeltaTime` once per newly observed `Time.frameCount`, via `EditorApplication.update`. This is an **Editor game-frame observation**, not a guarantee that every rendered frame was recorded, not GPU duration, and not Quest/device FPS. Editor UI and profiling overhead remain present.
- If the Editor callback skips game frames, `MissedFrames` reports the gaps. It does not interpolate or duplicate samples. Compare sample count, missed frames and end reason before comparing captures.
- Default counters: Memory / `GC Allocated In Frame` (bytes), Render / `Draw Calls Count` (count), Render / `Triangles Count` (count). Their last completed profiler-frame values are global Editor metrics, not measurements isolated to one camera or scene. They need not have exactly the same coverage as observed game-frame intervals.
- A metric not registered at `Start` is `unavailable`. A registered metric without samples is `no_samples`; one with fewer samples than observed frames is `partial`. Missing values are never reported as zero. A measured zero remains a valid sample.
- `ElapsedSeconds` is elapsed wall time after warmup until the termination callback, and may exceed the requested interval if the Editor stalled. It is not the sum of sampled intervals.
- `CapacityReached`, `Stopped`, `Disposed` and lifecycle termination reasons represent partial captures. A completed duration can still have no samples; the HTML makes that explicit.
- `PerformanceStatistics.Calculate` returns count/mean/P50/P95/max without mutating input. Empty input returns null. Non-finite input is rejected. Percentiles preserve the existing Mecha report convention: sort, use zero-based `floor(N * p)`, clamp to `N - 1`; for four values `[10,20,30,40]`, P50 is 30, P95 is 40. Mean is calculated with finite weighted accumulation, so final rounding may differ from a naive sum/divide.

Keep camera, entity count, quality, application version and sampling conditions equivalent for comparisons. Do not combine Editor results with device results or treat a diagnostic reduction in gameplay as a code optimization gain.

## Custom counters and cost

`PerformanceCaptureOptions.Counters` accepts zero to sixteen `PerformanceCounterDefinition` values. Supply category, exact registered name and raw unit. They are resolved at capture start; a later registration requires a new capture. Supported numeric data types are integer and floating-point counters; non-finite values are omitted and reflected in coverage. No automatic unit conversion occurs.

All collections are allocated before sampling with `MaxSamples` capacity; the default three counters plus frame intervals reserve about 512 KiB of double storage. Each observed frame is O(C), with C <= 16, and no intentional managed collection growth, string formatting or I/O in the sampling loop. Startup allocates managed/native recording state; report construction copies/sorts values and allocates strings. This is not a claim that the Editor or profiler itself allocates zero bytes. `MaxSamples` is bounded to 1..1,000,000 and each warmup/duration to at most one hour.

This module never sets global Profiler enabled/binary/deep-profile flags, changes camera/quality settings, edits scenes, or connects a backend. Project-specific room setup, fixed-view movement and raw CPU marker attribution remain in the consuming project.

## API references and tests

The implementation was checked against the installed Unity 2022.3.56f1 API XML and official [ProfilerRecorder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html) / [handle inventory](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable.html) APIs. Native recorders require disposal.

Package tests cover historical percentile semantics, missing/zero/non-finite values, configuration limits, real Play-mode capacity and duration termination, explicit stop/cleanup, an actually absent native counter, HTML/JSON escaping, invariant formatting and overwrite protection. Tests do not establish device performance or raw CPU attribution correctness.
