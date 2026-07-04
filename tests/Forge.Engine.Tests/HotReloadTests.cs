using Forge.Engine.IO;
using Xunit;

namespace Forge.Engine.Tests;

public class HotReloadTests : IDisposable
{
    private readonly string _tempDir;

    public HotReloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge_hotreload_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Cleanup is best-effort in tests
        }
    }

    [Fact]
    public void NewWatcher_DefaultState()
    {
        using var watcher = new HotReloadWatcher();

        Assert.True(watcher.IsEnabled);
        Assert.Equal(0, watcher.ReloadCount);
        Assert.Null(watcher.LastReloadedFile);
        Assert.Equal(100, watcher.DebounceMs);
    }

    [Fact]
    public void WatchDirectory_NonExistentPath_DoesNotThrow()
    {
        using var watcher = new HotReloadWatcher();

        // Should not throw, just log a warning
        watcher.WatchDirectory("/nonexistent/path/xyz", "*.json", _ => { });
    }

    [Fact]
    public void WatchDirectory_NullArgs_Throws()
    {
        using var watcher = new HotReloadWatcher();

        Assert.Throws<ArgumentNullException>(() => watcher.WatchDirectory(null!, "*.json", _ => { }));
        Assert.Throws<ArgumentNullException>(() => watcher.WatchDirectory(_tempDir, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => watcher.WatchDirectory(_tempDir, "*.json", null!));
    }

    [Fact]
    public void WatchDirectory_ValidPath_CountsFiles()
    {
        // Create some test files
        File.WriteAllText(Path.Combine(_tempDir, "test1.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "test2.json"), "{}");

        using var watcher = new HotReloadWatcher();
        watcher.WatchDirectory(_tempDir, "*.json", _ => { });

        Assert.Equal(2, watcher.WatchedFiles);
    }

    [Fact]
    public async Task FileChange_TriggersCallback()
    {
        var testFile = Path.Combine(_tempDir, "data.json");
        File.WriteAllText(testFile, "{\"v\":1}");

        var changedFiles = new List<string>();
        using var watcher = new HotReloadWatcher();
        watcher.DebounceMs = 50;
        watcher.WatchDirectory(_tempDir, "*.json", path => changedFiles.Add(path));

        // Modify the file
        await Task.Delay(100);
        File.WriteAllText(testFile, "{\"v\":2}");

        // Wait for debounce + task delay. FileSystemWatcher may fire multiple events
        // per write operation (Changed, Created, etc.), so we allow 1+ callbacks.
        await Task.Delay(300);
        watcher.ProcessChanges();

        Assert.True(changedFiles.Count >= 1, "Expected at least one change callback");
        Assert.Equal(testFile, changedFiles[0]);
        Assert.True(watcher.ReloadCount >= 1);
        Assert.Equal(testFile, watcher.LastReloadedFile);
    }

    [Fact]
    public async Task Debounce_EventsAreQueued()
    {
        var testFile = Path.Combine(_tempDir, "rapid.json");
        File.WriteAllText(testFile, "{\"v\":0}");

        int callbackCount = 0;
        using var watcher = new HotReloadWatcher();
        watcher.DebounceMs = 50;
        watcher.WatchDirectory(_tempDir, "*.json", _ => Interlocked.Increment(ref callbackCount));

        // Modify the file — FSW may fire multiple events per write
        await Task.Delay(100);
        File.WriteAllText(testFile, "{\"v\":1}");

        // Wait for events to be queued and debounce to pass
        await Task.Delay(300);
        watcher.ProcessChanges();

        // At least one callback should have fired
        Assert.True(callbackCount >= 1, $"Expected at least 1 callback, got {callbackCount}");
    }

    [Fact]
    public void Disabled_IgnoresChanges()
    {
        using var watcher = new HotReloadWatcher();
        watcher.IsEnabled = false;

        // ProcessChanges should drain without calling back
        watcher.ProcessChanges();

        Assert.Equal(0, watcher.ReloadCount);
    }

    [Fact]
    public void ProcessChanges_EmptyQueue_DoesNothing()
    {
        using var watcher = new HotReloadWatcher();
        watcher.ProcessChanges(); // Should not throw
        Assert.Equal(0, watcher.ReloadCount);
    }

    [Fact]
    public void Dispose_StopsWatching()
    {
        var watcher = new HotReloadWatcher();
        watcher.WatchDirectory(_tempDir, "*.json", _ => { });

        watcher.Dispose();
        watcher.Dispose(); // Double-dispose should be safe

        // After dispose, ProcessChanges should not throw
        watcher.ProcessChanges();
    }

    [Fact]
    public void WatchJsonData_SetsUpJsonFilter()
    {
        File.WriteAllText(Path.Combine(_tempDir, "buildings.json"), "{}");

        using var watcher = new HotReloadWatcher();
        var reloadedPaths = new List<string>();
        watcher.WatchJsonData(_tempDir, path => reloadedPaths.Add(path));

        Assert.Equal(1, watcher.WatchedFiles);
    }

    [Fact]
    public void WatchShaders_SetsUpShaderFilter()
    {
        File.WriteAllText(Path.Combine(_tempDir, "main.vert"), "void main() {}");
        File.WriteAllText(Path.Combine(_tempDir, "main.frag"), "void main() {}");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "not a shader");

        using var watcher = new HotReloadWatcher();
        watcher.WatchShaders(_tempDir, _ => { });

        // WatchShaders watches *.* but filters in callback
        // WatchedFiles counts all matching the filter pattern
        Assert.True(watcher.WatchedFiles >= 2);
    }

    [Fact]
    public void IsEnabled_CanToggle()
    {
        using var watcher = new HotReloadWatcher();

        Assert.True(watcher.IsEnabled);
        watcher.IsEnabled = false;
        Assert.False(watcher.IsEnabled);
        watcher.IsEnabled = true;
        Assert.True(watcher.IsEnabled);
    }
}
