using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Engine.IO;

/// <summary>
/// Loads game data definitions from JSON files. Building types, vehicle types,
/// tech tree, policies, etc. are all defined in JSON for easy modding.
/// </summary>
public sealed class JsonDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Load a single JSON file and deserialize to the given type.
    /// </summary>
    public T Load<T>(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Data file not found: {filePath}");

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize: {filePath}");
    }

    /// <summary>
    /// Load all JSON files in a directory and merge into a list.
    /// </summary>
    public List<T> LoadAll<T>(string directoryPath)
    {
        var results = new List<T>();

        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"[JsonDataLoader] Directory not found: {directoryPath}");
            return results;
        }

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                var item = Load<T>(file);
                results.Add(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JsonDataLoader] Error loading {file}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Load all JSON files in a directory, each containing an array, and flatten.
    /// </summary>
    public List<T> LoadAllFlat<T>(string directoryPath)
    {
        var results = new List<T>();

        if (!Directory.Exists(directoryPath))
            return results;

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                var items = Load<List<T>>(file);
                results.AddRange(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JsonDataLoader] Error loading {file}: {ex.Message}");
            }
        }

        return results;
    }
}
