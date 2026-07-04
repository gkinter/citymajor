using Forge.Engine.Core;
using Forge.Engine.Rendering;
using Xunit;

namespace Forge.Engine.Tests;

public class ScreenshotSystemTests : IDisposable
{
    private readonly string _testDir;
    private readonly Config _config;
    private readonly ScreenshotSystem _system;

    public ScreenshotSystemTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"forge_screenshot_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _config = new Config
        {
            WindowWidth = 320,
            WindowHeight = 240,
            BasePath = _testDir,
        };

        _system = new ScreenshotSystem(_config);
        _system.ScreenshotDirectory = _testDir;
    }

    public void Dispose()
    {
        _system.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // =========================================================================
    // File path generation
    // =========================================================================

    [Fact]
    public void GenerateFilePath_IncludesDirectory()
    {
        string path = _system.GenerateFilePath();
        Assert.StartsWith(_testDir, path);
    }

    [Fact]
    public void GenerateFilePath_IncludesExtension()
    {
        _system.FileFormat = "bmp";
        string path = _system.GenerateFilePath();
        Assert.EndsWith(".bmp", path);
    }

    [Fact]
    public void GenerateFilePath_SequentialCounters()
    {
        string path1 = _system.GenerateFilePath();
        string path2 = _system.GenerateFilePath();
        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void GenerateFilePath_ContainsScreenshotPrefix()
    {
        string path = _system.GenerateFilePath();
        string filename = Path.GetFileName(path);
        Assert.StartsWith("screenshot_", filename);
    }

    // =========================================================================
    // Resolution multiplier validation
    // =========================================================================

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void ResolutionMultiplier_ValidValues_Accepted(int input, int expected)
    {
        _system.ResolutionMultiplier = input;
        Assert.Equal(expected, _system.ResolutionMultiplier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(-1)]
    [InlineData(8)]
    public void ResolutionMultiplier_InvalidValues_DefaultToOne(int input)
    {
        _system.ResolutionMultiplier = input;
        Assert.Equal(1, _system.ResolutionMultiplier);
    }

    // =========================================================================
    // Screenshot mode
    // =========================================================================

    [Fact]
    public void EnterScreenshotMode_SetsFlag()
    {
        Assert.False(_system.IsInScreenshotMode);
        _system.EnterScreenshotMode();
        Assert.True(_system.IsInScreenshotMode);
    }

    [Fact]
    public void ExitScreenshotMode_ClearsFlag()
    {
        _system.EnterScreenshotMode();
        _system.ExitScreenshotMode();
        Assert.False(_system.IsInScreenshotMode);
    }

    [Fact]
    public void ExitScreenshotMode_ResetsOverrides()
    {
        _system.EnterScreenshotMode();
        _system.TimeOfDayOverride = 18f;
        _system.SeasonOverride = 2;
        _system.WeatherOverride = 3;
        _system.ResolutionMultiplier = 4;

        _system.ExitScreenshotMode();

        Assert.True(float.IsNaN(_system.TimeOfDayOverride));
        Assert.Equal(-1, _system.SeasonOverride);
        Assert.Equal(-1, _system.WeatherOverride);
        Assert.Equal(1, _system.ResolutionMultiplier);
    }

    // =========================================================================
    // Override validation
    // =========================================================================

    [Fact]
    public void TimeOfDayOverride_ClampsTo0_24()
    {
        _system.TimeOfDayOverride = 25f;
        Assert.Equal(24f, _system.TimeOfDayOverride);

        _system.TimeOfDayOverride = -1f;
        Assert.Equal(0f, _system.TimeOfDayOverride);
    }

    [Fact]
    public void TimeOfDayOverride_NaN_Preserved()
    {
        _system.TimeOfDayOverride = float.NaN;
        Assert.True(float.IsNaN(_system.TimeOfDayOverride));
    }

    [Fact]
    public void SeasonOverride_ClampsToValid()
    {
        _system.SeasonOverride = 5;
        Assert.Equal(3, _system.SeasonOverride);

        _system.SeasonOverride = -2;
        Assert.Equal(-1, _system.SeasonOverride);
    }

    [Fact]
    public void WeatherOverride_ClampsToValid()
    {
        _system.WeatherOverride = 10;
        Assert.Equal(7, _system.WeatherOverride);

        _system.WeatherOverride = -5;
        Assert.Equal(-1, _system.WeatherOverride);
    }

    // =========================================================================
    // File format validation
    // =========================================================================

    [Fact]
    public void FileFormat_BmpAccepted()
    {
        _system.FileFormat = "bmp";
        Assert.Equal("bmp", _system.FileFormat);
    }

    [Fact]
    public void FileFormat_PngAccepted()
    {
        _system.FileFormat = "png";
        Assert.Equal("png", _system.FileFormat);
    }

    [Fact]
    public void FileFormat_InvalidDefaultsToBmp()
    {
        _system.FileFormat = "jpg";
        Assert.Equal("bmp", _system.FileFormat);
    }

    // =========================================================================
    // CaptureToMemory (headless, no GL)
    // =========================================================================

    [Fact]
    public void CaptureToMemory_WithoutGL_ReturnsEmpty()
    {
        byte[] result = _system.CaptureToMemory(320, 240);
        Assert.Empty(result);
    }

    [Fact]
    public void CaptureToMemory_ZeroDimensions_ReturnsEmpty()
    {
        byte[] result = _system.CaptureToMemory(0, 100);
        Assert.Empty(result);
    }

    [Fact]
    public void CaptureToMemory_NegativeDimensions_ReturnsEmpty()
    {
        byte[] result = _system.CaptureToMemory(-1, 100);
        Assert.Empty(result);
    }

    // =========================================================================
    // Flash effect
    // =========================================================================

    [Fact]
    public void UpdateFlash_InitiallyZero()
    {
        float alpha = _system.UpdateFlash();
        Assert.Equal(0f, alpha);
    }

    // =========================================================================
    // Directory validation
    // =========================================================================

    [Fact]
    public void ScreenshotDirectory_CanBeSet()
    {
        string dir = Path.Combine(_testDir, "custom_shots");
        _system.ScreenshotDirectory = dir;
        Assert.Equal(dir, _system.ScreenshotDirectory);
    }

    [Fact]
    public void ScreenshotDirectory_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => _system.ScreenshotDirectory = null!);
    }

    // =========================================================================
    // Dispose safety
    // =========================================================================

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var system = new ScreenshotSystem(_config);
        system.Dispose();
        system.Dispose(); // Should not throw
    }
}
