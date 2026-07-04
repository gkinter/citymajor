using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Forge.Engine.Core;

/// <summary>
/// Complete internationalization system supporting multiple languages with
/// ICU MessageFormat-style variable substitution and plural rules.
///
/// Features:
/// - Variable substitution: "Hello {name}" with named parameters
/// - Plural rules: "{count, plural, =0{No items} =1{One item} other{{count} items}}"
/// - Locale-aware number, currency, percent, and date formatting
/// - Missing key tracking for development (logs and collects unresolved keys)
/// - Fallback: returns the key itself when a translation is not found
///
/// Thread safety: NOT thread-safe. Intended for main thread use only.
/// </summary>
public sealed class Localization
{
    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _currentStrings = new();
    private readonly HashSet<string> _missingKeys = new(StringComparer.Ordinal);
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The currently active language code (e.g., "en", "de", "ja").</summary>
    public string CurrentLanguage { get; private set; } = string.Empty;

    /// <summary>All loaded language codes.</summary>
    public string[] AvailableLanguages => _languages.Keys.OrderBy(k => k).ToArray();

    /// <summary>Number of strings in the current language.</summary>
    public int StringCount => _currentStrings.Count;

    /// <summary>Number of distinct keys that were requested but not found.</summary>
    public int MissingCount => _missingKeys.Count;

    /// <summary>All keys that were requested but not found in the current language.</summary>
    public string[] MissingKeys => _missingKeys.OrderBy(k => k).ToArray();

    // =========================================================================
    // Language loading
    // =========================================================================

    /// <summary>
    /// Load a language file from disk. The JSON must have a "language" field and a
    /// "strings" object with key-value pairs.
    /// </summary>
    /// <param name="languageCode">Language code to register (e.g., "en").</param>
    /// <param name="jsonPath">Path to the JSON language file.</param>
    public void LoadLanguage(string languageCode, string jsonPath)
    {
        ArgumentNullException.ThrowIfNull(languageCode);
        ArgumentNullException.ThrowIfNull(jsonPath);

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"[Localization] Language file not found: {jsonPath}");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var langFile = JsonSerializer.Deserialize<LanguageFile>(json, JsonOptions);

        if (langFile?.Strings == null)
        {
            Console.WriteLine($"[Localization] Invalid language file format: {jsonPath}");
            return;
        }

        string code = languageCode.ToLowerInvariant();

        if (_languages.TryGetValue(code, out var existing))
        {
            // Merge new strings into existing language (allows loading multiple files per language)
            foreach (var (key, value) in langFile.Strings)
            {
                existing[key] = value;
            }
        }
        else
        {
            _languages[code] = new Dictionary<string, string>(langFile.Strings, StringComparer.Ordinal);
        }

