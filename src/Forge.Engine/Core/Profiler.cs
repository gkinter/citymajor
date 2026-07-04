using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;

namespace Forge.Engine.Core;

/// <summary>
/// Built-in performance profiler with per-scope timing, memory tracking, counters,
/// and an ImGui overlay for real-time visualization.
///
/// Thread-safe: the simulation thread can profile scopes concurrently with the render thread.
///
/// Usage:
///   Profiler.BeginFrame();
///   using (Profiler.BeginScope("Physics")) { ... }
///   Profiler.SetCounter("Entities", entityCount);
///   Profiler.EndFrame();
///   Profiler.RenderOverlay(); // during ImGui pass
///
/// Stores 300 frames of history per scope (5 seconds at 60fps).
/// </summary>
public static class Profiler
{
    /// <summary>Number of frames of history to keep per scope.</summary>
    private const int HistorySize = 300;

    /// <summary>Number of frames shown in the frame time graph.</summary>
    private const int GraphFrames = 120;

    /// <summary>Reusable buffer for frame time graph rendering (avoids per-frame allocation).</summary>
    private static readonly float[] _graphBuffer = new float[GraphFrames];

    /// <summary>Whether profiling is active. When disabled, all calls are no-ops.</summary>
    public static bool IsEnabled { get; set; } = true;

    // ─── Scope timing ────────────────────────────────────────────────────────

    private static readonly ConcurrentDictionary<string, ScopeData> _scopes = new();
    private static readonly ConcurrentDictionary<string, long> _activeScopes = new();

    // ─── Counters ────────────────────────────────────────────────────────────

    private static readonly ConcurrentDictionary<string, long> _counters = new();

    // ─── Memory tracking ─────────────────────────────────────────────────────

    private static readonly ConcurrentDictionary<string, long> _memoryUsage = new();

    // ─── Frame timing ────────────────────────────────────────────────────────

    private static readonly double[] _frameTimes = new double[HistorySize];
    private static int _frameIndex;
    private static long _frameStartTick;
    private static long _frameCount;

    // For 1% low FPS calculation
    private static readonly double[] _recentFrameTimes = new double[120];
    private static int _recentFrameIndex;

    /// <summary>
    /// RAII-style scope that measures elapsed time between creation and disposal.
    /// Use with a `using` statement for automatic timing.
    /// </summary>
    public readonly struct ProfileScope : IDisposable
    {
        private readonly string _name;
        private readonly long _startTick;
        private readonly bool _active;

        internal ProfileScope(string name, long startTick, bool active)
        {
            _name = name;
            _startTick = startTick;
            _active = active;
        }

        public void Dispose()
        {
            if (!_active) return;
            long elapsed = Stopwatch.GetTimestamp() - _startTick;
            double ms = (double)elapsed / Stopwatch.Frequency * 1000.0;

            var data = _scopes.GetOrAdd(_name, _ => new ScopeData());
            data.RecordSample(ms);
            _activeScopes.TryRemove(_name, out _);
        }
    }

    /// <summary>
    /// Begin a profiling scope. Returns an IDisposable — use with `using` for automatic End.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProfileScope BeginScope(string name)
    {
        if (!IsEnabled)
            return new ProfileScope(name, 0, false);

        long tick = Stopwatch.GetTimestamp();
        _activeScopes[name] = tick;
        return new ProfileScope(name, tick, true);
    }

    /// <summary>Begin a manual timing sample (pair with EndSample).</summary>
    public static void BeginSample(string name)
    {
        if (!IsEnabled) return;
        _activeScopes[name] = Stopwatch.GetTimestamp();
    }

    /// <summary>End a manual timing sample started with BeginSample.</summary>
    public static void EndSample(string name)
    {
        if (!IsEnabled) return;
        if (!_activeScopes.TryRemove(name, out long startTick)) return;

        long elapsed = Stopwatch.GetTimestamp() - startTick;
        double ms = (double)elapsed / Stopwatch.Frequency * 1000.0;

        var data = _scopes.GetOrAdd(name, _ => new ScopeData());
        data.RecordSample(ms);
    }

