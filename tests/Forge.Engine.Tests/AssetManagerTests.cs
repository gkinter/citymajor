using Forge.Engine.Rendering;
using Xunit;

namespace Forge.Engine.Tests;

public class AssetManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _basePath;
    private readonly string _modsPath;

    public AssetManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"forge_asset_test_{Guid.NewGuid():N}");
        _basePath = Path.Combine(_testDir, "base");
        _modsPath = Path.Combine(_testDir, "mods");
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(_modsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // =========================================================================
    // Path resolution
    // =========================================================================

    [Fact]
    public void ResolvePath_BaseFile_ReturnsBasePath()
    {
        string dataDir = Path.Combine(_basePath, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "buildings.json"), "{}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        string? resolved = mgr.ResolvePath("data/buildings.json");

        Assert.NotNull(resolved);
        Assert.True(resolved!.Contains("base"));
    }

    [Fact]
    public void ResolvePath_ModOverride_ReturnsModPath()
    {
        // Base file
        string baseData = Path.Combine(_basePath, "data");
        Directory.CreateDirectory(baseData);
        File.WriteAllText(Path.Combine(baseData, "buildings.json"), "{\"source\":\"base\"}");

        // Mod override
        string modDir = Path.Combine(_modsPath, "my_mod", "data");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "buildings.json"), "{\"source\":\"mod\"}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        string? resolved = mgr.ResolvePath("data/buildings.json");

        Assert.NotNull(resolved);
        Assert.Contains("my_mod", resolved!);
    }

    [Fact]
    public void ResolvePath_NoFile_ReturnsNull()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        Assert.Null(mgr.ResolvePath("nonexistent/file.json"));
    }

    [Fact]
    public void ResolvePath_LastModWins()
    {
        // Two mods override the same file. Directory enumeration order is OS-dependent,
        // so we use numbered dirs to ensure predictable order.
        string mod1Dir = Path.Combine(_modsPath, "01_first_mod", "data");
        string mod2Dir = Path.Combine(_modsPath, "02_second_mod", "data");
        Directory.CreateDirectory(mod1Dir);
        Directory.CreateDirectory(mod2Dir);
        File.WriteAllText(Path.Combine(mod1Dir, "config.json"), "mod1");
        File.WriteAllText(Path.Combine(mod2Dir, "config.json"), "mod2");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        string? resolved = mgr.ResolvePath("data/config.json");

        Assert.NotNull(resolved);
        // Last mod (highest alphabetical sort) should win
        Assert.Contains("02_second_mod", resolved!);
    }

    // =========================================================================
    // JSON loading
    // =========================================================================

    [Fact]
    public void LoadJson_DeserializesCorrectly()
    {
        string dataDir = Path.Combine(_basePath, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "test.json"), "{\"Name\":\"TestBuilding\",\"Cost\":1500}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var result = mgr.LoadJson<TestData>("data/test.json");

        Assert.Equal("TestBuilding", result.Name);
        Assert.Equal(1500, result.Cost);
    }

    [Fact]
    public void LoadJson_ModOverride_LoadsModVersion()
    {
        string baseData = Path.Combine(_basePath, "data");
        Directory.CreateDirectory(baseData);
        File.WriteAllText(Path.Combine(baseData, "test.json"), "{\"Name\":\"Base\",\"Cost\":100}");

        string modData = Path.Combine(_modsPath, "override_mod", "data");
        Directory.CreateDirectory(modData);
        File.WriteAllText(Path.Combine(modData, "test.json"), "{\"Name\":\"Modded\",\"Cost\":999}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var result = mgr.LoadJson<TestData>("data/test.json");

        Assert.Equal("Modded", result.Name);
        Assert.Equal(999, result.Cost);
    }

    [Fact]
    public void LoadJson_MissingFile_Throws()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        Assert.Throws<FileNotFoundException>(() => mgr.LoadJson<TestData>("nonexistent.json"));
    }

    [Fact]
    public void LoadJsonArray_LoadsAllFiles()
    {
        string dataDir = Path.Combine(_basePath, "buildings");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "house.json"), "{\"Name\":\"House\",\"Cost\":500}");
        File.WriteAllText(Path.Combine(dataDir, "shop.json"), "{\"Name\":\"Shop\",\"Cost\":1000}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var results = mgr.LoadJsonArray<TestData>("buildings");

        Assert.Equal(2, results.Length);
        Assert.Contains(results, r => r.Name == "House");
        Assert.Contains(results, r => r.Name == "Shop");
    }

    [Fact]
    public void LoadJsonArray_ModOverridesByFilename()
    {
        // Base has house.json and shop.json
        string baseDir = Path.Combine(_basePath, "buildings");
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(Path.Combine(baseDir, "house.json"), "{\"Name\":\"BaseHouse\",\"Cost\":500}");
        File.WriteAllText(Path.Combine(baseDir, "shop.json"), "{\"Name\":\"BaseShop\",\"Cost\":1000}");

        // Mod overrides house.json only
        string modDir = Path.Combine(_modsPath, "balance_mod", "buildings");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "house.json"), "{\"Name\":\"ModHouse\",\"Cost\":750}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var results = mgr.LoadJsonArray<TestData>("buildings");

        Assert.Equal(2, results.Length);
        Assert.Contains(results, r => r.Name == "ModHouse" && r.Cost == 750);
        Assert.Contains(results, r => r.Name == "BaseShop" && r.Cost == 1000);
    }

    // =========================================================================
    // Asset discovery
    // =========================================================================

    [Fact]
    public void ListAssets_FindsBaseAndModFiles()
    {
        string baseDir = Path.Combine(_basePath, "textures");
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(Path.Combine(baseDir, "terrain.png"), "");
        File.WriteAllText(Path.Combine(baseDir, "buildings.png"), "");

        string modDir = Path.Combine(_modsPath, "gfx_mod", "textures");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "custom.png"), "");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var assets = mgr.ListAssets("textures", ".png");

        Assert.Equal(3, assets.Length);
    }

    [Fact]
    public void AssetExists_ReturnsTrueForExisting()
    {
        string dataDir = Path.Combine(_basePath, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "config.json"), "{}");

        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        Assert.True(mgr.AssetExists("data/config.json"));
        Assert.False(mgr.AssetExists("data/missing.json"));
    }

    // =========================================================================
    // Texture management (headless - no GL, verify ref counting logic)
    // =========================================================================

    [Fact]
    public void LoadTexture_WithoutGL_ReturnsInvalid()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        var handle = mgr.LoadTexture("textures/test.png");
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void TextureHandle_Invalid_IsDefault()
    {
        Assert.False(TextureHandle.Invalid.IsValid);
        Assert.Equal(0u, TextureHandle.Invalid.GlHandle);
    }

    [Fact]
    public void TextureHandle_Equality()
    {
        var a = new TextureHandle(1, 64, 64, 1);
        var b = new TextureHandle(1, 64, 64, 1);
        var c = new TextureHandle(2, 64, 64, 1);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }

    // =========================================================================
    // Memory tracking
    // =========================================================================

    [Fact]
    public void MemoryReport_ReturnsNonEmpty()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        string report = mgr.MemoryReport();
        Assert.Contains("AssetManager Memory Report", report);
        Assert.Contains("Textures loaded: 0", report);
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    [Fact]
    public void UnloadAll_ResetsCounters()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        mgr.UnloadAll();
        Assert.Equal(0, mgr.LoadedTextureCount);
        Assert.Equal(0L, mgr.TotalTextureMemory);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var mgr = new AssetManager(_basePath, _modsPath, gl: null);
        mgr.Dispose();
        mgr.Dispose(); // Should not throw
    }

    // =========================================================================
    // Helper types
    // =========================================================================

    private record TestData(string Name, int Cost);
}