        Console.WriteLine($"[Localization] Loaded {langFile.Strings.Count} strings for '{code}' from {jsonPath}");
    }

    /// <summary>
    /// Load a language from an in-memory dictionary. Useful for testing and runtime-generated translations.
    /// </summary>
    public void LoadLanguage(string languageCode, Dictionary<string, string> strings)
    {
        ArgumentNullException.ThrowIfNull(languageCode);
        ArgumentNullException.ThrowIfNull(strings);

        string code = languageCode.ToLowerInvariant();

        if (_languages.TryGetValue(code, out var existing))
        {
            foreach (var (key, value) in strings)
                existing[key] = value;
        }
        else
        {
            _languages[code] = new Dictionary<string, string>(strings, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Set the active language. All subsequent Get() calls use this language.
    /// </summary>
    /// <param name="languageCode">Language code (must have been loaded).</param>
    public void SetLanguage(string languageCode)
    {
        ArgumentNullException.ThrowIfNull(languageCode);

        string code = languageCode.ToLowerInvariant();

        if (!_languages.TryGetValue(code, out var strings))
        {
            Console.WriteLine($"[Localization] Language not loaded: {code}");
            return;
        }

        CurrentLanguage = code;
        _currentStrings = strings;
        _missingKeys.Clear();

        // Set culture for number/date formatting
        _culture = code switch
        {
            "en" => new CultureInfo("en-US"),
            "de" => new CultureInfo("de-DE"),
            "fr" => new CultureInfo("fr-FR"),
            "es" => new CultureInfo("es-ES"),
            "it" => new CultureInfo("it-IT"),
            "pt" => new CultureInfo("pt-BR"),
            "ja" => new CultureInfo("ja-JP"),
            "ko" => new CultureInfo("ko-KR"),
            "zh" => new CultureInfo("zh-CN"),
            "pl" => new CultureInfo("pl-PL"),
            "ru" => new CultureInfo("ru-RU"),
            "nl" => new CultureInfo("nl-NL"),
            "sv" => new CultureInfo("sv-SE"),
            "tr" => new CultureInfo("tr-TR"),
            "ar" => new CultureInfo("ar-SA"),
            _ => CultureInfo.InvariantCulture,
        };

        Console.WriteLine($"[Localization] Language set to '{code}' ({_currentStrings.Count} strings, culture={_culture.Name})");
    }

    // =========================================================================
    // String lookup
    // =========================================================================

    /// <summary>
    /// Get a translated string by key. Returns the key itself if not found (fallback behavior).
    /// </summary>
    public string Get(string key)
    {
        if (_currentStrings.TryGetValue(key, out var value))
            return value;

        _missingKeys.Add(key);
        return key;
    }

    /// <summary>
    /// Get a translated string with variable substitution and plural support.
    /// Variables are referenced as {name} in the string. Plural forms use ICU-style syntax:
    /// "{count, plural, =0{No items} =1{One item} other{{count} items}}"
    /// </summary>
    public string Get(string key, params (string name, object value)[] args)
    {
        if (!_currentStrings.TryGetValue(key, out var template))
        {
            _missingKeys.Add(key);
            return key;
        }

        if (args.Length == 0)
            return template;

        var argDict = new Dictionary<string, object>(args.Length, StringComparer.Ordinal);
        foreach (var (name, val) in args)
            argDict[name] = val;

        return FormatMessage(template, argDict);
    }

    // =========================================================================
    // ICU MessageFormat parser
    // =========================================================================

    /// <summary>
    /// Format a message template with the given arguments. Supports:
    /// - Simple substitution: {name}
    /// - Plural: {name, plural, =0{zero} =1{one} other{many}}
    /// </summary>
    public string FormatMessage(string template, Dictionary<string, object> args)
    {
        var sb = new StringBuilder(template.Length + 32);
        int i = 0;

        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                // Check for escaped literal brace {{
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    // Peek ahead: is this inside a plural block? We need context.
                    // Actually for ICU: {{ inside a plural alternative is a literal {
                    sb.Append('{');
                    i += 2;
                    continue;
                }

                // Find the matching close brace, respecting nesting
                int start = i;
                int depth = 1;
                i++;
                while (i < template.Length && depth > 0)
                {
                    if (template[i] == '{') depth++;
                    else if (template[i] == '}') depth--;
                    if (depth > 0) i++;
                }

                if (depth != 0)
                {
                    // Unmatched brace, output as-is
                    sb.Append(template[start..]);
                    break;
                }

                // Extract content between the outer braces
                string content = template[(start + 1)..i];
                i++; // skip closing }

                sb.Append(ResolveExpression(content, args));
            }
            else if (template[i] == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                sb.Append('}');
                i += 2;
            }
            else
            {
                sb.Append(template[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private string ResolveExpression(string expression, Dictionary<string, object> args)
    {
        // Check if this is a plural expression: "name, plural, ..."
        int firstComma = expression.IndexOf(',');
        if (firstComma < 0)
        {
            // Simple variable substitution
            string varName = expression.Trim();
            if (args.TryGetValue(varName, out var value))
                return FormatValue(value);
            return $"{{{expression}}}"; // Return unresolved
        }

        string argName = expression[..firstComma].Trim();
        string rest = expression[(firstComma + 1)..].Trim();

        int secondComma = rest.IndexOf(',');
        if (secondComma < 0)
        {
            // Unknown format, treat as simple variable
            if (args.TryGetValue(argName, out var val))
                return FormatValue(val);
            return $"{{{expression}}}";
        }

        string formatType = rest[..secondComma].Trim().ToLowerInvariant();
        string formatBody = rest[(secondComma + 1)..].Trim();

        if (formatType == "plural")
        {
            if (!args.TryGetValue(argName, out var countObj))
                return $"{{{expression}}}";

            long count = Convert.ToInt64(countObj);
            string selectedForm = SelectPluralForm(formatBody, count);
            // Recursively format the selected form (it may contain {name} refs)
            return FormatMessage(selectedForm, args);
        }

        // Unsupported format type, return the variable value
        if (args.TryGetValue(argName, out var fallback))
            return FormatValue(fallback);
        return $"{{{expression}}}";
    }

    /// <summary>
    /// Select the appropriate plural form from an ICU plural body.
    /// Syntax: "=0{zero text} =1{one text} other{many text}"
    /// </summary>
    public static string SelectPluralForm(string pluralBody, long count)
    {
        // Parse plural alternatives: =N{...} and other{...}
        string? exactMatch = null;
        string? otherMatch = null;

        int i = 0;
        while (i < pluralBody.Length)
        {
            // Skip whitespace
            while (i < pluralBody.Length && char.IsWhiteSpace(pluralBody[i]))
                i++;

            if (i >= pluralBody.Length)
                break;

            // Read the selector (=0, =1, other, etc.)
            int selectorStart = i;
            while (i < pluralBody.Length && pluralBody[i] != '{')
                i++;

            if (i >= pluralBody.Length)
                break;

            string selector = pluralBody[selectorStart..i].Trim();

            // Read the body between { and matching }
            i++; // skip opening {
            int depth = 1;
            int bodyStart = i;
            while (i < pluralBody.Length && depth > 0)
            {
                if (pluralBody[i] == '{') depth++;
                else if (pluralBody[i] == '}') depth--;
                if (depth > 0) i++;
            }

            string body = pluralBody[bodyStart..i];
            i++; // skip closing }

            // Match selector
            if (selector.StartsWith('='))
            {
                if (long.TryParse(selector[1..], out long n) && n == count)
                    exactMatch = body;
            }
            else if (selector.Equals("one", StringComparison.OrdinalIgnoreCase) && count == 1)
            {
                exactMatch ??= body;
            }
            else if (selector.Equals("zero", StringComparison.OrdinalIgnoreCase) && count == 0)
            {
                exactMatch ??= body;
            }
            else if (selector.Equals("other", StringComparison.OrdinalIgnoreCase))
            {
                otherMatch = body;
            }
        }

        return exactMatch ?? otherMatch ?? string.Empty;
    }

    private string FormatValue(object value)
    {
        return value switch
        {
            int i => i.ToString("N0", _culture),
            long l => l.ToString("N0", _culture),
            float f => f.ToString("N1", _culture),
            double d => d.ToString("N1", _culture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    // =========================================================================
    // Formatting helpers
    // =========================================================================

    /// <summary>Locale-aware number formatting: 1,234 (en) vs 1.234 (de).</summary>
    public string FormatNumber(long value) => value.ToString("N0", _culture);

    /// <summary>Locale-aware currency formatting.</summary>
    public string FormatCurrency(long value) => value.ToString("C0", _culture);

    /// <summary>Locale-aware percent formatting: 85.3%.</summary>
    public string FormatPercent(float value) => value.ToString("P1", _culture);

    /// <summary>Locale-aware date formatting.</summary>
    public string FormatDate(int year, int month, int day)
    {
        try
        {
            var date = new DateTime(year, month, day);
            return date.ToString("d", _culture);
        }
        catch
        {
            return $"{year}-{month:D2}-{day:D2}";
        }
    }

    // =========================================================================
    // Internal JSON model
    // =========================================================================

    private sealed class LanguageFile
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("strings")]
        public Dictionary<string, string> Strings { get; set; } = new();
    }
}