    // ─── Memory tracking ─────────────────────────────────────────────────────

    /// <summary>Record a memory allocation for a category.</summary>
    public static void TrackAllocation(string category, long bytes)
    {
        if (!IsEnabled) return;
        _memoryUsage.AddOrUpdate(category, bytes, (_, current) => current + bytes);
    }

    /// <summary>Record a memory deallocation for a category.</summary>
    public static void TrackDeallocation(string category, long bytes)
    {
        if (!IsEnabled) return;
        _memoryUsage.AddOrUpdate(category, -bytes, (_, current) => current - bytes);
    }

    /// <summary>Get current tracked memory usage for a category in bytes.</summary>
    public static long GetMemoryUsage(string category)
    {
        return _memoryUsage.TryGetValue(category, out long bytes) ? bytes : 0;
    }

    // ─── Counters ────────────────────────────────────────────────────────────

    /// <summary>Set a counter to an absolute value (e.g., entity count, draw calls).</summary>
    public static void SetCounter(string name, long value)
    {
        if (!IsEnabled) return;
        _counters[name] = value;
    }

    /// <summary>Increment a counter by delta (default 1).</summary>
    public static void IncrementCounter(string name, long delta = 1)
    {
        if (!IsEnabled) return;
        _counters.AddOrUpdate(name, delta, (_, current) => current + delta);
    }

    /// <summary>Get the current value of a counter.</summary>
    public static long GetCounter(string name)
    {
        return _counters.TryGetValue(name, out long val) ? val : 0;
    }

    // ─── Frame markers ───────────────────────────────────────────────────────

