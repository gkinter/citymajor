using System.Text.Json;

namespace Forge.Engine.IO;

/// <summary>
/// Loads mods from a directory. Each mod is a subdirectory containing a mod.json
/// manifest and optional data/asset overrides.
/// </summary>
public sealed class ModLoader
{
    public record ModManifest(
        string Id,
        string Name,
        string? Description,
        string? Author,
        string? Version,
        string[]? Dependencies
    );

    public record LoadedMod(
        ModManifest Manifest,
        string Path
    );

    private readonly List<LoadedMod> _loadedMods = new();
    public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

    /// <summary>
    /// Discover and load all mods from the given directory.
    /// Each subdirectory with a mod.json is treated as a mod.
    /// </summary>
    public void LoadMods(string modsDirectory)
    {
        _loadedMods.Clear();

        if (!Directory.Exists(modsDirectory))
        {
            Console.WriteLine($"[ModLoader] Mods directory not found: {modsDirectory}");
            return;
        }

        foreach (var modDir in Directory.GetDirectories(modsDirectory))
        {
            string manifestPath = Path.Combine(modDir, "mod.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                string json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ModManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                if (manifest == null || string.IsNullOrEmpty(manifest.Id))
                {
                    Console.WriteLine($"[ModLoader] Invalid manifest in {modDir}");
                    continue;
                }

                _loadedMods.Add(new LoadedMod(manifest, modDir));
                Console.WriteLine($"[ModLoader] Loaded mod: {manifest.Name} ({manifest.Id}) v{manifest.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModLoader] Error loading mod from {modDir}: {ex.Message}");
            }
        }

        // Sort by dependencies (topological sort would go here for complex dep graphs)
        Console.WriteLine($"[ModLoader] {_loadedMods.Count} mod(s) loaded.");
    }

    /// <summary>
    /// Get the file path for a mod asset, checking mod overrides before base game.
    /// </summary>
    public string? ResolveAsset(string relativePath, string basePath)
    {
        // Check mods in reverse order (last loaded wins)
        for (int i = _loadedMods.Count - 1; i >= 0; i--)
        {
            string modPath = Path.Combine(_loadedMods[i].Path, relativePath);
            if (File.Exists(modPath))
                return modPath;
        }

        // Fall back to base game
        string baseFilePath = Path.Combine(basePath, relativePath);
        return File.Exists(baseFilePath) ? baseFilePath : null;
    }
}
