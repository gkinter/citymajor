using System.Collections.Concurrent;

namespace Forge.Engine.IO;

/// <summary>
/// Watches game data files and shaders for changes, automatically queuing reload
/// callbacks for main-thread dispatch. Uses FileSystemWatcher internally with
/// debouncing to handle rapid saves (editors often write files in multiple passes).
///
/// Thread safety: FileSystemWatcher fires callbacks on thread pool threads.
/// All user-provided callbacks are queued and dispatched on the main thread
/// via ProcessChanges(), ensuring game systems never receive callbacks on
/// background threads.
/// </summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentQueue<PendingChange> _pendingChanges = new();
    private readonly ConcurrentDictionary<string, long> _lastChangeTicks = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>Debounce window in milliseconds. Changes within this window after
    /// the last modification are coalesced into a single reload.</summary>
    public int DebounceMs { get; set; } = 100;

    /// <summary>Whether the hot reload system is active. When disabled, file changes are ignored.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Total number of files being monitored across all watchers.</summary>
    public int WatchedFiles
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (var watcher in _watchers)
                {
                    if (watcher.EnableRaisingEvents && Directory.Exists(watcher.Path))
                    {
                        try
                        {
                            count += Directory.GetFiles(watcher.Path, watcher.Filter, SearchOption.AllDirectories).Length;
                        }
                        catch
                        {
                            // Directory may have been removed between check and enumeration
                        }
                    }
                }
                return count;
            }
        }
    }

    /// <summary>Total number of successful reloads since creation.</summary>
    public int ReloadCount { get; private set; }

    /// <summary>The last file that was successfully reloaded, or null if none yet.</summary>
    public string? LastReloadedFile { get; private set; }

    // =========================================================================
    // Watch registration
    // =========================================================================

    /// <summary>
    /// Watch a directory for file changes matching a filter pattern.
    /// When a matching file changes, onChanged is called with the full file path
    /// on the next ProcessChanges() call (main thread).
    /// </summary>
    /// <param name="path">Directory to watch. Must exist.</param>
    /// <param name="filter">File filter pattern (e.g., "*.json", "*.glsl").</param>
    /// <param name="onChanged">Callback invoked on main thread with the changed file path.</param>
    public void WatchDirectory(string path, string filter, Action<string> onChanged)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(onChanged);

        if (!Directory.Exists(path))
        {
            Console.WriteLine($"[HotReload] Directory not found, skipping watch: {path}");
            return;
        }

        var watcher = new FileSystemWatcher(path)
        {
            Filter = filter,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        watcher.Changed += (_, e) => EnqueueChange(e.FullPath, onChanged);
        watcher.Created += (_, e) => EnqueueChange(e.FullPath, onChanged);
        watcher.Renamed += (_, e) => EnqueueChange(e.FullPath, onChanged);

        lock (_lock)
        {
            _watchers.Add(watcher);
        }

        Console.WriteLine($"[HotReload] Watching: {path}/{filter}");
    }

    /// <summary>
    /// Watch a directory of JSON data files. When any .json file changes,
    /// onDataReloaded is called with the full file path.
    /// </summary>
    /// <param name="dataPath">Path to the data directory (e.g., "base/data").</param>
    /// <param name="onDataReloaded">Callback with the changed file path.</param>
    public void WatchJsonData(string dataPath, Action<string> onDataReloaded)
    {
        WatchDirectory(dataPath, "*.json", filePath =>
        {
            Console.WriteLine($"[HotReload] Reloaded: {Path.GetRelativePath(dataPath, filePath)}");
            onDataReloaded(filePath);
        });
    }

    /// <summary>
    /// Watch a directory of shader files. When a shader file changes,
    /// onShaderReloaded is called with the full file path.
    /// Supports .glsl, .vert, .frag, and .shader extensions.
    /// </summary>
    /// <param name="shaderPath">Path to the shader directory.</param>
    /// <param name="onShaderReloaded">Callback with the changed file path.</param>
    public void WatchShaders(string shaderPath, Action<string> onShaderReloaded)
    {
        // FileSystemWatcher only supports one filter pattern, so watch all files
        // and filter by extension in the handler
        WatchDirectory(shaderPath, "*.*", filePath =>
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".glsl" or ".vert" or ".frag" or ".shader")
            {
                Console.WriteLine($"[HotReload] Shader changed: {Path.GetFileName(filePath)}");
                onShaderReloaded(filePath);
            }
        });
    }

    // =========================================================================
    // Main-thread processing
    // =========================================================================

    /// <summary>
    /// Process all pending file change callbacks on the calling thread.
    /// Call once per frame from the main/render thread to dispatch queued reloads.
    /// Changes that are still within the debounce window are re-queued.
    /// </summary>
    public void ProcessChanges()
    {
        if (!IsEnabled)
        {
            // Drain the queue silently when disabled
            while (_pendingChanges.TryDequeue(out _)) { }
            return;
        }

        int count = _pendingChanges.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_pendingChanges.TryDequeue(out var change))
                break;

            long nowTicks = Environment.TickCount64;
            long lastChange = _lastChangeTicks.GetOrAdd(change.FilePath, nowTicks);

            // If the file was modified again very recently, re-queue to wait for debounce
            if (nowTicks - lastChange < DebounceMs)
            {
                _pendingChanges.Enqueue(change);
                continue;
            }

            try
            {
                change.Callback(change.FilePath);
                ReloadCount++;
                LastReloadedFile = change.FilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotReload] Reload failed for {change.FilePath}: {ex.Message}");
            }
        }
    }

    // =========================================================================
    // Internal
    // =========================================================================

    private void EnqueueChange(string filePath, Action<string> callback)
    {
        if (!IsEnabled)
            return;

        long now = Environment.TickCount64;
        _lastChangeTicks[filePath] = now;

        // Schedule for processing after debounce window elapses
        Task.Delay(TimeSpan.FromMilliseconds(DebounceMs + 10)).ContinueWith(_ =>
        {
            _pendingChanges.Enqueue(new PendingChange(filePath, callback));
        }, TaskScheduler.Default);
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }

    // =========================================================================
    // Internal types
    // =========================================================================

    private readonly record struct PendingChange(string FilePath, Action<string> Callback);
}