    /// <summary>Mark the beginning of a new frame. Call at the start of the game loop.</summary>
    public static void BeginFrame()
    {
        if (!IsEnabled) return;
        _frameStartTick = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Mark the end of a frame. Records the frame time and advances the history ring buffer.
    /// </summary>
    public static void EndFrame()
    {
        if (!IsEnabled) return;

        long elapsed = Stopwatch.GetTimestamp() - _frameStartTick;
        double ms = (double)elapsed / Stopwatch.Frequency * 1000.0;

        _frameTimes[_frameIndex % HistorySize] = ms;
        _recentFrameTimes[_recentFrameIndex % _recentFrameTimes.Length] = ms;
        _frameIndex++;
        _recentFrameIndex++;
        _frameCount++;

        // Advance all scope history rings
        foreach (var kvp in _scopes)
        {
            kvp.Value.AdvanceFrame();
        }
    }

    // ─── Data access ─────────────────────────────────────────────────────────

    /// <summary>Get the average time in ms for a named scope over the last HistorySize frames.</summary>
    public static double GetAverageMs(string scope)
    {
        if (!_scopes.TryGetValue(scope, out var data)) return 0.0;
        return data.GetAverage();
    }

    /// <summary>Get the maximum time in ms for a named scope over the last HistorySize frames.</summary>
    public static double GetMaxMs(string scope)
    {
        if (!_scopes.TryGetValue(scope, out var data)) return 0.0;
        return data.GetMax();
    }

    /// <summary>Get all scope names that have been recorded.</summary>
    public static IEnumerable<string> GetScopeNames() => _scopes.Keys;

    /// <summary>Get all counter names.</summary>
    public static IEnumerable<string> GetCounterNames() => _counters.Keys;

    /// <summary>Get all memory category names.</summary>
    public static IEnumerable<string> GetMemoryCategories() => _memoryUsage.Keys;

    /// <summary>Total frames profiled.</summary>
    public static long FrameCount => _frameCount;

    /// <summary>
    /// Reset all profiling data. Useful when changing scenes or after loading.
    /// </summary>
    public static void Reset()
    {
        _scopes.Clear();
        _activeScopes.Clear();
        _counters.Clear();
        _memoryUsage.Clear();
        Array.Clear(_frameTimes);
        Array.Clear(_recentFrameTimes);
        _frameIndex = 0;
        _recentFrameIndex = 0;
        _frameCount = 0;
    }

    // ─── ImGui overlay ───────────────────────────────────────────────────────

    /// <summary>
    /// Render the profiler overlay using ImGui. Call during the UI render pass.
    /// Shows: frame time graph, per-scope timing bars, memory usage, counters, FPS.
    /// </summary>
    public static void RenderOverlay()
    {
        if (!IsEnabled) return;

        ImGui.SetNextWindowSize(new Vector2(380, 520), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Profiler", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            ImGui.End();
            return;
        }

        RenderFpsHeader();
        ImGui.Separator();
        RenderFrameTimeGraph();
        ImGui.Separator();
        RenderScopeTimings();
        ImGui.Separator();
        RenderMemoryUsage();
        ImGui.Separator();
        RenderCounters();

        ImGui.End();
    }

    private static void RenderFpsHeader()
    {
        // Calculate current FPS from recent frame times
        int count = System.Math.Min(_recentFrameIndex, _recentFrameTimes.Length);
        if (count == 0)
        {
            ImGui.Text("FPS: --");
            return;
        }

        double totalMs = 0;
        double maxMs = 0;

        // Collect frame times for 1% low calculation
        Span<double> sorted = stackalloc double[count];
        for (int i = 0; i < count; i++)
        {
            int idx = (_recentFrameIndex - count + i);
            if (idx < 0) idx += _recentFrameTimes.Length;
            idx %= _recentFrameTimes.Length;
            double ms = _recentFrameTimes[idx];
            totalMs += ms;
            sorted[i] = ms;
            if (ms > maxMs) maxMs = ms;
        }

        double avgMs = totalMs / count;
        double avgFps = avgMs > 0 ? 1000.0 / avgMs : 0;

        // 1% low: sort descending, take top 1% (at least 1 frame), average those
        sorted.Sort();
        int onePercentCount = System.Math.Max(1, count / 100);
        double onePercentTotal = 0;
        for (int i = count - onePercentCount; i < count; i++)
            onePercentTotal += sorted[i];
        double onePercentAvgMs = onePercentTotal / onePercentCount;
        double onePercentFps = onePercentAvgMs > 0 ? 1000.0 / onePercentAvgMs : 0;

        // Color code the FPS display
        var fpsColor = avgFps >= 58 ? new Vector4(0.3f, 0.9f, 0.3f, 1f) :
                       avgFps >= 30 ? new Vector4(0.9f, 0.9f, 0.3f, 1f) :
                                      new Vector4(0.9f, 0.3f, 0.3f, 1f);

        ImGui.TextColored(fpsColor, $"FPS: {avgFps:F1}");
        ImGui.SameLine();
        ImGui.Text($"| 1% Low: {onePercentFps:F1} | Frame: {avgMs:F2}ms");
    }

    private static void RenderFrameTimeGraph()
    {
        ImGui.Text("Frame Time (last 120 frames)");

        int count = System.Math.Min(_frameIndex, GraphFrames);
        if (count < 2)
        {
            ImGui.Text("  (collecting data...)");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int idx = (_frameIndex - count + i) % HistorySize;
            if (idx < 0) idx += HistorySize;
            _graphBuffer[i] = (float)_frameTimes[idx];
        }

        // Use ImGui's built-in plot for the graph (line chart)
        float maxVal = 33.3f; // Cap at ~30fps for scale
        for (int i = 0; i < count; i++)
            if (_graphBuffer[i] > maxVal) maxVal = _graphBuffer[i];

        ImGui.PlotLines("##frametimes", ref _graphBuffer[0], count, 0, null, 0f, maxVal, new Vector2(360, 60));

        // Reference lines text
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "  16.6ms (60fps) | 33.3ms (30fps)");
    }

