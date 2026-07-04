using System.Text.Json;
using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class LocalizationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Localization _loc;

    public LocalizationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge_loc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loc = new Localization();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    private string WriteLanguageFile(string code, Dictionary<string, string> strings)
    {
        var file = new { language = code, strings };
        string json = JsonSerializer.Serialize(file);
        string path = Path.Combine(_tempDir, $"{code}.json");
        File.WriteAllText(path, json);
        return path;
    }

    // =========================================================================
    // Basic lookup
    // =========================================================================

    [Fact]
    public void Get_LoadedKey_ReturnsTranslation()
    {
        var strings = new Dictionary<string, string>
        {
            ["menu.new_game"] = "New Game",
            ["menu.quit"] = "Quit",
        };
        string path = WriteLanguageFile("en", strings);

        _loc.LoadLanguage("en", path);
        _loc.SetLanguage("en");

        Assert.Equal("New Game", _loc.Get("menu.new_game"));
        Assert.Equal("Quit", _loc.Get("menu.quit"));
    }

    [Fact]
    public void Get_MissingKey_ReturnsKeyItself()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["a"] = "A" });
        _loc.SetLanguage("en");

        string result = _loc.Get("nonexistent.key");

        Assert.Equal("nonexistent.key", result);
    }

    [Fact]
    public void MissingKeys_TrackedForDevelopment()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["a"] = "A" });
        _loc.SetLanguage("en");

        _loc.Get("missing.one");
        _loc.Get("missing.two");
        _loc.Get("missing.one"); // Duplicate should not increase count

        Assert.Equal(2, _loc.MissingCount);
        Assert.Contains("missing.one", _loc.MissingKeys);
        Assert.Contains("missing.two", _loc.MissingKeys);
    }

    [Fact]
    public void MissingKeys_ClearedOnLanguageSwitch()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["a"] = "A" });
        _loc.LoadLanguage("de", new Dictionary<string, string> { ["a"] = "A-de" });

        _loc.SetLanguage("en");
        _loc.Get("missing.key");
        Assert.Equal(1, _loc.MissingCount);

        _loc.SetLanguage("de");
        Assert.Equal(0, _loc.MissingCount);
    }

    // =========================================================================
    // Variable substitution
    // =========================================================================

    [Fact]
    public void Get_SimpleVariable_Substituted()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["hud.population"] = "Population: {count}",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("hud.population", ("count", "1500"));

        Assert.Equal("Population: 1500", result);
    }

    [Fact]
    public void Get_MultipleVariables_AllSubstituted()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["coords"] = "Position: ({x}, {y})",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("coords", ("x", "10"), ("y", "20"));

        Assert.Equal("Position: (10, 20)", result);
    }

    [Fact]
    public void Get_UnknownVariable_LeftAsIs()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["msg"] = "Hello {name}",
        });
        _loc.SetLanguage("en");

        // No args provided
        string result = _loc.Get("msg");

        Assert.Equal("Hello {name}", result);
    }

    // =========================================================================
    // Plural rules
    // =========================================================================

    [Fact]
    public void Plural_Zero()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["items"] = "{count, plural, =0{No items} =1{One item} other{{count} items}}",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("items", ("count", 0));

        Assert.Equal("No items", result);
    }

    [Fact]
    public void Plural_One()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["items"] = "{count, plural, =0{No items} =1{One item} other{{count} items}}",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("items", ("count", 1));

        Assert.Equal("One item", result);
    }

    [Fact]
    public void Plural_Other()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["items"] = "{count, plural, =0{No items} =1{One item} other{{count} items}}",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("items", ("count", 5));

        Assert.Equal("5 items", result);
    }

    [Fact]
    public void Plural_LargeNumber()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["items"] = "{count, plural, =0{No items} =1{One item} other{{count} items}}",
        });
        _loc.SetLanguage("en");

        string result = _loc.Get("items", ("count", 42));

        Assert.Equal("42 items", result);
    }

    [Fact]
    public void Plural_OnlyOtherForm()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["things"] = "{n, plural, other{{n} things}}",
        });
        _loc.SetLanguage("en");

        Assert.Equal("0 things", _loc.Get("things", ("n", 0)));
        Assert.Equal("1 things", _loc.Get("things", ("n", 1)));
        Assert.Equal("99 things", _loc.Get("things", ("n", 99)));
    }

    // =========================================================================
    // Language switching
    // =========================================================================

    [Fact]
    public void SetLanguage_SwitchesBetweenLanguages()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["greeting"] = "Hello",
        });
        _loc.LoadLanguage("de", new Dictionary<string, string>
        {
            ["greeting"] = "Hallo",
        });

        _loc.SetLanguage("en");
        Assert.Equal("Hello", _loc.Get("greeting"));
        Assert.Equal("en", _loc.CurrentLanguage);

        _loc.SetLanguage("de");
        Assert.Equal("Hallo", _loc.Get("greeting"));
        Assert.Equal("de", _loc.CurrentLanguage);
    }

    [Fact]
    public void AvailableLanguages_ListsAllLoaded()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["a"] = "A" });
        _loc.LoadLanguage("de", new Dictionary<string, string> { ["a"] = "A" });
        _loc.LoadLanguage("ja", new Dictionary<string, string> { ["a"] = "A" });

        var langs = _loc.AvailableLanguages;

        Assert.Equal(3, langs.Length);
        Assert.Contains("de", langs);
        Assert.Contains("en", langs);
        Assert.Contains("ja", langs);
    }

    [Fact]
    public void StringCount_ReflectsCurrentLanguage()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>
        {
            ["a"] = "A", ["b"] = "B", ["c"] = "C",
        });
        _loc.LoadLanguage("de", new Dictionary<string, string>
        {
            ["a"] = "A-de",
        });

        _loc.SetLanguage("en");
        Assert.Equal(3, _loc.StringCount);

        _loc.SetLanguage("de");
        Assert.Equal(1, _loc.StringCount);
    }

    // =========================================================================
    // File loading
    // =========================================================================

    [Fact]
    public void LoadLanguage_FromJsonFile()
    {
        var strings = new Dictionary<string, string>
        {
            ["menu.new"] = "New",
            ["menu.load"] = "Load",
        };
        string path = WriteLanguageFile("en", strings);

        _loc.LoadLanguage("en", path);
        _loc.SetLanguage("en");

        Assert.Equal("New", _loc.Get("menu.new"));
        Assert.Equal("Load", _loc.Get("menu.load"));
        Assert.Equal(2, _loc.StringCount);
    }

    [Fact]
    public void LoadLanguage_NonExistentFile_DoesNotThrow()
    {
        _loc.LoadLanguage("en", "/nonexistent/path/en.json");
        // Should not throw, just log a warning
    }

    [Fact]
    public void LoadLanguage_MultipleFiles_MergesStrings()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["a"] = "A" });
        _loc.LoadLanguage("en", new Dictionary<string, string> { ["b"] = "B" });

        _loc.SetLanguage("en");

        Assert.Equal("A", _loc.Get("a"));
        Assert.Equal("B", _loc.Get("b"));
        Assert.Equal(2, _loc.StringCount);
    }

    // =========================================================================
    // Formatting helpers
    // =========================================================================

    [Fact]
    public void FormatNumber_EnglishLocale()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>());
        _loc.SetLanguage("en");

        Assert.Equal("1,234", _loc.FormatNumber(1234));
        Assert.Equal("1,000,000", _loc.FormatNumber(1000000));
        Assert.Equal("0", _loc.FormatNumber(0));
    }

    [Fact]
    public void FormatNumber_GermanLocale()
    {
        _loc.LoadLanguage("de", new Dictionary<string, string>());
        _loc.SetLanguage("de");

        Assert.Equal("1.234", _loc.FormatNumber(1234));
    }

    [Fact]
    public void FormatCurrency_EnglishLocale()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>());
        _loc.SetLanguage("en");

        string result = _loc.FormatCurrency(1234);
        // Should contain 1,234 and a currency symbol
        Assert.Contains("1,234", result);
    }

    [Fact]
    public void FormatPercent_EnglishLocale()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>());
        _loc.SetLanguage("en");

        string result = _loc.FormatPercent(0.853f);
        Assert.Contains("85", result);
        Assert.Contains("%", result);
    }

    [Fact]
    public void FormatDate_EnglishLocale()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>());
        _loc.SetLanguage("en");

        string result = _loc.FormatDate(2024, 3, 15);
        Assert.Contains("2024", result);
        Assert.Contains("3", result);
        Assert.Contains("15", result);
    }

    [Fact]
    public void FormatDate_InvalidDate_ReturnsFallback()
    {
        _loc.LoadLanguage("en", new Dictionary<string, string>());
        _loc.SetLanguage("en");

        string result = _loc.FormatDate(2024, 13, 45);
        Assert.Equal("2024-13-45", result);
    }

    // =========================================================================
    // Edge cases
    // =========================================================================

    [Fact]
    public void SetLanguage_NotLoaded_DoesNotThrow()
    {
        _loc.SetLanguage("xx");
        // Should not throw, just log
        Assert.Equal(string.Empty, _loc.CurrentLanguage);
    }

    [Fact]
    public void Get_NoLanguageSet_ReturnsFallback()
    {
        // No language loaded or set
        string result = _loc.Get("some.key");
        Assert.Equal("some.key", result);
    }

    [Fact]
    public void SelectPluralForm_ExactMatchPriority()
    {
        // =1 should take priority over other for count=1
        string result = Localization.SelectPluralForm("=1{exactly one} other{many}", 1);
        Assert.Equal("exactly one", result);
    }

    [Fact]
    public void SelectPluralForm_FallsBackToOther()
    {
        string result = Localization.SelectPluralForm("=0{none} other{some}", 99);
        Assert.Equal("some", result);
    }

    [Fact]
    public void SelectPluralForm_NoMatch_ReturnsEmpty()
    {
        string result = Localization.SelectPluralForm("=0{none} =1{one}", 5);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void LoadLanguage_CaseInsensitiveCode()
    {
        _loc.LoadLanguage("EN", new Dictionary<string, string> { ["a"] = "A" });
        _loc.SetLanguage("en");

        Assert.Equal("A", _loc.Get("a"));
    }

    [Fact]
    public void LoadEnglishFile_HasOver100Strings()
    {
        // Verify the actual en.json file has 100+ strings
        string enPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "base", "data", "localization", "en.json");

        // Also try relative from project root
        string altPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "base", "data", "localization", "en.json"));

        string? usePath = File.Exists(enPath) ? enPath : (File.Exists(altPath) ? altPath : null);

        if (usePath != null)
        {
            _loc.LoadLanguage("en", usePath);
            _loc.SetLanguage("en");
            Assert.True(_loc.StringCount >= 100, $"Expected 100+ strings, got {_loc.StringCount}");
        }
        // Skip if file not found in CI/different working directory
    }
}
