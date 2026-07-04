using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Manages loading, caching, and reference counting for all game assets.
/// Supports mod overlay resolution: files in mods/ override base/ counterparts.
///
/// Texture loading uses SDL2's SDL_LoadBMP for BMP files. For PNG/JPG support,
/// raw RGBA byte arrays can be uploaded directly via LoadTextureFromPixels().
/// Shader sources are loaded as text files and compiled via ShaderProgram.
/// JSON data is loaded with mod-aware path resolution.
///
/// Thread safety: NOT thread-safe. All calls must be from the main (render) thread,
/// which is standard for OpenGL resource management.
/// </summary>
public sealed class AssetManager : IDisposable
{
    private readonly string _basePath;
    private readonly string _modsPath;
    private readonly GL? _gl;
    private bool _disposed;

    // Texture cache: relative path -> entry
    private readonly Dictionary<string, TextureEntry> _textures = new();

    // Shader cache: "vert|frag" -> ShaderProgram
    private readonly Dictionary<string, ShaderProgram> _shaders = new();

    // JSON options matching JsonDataLoader conventions
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Memory tracking
    private long _totalTextureMemory;
    private int _loadedTextureCount;

    public long TotalTextureMemory => _totalTextureMemory;
    public int LoadedTextureCount => _loadedTextureCount;

    /// <summary>
    /// Create an asset manager rooted at the given base path.
    /// </summary>
    /// <param name="basePath">Root directory for base game assets (e.g., "base/").</param>
    /// <param name="modsPath">Root directory for mod overlays (e.g., "mods/").</param>
    /// <param name="gl">OpenGL context for texture/shader loading. Null for headless/test mode.</param>
    public AssetManager(string basePath, string modsPath, GL? gl = null)
    {
        _basePath = basePath;
        _modsPath = modsPath;
        _gl = gl;
    }

    // =========================================================================
    // Path resolution (mod overlay support)
    // =========================================================================

    /// <summary>
    /// Resolve a relative asset path, checking mod directories first (in reverse order,
    /// so last-loaded mod wins), then falling back to base path.
    /// </summary>
    /// <param name="relativePath">Path relative to the base/mod root (e.g., "data/buildings.json").</param>
    /// <returns>Absolute file path, or null if the file doesn't exist anywhere.</returns>
    public string? ResolvePath(string relativePath)
    {
        // Check mods directory: each subdirectory is a mod
        if (Directory.Exists(_modsPath))
        {
            var modDirs = Directory.GetDirectories(_modsPath);
            // Reverse iteration: last mod wins (highest priority)
            for (int i = modDirs.Length - 1; i >= 0; i--)
            {
                string candidate = Path.Combine(modDirs[i], relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Fall back to base
        string basefile = Path.Combine(_basePath, relativePath);
        return File.Exists(basefile) ? basefile : null;
    }

    // =========================================================================
    // Texture management
    // =========================================================================

    /// <summary>
    /// Load a BMP texture from a relative path. Returns a cached handle if already loaded,
    /// incrementing the reference count. Uses SDL2's built-in SDL_LoadBMP.
    /// For PNG/JPG, use LoadTextureFromPixels() with pre-decoded pixel data.
    /// </summary>
    public TextureHandle LoadTexture(string path)
    {
        if (_gl == null)
            return TextureHandle.Invalid;

        if (_textures.TryGetValue(path, out var existing))
        {
            var updated = existing with { RefCount = existing.RefCount + 1 };
            _textures[path] = updated;
            return new TextureHandle(updated.GlHandle, updated.Width, updated.Height, updated.RefCount);
        }

        string? resolvedPath = ResolvePath(path);
        if (resolvedPath == null)
        {
            Console.WriteLine($"[AssetManager] Texture not found: {path}");
            return TextureHandle.Invalid;
        }

        // Load pixel data via SDL2's built-in BMP loader
        IntPtr surface = SDL2.SDL.SDL_LoadBMP(resolvedPath);
        if (surface == IntPtr.Zero)
        {
            Console.WriteLine($"[AssetManager] Failed to load texture: {resolvedPath} ({SDL2.SDL.SDL_GetError()})");
            return TextureHandle.Invalid;
        }

        // Convert surface to RGBA32
        IntPtr convertedSurface = SDL2.SDL.SDL_ConvertSurfaceFormat(surface, SDL2.SDL.SDL_PIXELFORMAT_ABGR8888, 0);
        SDL2.SDL.SDL_FreeSurface(surface);

        if (convertedSurface == IntPtr.Zero)
        {
            Console.WriteLine($"[AssetManager] Failed to convert surface: {resolvedPath}");
            return TextureHandle.Invalid;
        }

        unsafe
        {
            var surfaceData = (SDL2.SDL.SDL_Surface*)convertedSurface;
            int w = surfaceData->w;
            int h = surfaceData->h;

            uint glHandle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, glHandle);

            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)w, (uint)h, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, (void*)surfaceData->pixels);

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            SDL2.SDL.SDL_FreeSurface(convertedSurface);

            long memBytes = (long)w * h * 4;
            _totalTextureMemory += memBytes;
            _loadedTextureCount++;

            var entry = new TextureEntry(glHandle, w, h, 1, memBytes);
            _textures[path] = entry;

            Console.WriteLine($"[AssetManager] Loaded texture: {path} ({w}x{h})");
            return new TextureHandle(glHandle, w, h, 1);
        }
    }