    private static void RenderScopeTimings()
    {
        ImGui.Text("Scope Timings");

        foreach (var kvp in _scopes)
        {
            string name = kvp.Key;
            var data = kvp.Value;

            double avg = data.GetAverage();
            double max = data.GetMax();
            double last = data.GetLastSample();

            // Color code: green < 2ms, yellow < 5ms, red > 5ms
            var color = last < 2.0 ? new Vector4(0.3f, 0.9f, 0.3f, 1f) :
                        last < 5.0 ? new Vector4(0.9f, 0.9f, 0.3f, 1f) :
                                     new Vector4(0.9f, 0.3f, 0.3f, 1f);

            // Draw a proportional bar (max 16.6ms = full width)
            float fraction = (float)(last / 16.6);
            fraction = System.Math.Clamp(fraction, 0f, 1f);

            ImGui.TextColored(color, $"  {name}");
            ImGui.SameLine(160);
            ImGui.TextColored(color, $"{last:F2}ms");
            ImGui.SameLine(220);
            ImGui.Text($"avg:{avg:F2} max:{max:F2}");

            // Progress bar for visual weight
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ImGui.ColorConvertFloat4ToU32(color));
            ImGui.ProgressBar(fraction, new Vector2(360, 4), "");
            ImGui.PopStyleColor();
        }
    }

    private static void RenderMemoryUsage()
    {
        if (_memoryUsage.IsEmpty) return;

        ImGui.Text("Memory Usage");

        foreach (var kvp in _memoryUsage)
        {
            long bytes = kvp.Value;
            string display = FormatBytes(bytes);
            ImGui.Text($"  {kvp.Key}: {display}");
        }
    }

    private static void RenderCounters()
    {
        if (_counters.IsEmpty) return;

        ImGui.Text("Counters");

        foreach (var kvp in _counters)
        {
            ImGui.Text($"  {kvp.Key}: {kvp.Value:N0}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    // ─── Internal scope data storage ─────────────────────────────────────────

    /// <summary>
    /// Per-scope timing data with a ring buffer of 300 frame samples.
    /// Thread-safe via locking (profiling overhead is minimal compared to what's being measured).
    /// </summary>
    internal sealed class ScopeData
    {
        private readonly double[] _samples = new double[HistorySize];
        private int _writeIndex;
        private int _sampleCount;
        private double _currentFrameAccumulator;
        private readonly object _lock = new();

        /// <summary>Record a timing sample for the current frame. Multiple calls accumulate.</summary>
        public void RecordSample(double ms)
        {
            lock (_lock)
            {
                _currentFrameAccumulator += ms;
            }
        }

        /// <summary>Commit the accumulated samples for this frame and advance the ring buffer.</summary>
        public void AdvanceFrame()
        {
            lock (_lock)
            {
                _samples[_writeIndex % HistorySize] = _currentFrameAccumulator;
                _writeIndex++;
                _sampleCount = System.Math.Min(_sampleCount + 1, HistorySize);
                _currentFrameAccumulator = 0;
            }
        }

        public double GetAverage()
        {
            lock (_lock)
            {
                if (_sampleCount == 0) return 0;
                double total = 0;
                int count = _sampleCount;
                for (int i = 0; i < count; i++)
                {
                    int idx = (_writeIndex - count + i) % HistorySize;
                    if (idx < 0) idx += HistorySize;
                    total += _samples[idx];
                }
                return total / count;
            }
        }

        public double GetMax()
        {
            lock (_lock)
            {
                if (_sampleCount == 0) return 0;
                double max = 0;
                int count = _sampleCount;
                for (int i = 0; i < count; i++)
                {
                    int idx = (_writeIndex - count + i) % HistorySize;
                    if (idx < 0) idx += HistorySize;
                    if (_samples[idx] > max) max = _samples[idx];
                }
                return max;
            }
        }

        public double GetLastSample()
        {
            lock (_lock)
            {
                if (_sampleCount == 0) return 0;
                int idx = (_writeIndex - 1) % HistorySize;
                if (idx < 0) idx += HistorySize;
                return _samples[idx];
            }
        }

        /// <summary>Get the recorded sample count (for testing).</summary>
        public int SampleCount
        {
            get { lock (_lock) return _sampleCount; }
        }
    }

    // Expose ScopeData for testing
    internal static ScopeData? GetScopeData(string name)
    {
        return _scopes.TryGetValue(name, out var data) ? data : null;
    }
}