    /// <summary>
    /// Load a texture from raw RGBA pixel data (for use with custom image decoders).
    /// </summary>
    /// <param name="name">Cache key for the texture.</param>
    /// <param name="pixels">RGBA8 pixel data, row-major, length = width * height * 4.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    public TextureHandle LoadTextureFromPixels(string name, byte[] pixels, int width, int height)
    {
        if (_gl == null)
            return TextureHandle.Invalid;

        if (_textures.TryGetValue(name, out var existing))
        {
            var updated = existing with { RefCount = existing.RefCount + 1 };
            _textures[name] = updated;
            return new TextureHandle(updated.GlHandle, updated.Width, updated.Height, updated.RefCount);
        }

        uint glHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, glHandle);

        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)width, (uint)height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        long memBytes = (long)width * height * 4;
        _totalTextureMemory += memBytes;
        _loadedTextureCount++;

        _textures[name] = new TextureEntry(glHandle, width, height, 1, memBytes);
        return new TextureHandle(glHandle, width, height, 1);
    }

    /// <summary>
    /// Load multiple BMP textures as a GL_TEXTURE_2D_ARRAY. All images must have the same dimensions.
    /// Returns a handle referencing the array texture, with Width/Height of each layer.
    /// </summary>
    public TextureHandle LoadTextureArray(string[] paths)
    {
        if (_gl == null || paths.Length == 0)
            return TextureHandle.Invalid;

        string cacheKey = string.Join("|", paths);
        if (_textures.TryGetValue(cacheKey, out var existing))
        {
            var updated = existing with { RefCount = existing.RefCount + 1 };
            _textures[cacheKey] = updated;
            return new TextureHandle(updated.GlHandle, updated.Width, updated.Height, updated.RefCount);
        }

        // Load first image to get dimensions
        string? firstResolved = ResolvePath(paths[0]);
        if (firstResolved == null)
        {
            Console.WriteLine($"[AssetManager] Texture array first file not found: {paths[0]}");
            return TextureHandle.Invalid;
        }

        IntPtr firstSurface = SDL2.SDL.SDL_LoadBMP(firstResolved);
        if (firstSurface == IntPtr.Zero)
            return TextureHandle.Invalid;

        IntPtr firstConverted = SDL2.SDL.SDL_ConvertSurfaceFormat(firstSurface, SDL2.SDL.SDL_PIXELFORMAT_ABGR8888, 0);
        SDL2.SDL.SDL_FreeSurface(firstSurface);
        if (firstConverted == IntPtr.Zero)
            return TextureHandle.Invalid;

        unsafe
        {
            var surfData = (SDL2.SDL.SDL_Surface*)firstConverted;
            int w = surfData->w;
            int h = surfData->h;

            uint glHandle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2DArray, glHandle);

            // Allocate storage for all layers
            _gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba8,
                (uint)w, (uint)h, (uint)paths.Length, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);

            // Upload first layer
            _gl.TexSubImage3D(TextureTarget.Texture2DArray, 0,
                0, 0, 0, (uint)w, (uint)h, 1,
                PixelFormat.Rgba, PixelType.UnsignedByte, (void*)surfData->pixels);

            SDL2.SDL.SDL_FreeSurface(firstConverted);

            // Upload remaining layers
            for (int i = 1; i < paths.Length; i++)
            {
                string? resolved = ResolvePath(paths[i]);
                if (resolved == null)
                {
                    Console.WriteLine($"[AssetManager] Texture array layer not found: {paths[i]}");
                    continue;
                }

                IntPtr layerSurf = SDL2.SDL.SDL_LoadBMP(resolved);
                if (layerSurf == IntPtr.Zero) continue;
                IntPtr layerConverted = SDL2.SDL.SDL_ConvertSurfaceFormat(layerSurf, SDL2.SDL.SDL_PIXELFORMAT_ABGR8888, 0);
                SDL2.SDL.SDL_FreeSurface(layerSurf);
                if (layerConverted == IntPtr.Zero) continue;

                var layerData = (SDL2.SDL.SDL_Surface*)layerConverted;
                _gl.TexSubImage3D(TextureTarget.Texture2DArray, 0,
                    0, 0, i, (uint)w, (uint)h, 1,
                    PixelFormat.Rgba, PixelType.UnsignedByte, (void*)layerData->pixels);
                SDL2.SDL.SDL_FreeSurface(layerConverted);
            }

            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            long memBytes = (long)w * h * 4 * paths.Length;
            _totalTextureMemory += memBytes;
            _loadedTextureCount++;

            var entry = new TextureEntry(glHandle, w, h, 1, memBytes);
            _textures[cacheKey] = entry;

            return new TextureHandle(glHandle, w, h, 1);
        }
    }

    /// <summary>
    /// Decrement texture reference count. Frees the GL resource when refcount hits zero.
    /// </summary>
    public void UnloadTexture(TextureHandle handle)
    {
        if (_gl == null || handle.GlHandle == 0)
            return;

        // Find the entry by GL handle
        string? keyToRemove = null;
        string? keyToUpdate = null;
        TextureEntry updatedEntry = default;

        foreach (var (key, entry) in _textures)
        {
            if (entry.GlHandle == handle.GlHandle)
            {
                int newRef = entry.RefCount - 1;
                if (newRef <= 0)
                {
                    _gl.DeleteTexture(entry.GlHandle);
                    _totalTextureMemory -= entry.MemoryBytes;
                    _loadedTextureCount--;
                    keyToRemove = key;
                }
                else
                {
                    keyToUpdate = key;
                    updatedEntry = entry with { RefCount = newRef };
                }
                break;
            }
        }

        if (keyToRemove != null)
            _textures.Remove(keyToRemove);
        else if (keyToUpdate != null)
            _textures[keyToUpdate] = updatedEntry;
    }

    // =========================================================================
    // JSON data loading (mod-aware)
    // =========================================================================

    /// <summary>
    /// Load and deserialize a JSON file. Checks mods/ first, then base/.
    /// </summary>
    public T LoadJson<T>(string path)
    {
        string? resolved = ResolvePath(path);
        if (resolved == null)
            throw new FileNotFoundException($"JSON asset not found: {path}");

        string json = File.ReadAllText(resolved);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize: {resolved}");
    }

    /// <summary>
    /// Load all JSON files from a directory and deserialize each as T.
    /// Mod files overlay base files by filename.
    /// </summary>
    public T[] LoadJsonArray<T>(string directory)
    {
        var results = new List<T>();

        // Collect all unique filenames from base + mods
        var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Base files first
        string baseDir = Path.Combine(_basePath, directory);
        if (Directory.Exists(baseDir))
        {
            foreach (var file in Directory.GetFiles(baseDir, "*.json"))
            {
                string name = Path.GetFileName(file);
                fileMap[name] = file;
            }
        }

        // Mod files override by filename
        if (Directory.Exists(_modsPath))
        {
            foreach (var modDir in Directory.GetDirectories(_modsPath))
            {
                string modSubDir = Path.Combine(modDir, directory);
                if (!Directory.Exists(modSubDir)) continue;

                foreach (var file in Directory.GetFiles(modSubDir, "*.json"))
                {
                    string name = Path.GetFileName(file);
                    fileMap[name] = file; // Override base
                }
            }
        }

        foreach (var (_, filePath) in fileMap)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var item = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (item != null)
                    results.Add(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetManager] Error loading JSON {filePath}: {ex.Message}");
            }
        }

        return results.ToArray();
    }

    // =========================================================================
    // Shader loading
    // =========================================================================

    /// <summary>
    /// Load and compile a shader program from vertex + fragment source files.
    /// Cached by path pair.
    /// </summary>
    public ShaderProgram? LoadShader(string vertPath, string fragPath)
    {
        if (_gl == null) return null;

        string cacheKey = $"{vertPath}|{fragPath}";
        if (_shaders.TryGetValue(cacheKey, out var cached))
            return cached;

        string? resolvedVert = ResolvePath(vertPath);
        string? resolvedFrag = ResolvePath(fragPath);

        if (resolvedVert == null)
        {
            Console.WriteLine($"[AssetManager] Vertex shader not found: {vertPath}");
            return null;
        }
        if (resolvedFrag == null)
        {
            Console.WriteLine($"[AssetManager] Fragment shader not found: {fragPath}");
            return null;
        }

        string vertSource = File.ReadAllText(resolvedVert);
        string fragSource = File.ReadAllText(resolvedFrag);

        var program = new ShaderProgram(_gl, vertSource, fragSource);
        _shaders[cacheKey] = program;
        Console.WriteLine($"[AssetManager] Compiled shader: {vertPath} + {fragPath}");
        return program;
    }

    // =========================================================================
    // Font loading
    // =========================================================================

    /// <summary>
    /// Load a TrueType font for ImGui rendering. Returns the ImGui font pointer.
    /// </summary>
    public unsafe IntPtr LoadFont(string path, int size)
    {
        string? resolved = ResolvePath(path);
        if (resolved == null)
        {
            Console.WriteLine($"[AssetManager] Font not found: {path}");
            return IntPtr.Zero;
        }

        var io = ImGuiNET.ImGui.GetIO();
        var font = io.Fonts.AddFontFromFileTTF(resolved, size);
        Console.WriteLine($"[AssetManager] Loaded font: {path} @ {size}px");
        return font.NativePtr != null ? (IntPtr)font.NativePtr : IntPtr.Zero;
    }

    // =========================================================================
    // Asset discovery
    // =========================================================================

    /// <summary>
    /// List all assets matching an extension in a directory, combining base + mods.
    /// Returns relative paths (suitable for passing back to Load methods).
    /// </summary>
    public string[] ListAssets(string directory, string extension)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string baseDir = Path.Combine(_basePath, directory);
        if (Directory.Exists(baseDir))
        {
            foreach (var file in Directory.GetFiles(baseDir, $"*{extension}"))
            {
                string relative = Path.Combine(directory, Path.GetFileName(file));
                found.Add(relative);
            }
        }

        if (Directory.Exists(_modsPath))
        {
            foreach (var modDir in Directory.GetDirectories(_modsPath))
            {
                string modSubDir = Path.Combine(modDir, directory);
                if (!Directory.Exists(modSubDir)) continue;

                foreach (var file in Directory.GetFiles(modSubDir, $"*{extension}"))
                {
                    string relative = Path.Combine(directory, Path.GetFileName(file));
                    found.Add(relative);
                }
            }
        }

        return found.ToArray();
    }

    /// <summary>Check if an asset exists (in base or any mod).</summary>
    public bool AssetExists(string path) => ResolvePath(path) != null;

    // =========================================================================
    // Memory reporting
    // =========================================================================

    /// <summary>
    /// Generate a human-readable memory usage report.
    /// </summary>
    public string MemoryReport()
    {
        double mb = _totalTextureMemory / (1024.0 * 1024.0);
        var sb = new System.Text.StringBuilder(256);
        sb.AppendLine($"AssetManager Memory Report:");
        sb.AppendLine($"  Textures loaded: {_loadedTextureCount}");
        sb.AppendLine($"  Texture memory: {mb:F2} MB ({_totalTextureMemory:N0} bytes)");
        sb.AppendLine($"  Shaders cached: {_shaders.Count}");
        sb.AppendLine($"  Cache entries: {_textures.Count}");
        return sb.ToString();
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    /// <summary>Unload all assets and free GPU resources.</summary>
    public void UnloadAll()
    {
        if (_gl != null)
        {
            foreach (var entry in _textures.Values)
            {
                _gl.DeleteTexture(entry.GlHandle);
            }

            foreach (var shader in _shaders.Values)
            {
                shader.Dispose();
            }
        }

        _textures.Clear();
        _shaders.Clear();
        _totalTextureMemory = 0;
        _loadedTextureCount = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnloadAll();
            _disposed = true;
        }
    }

    // =========================================================================
    // Internal types
    // =========================================================================

    private record struct TextureEntry(uint GlHandle, int Width, int Height, int RefCount, long MemoryBytes);
}

/// <summary>
/// Lightweight handle to a loaded texture. Immutable value type for safe passing.
/// </summary>
public readonly struct TextureHandle : IEquatable<TextureHandle>
{
    public readonly uint GlHandle;
    public readonly int Width;
    public readonly int Height;
    public readonly int RefCount;

    public static readonly TextureHandle Invalid = default;

    public TextureHandle(uint glHandle, int width, int height, int refCount)
    {
        GlHandle = glHandle;
        Width = width;
        Height = height;
        RefCount = refCount;
    }

    public bool IsValid => GlHandle != 0;

    public bool Equals(TextureHandle other) => GlHandle == other.GlHandle;
    public override bool Equals(object? obj) => obj is TextureHandle other && Equals(other);
    public override int GetHashCode() => GlHandle.GetHashCode();
    public static bool operator ==(TextureHandle left, TextureHandle right) => left.Equals(right);
    public static bool operator !=(TextureHandle left, TextureHandle right) => !left.Equals(right);
}
