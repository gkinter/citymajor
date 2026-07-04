using System.Diagnostics;
using System.Runtime.InteropServices;
using Forge.Engine.Core;
using ImGuiNET;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Screenshot capture system with a dedicated screenshot mode for composing
/// high-quality shots. Supports quick capture (F12), super-resolution rendering
/// via FBO upscaling, and an ImGui toolbar for controlling time-of-day, season,
/// weather, and resolution multiplier overrides.
///
/// Capture pipeline:
///   1. If super-resolution: create a larger FBO, render the scene into it.
///   2. Read pixels via glReadPixels into a managed byte array.
///   3. Write BMP file using SDL_SaveBMP (no external image libraries needed).
///   4. Trigger a screen-flash effect (white overlay, 50ms hold + 150ms fade).
/// </summary>
public sealed class ScreenshotSystem : IDisposable
{
    private readonly Config _config;
    private GL? _gl;

    // Screenshot mode state
    private bool _inScreenshotMode;

    // Screenshot mode overrides
    private float _timeOfDayOverride = float.NaN; // NaN = use game time
    private int _seasonOverride = -1;              // -1 = use game season
    private int _weatherOverride = -1;             // -1 = use game weather
    private int _resolutionMultiplier = 1;         // 1x, 2x, 4x

    // Flash effect state
    private float _flashAlpha;
    private readonly Stopwatch _flashTimer = new();
    private const float FlashHoldMs = 50f;
    private const float FlashFadeMs = 150f;

    // Super-resolution FBO resources
    private uint _superFbo;
    private uint _superColorTexture;
    private int _superWidth;
    private int _superHeight;

    // File output
    private string _screenshotDirectory;
    private string _fileFormat = "bmp";
    private int _screenshotCounter;

    // Season/weather names for the UI
    private static readonly string[] SeasonNames = ["Spring", "Summer", "Autumn", "Winter"];
    private static readonly string[] WeatherNames =
        ["Clear", "Cloudy", "Rain", "Storm", "Snow", "Fog", "Heatwave", "Blizzard"];

    public bool IsInScreenshotMode => _inScreenshotMode;

    public float TimeOfDayOverride
    {
        get => _timeOfDayOverride;
        set => _timeOfDayOverride = float.IsNaN(value) ? float.NaN : System.Math.Clamp(value, 0f, 24f);
    }

    public int SeasonOverride
    {
        get => _seasonOverride;
        set => _seasonOverride = System.Math.Clamp(value, -1, 3);
    }

    public int WeatherOverride
    {
        get => _weatherOverride;
        set => _weatherOverride = System.Math.Clamp(value, -1, 7);
    }

    public int ResolutionMultiplier
    {
        get => _resolutionMultiplier;
        set => _resolutionMultiplier = value switch
        {
            1 or 2 or 4 => value,
            _ => 1,
        };
    }

    public string ScreenshotDirectory
    {
        get => _screenshotDirectory;
        set => _screenshotDirectory = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string FileFormat
    {
        get => _fileFormat;
        set => _fileFormat = value switch
        {
            "png" or "bmp" => value,
            _ => "bmp",
        };
    }

    public ScreenshotSystem(Config config, GL? gl = null)
    {
        _config = config;
        _gl = gl;

        // Default screenshot directory: "screenshots" next to the base path
        _screenshotDirectory = Path.Combine(
            Path.GetDirectoryName(config.BasePath) ?? ".",
            "screenshots");
    }

    /// <summary>
    /// Set the GL context (if not available at construction time).
    /// </summary>
    public void SetGL(GL gl)
    {
        _gl = gl;
    }

    // =========================================================================
    // Quick screenshot (F12)
    // =========================================================================

    /// <summary>
    /// Capture a screenshot of the current framebuffer and save to disk.
    /// If customPath is null, auto-generates a timestamped filename in the
    /// screenshot directory.
    /// </summary>
    public void CaptureScreenshot(string? customPath = null)
    {
        if (_gl == null) return;

        string path = customPath ?? GenerateFilePath();
        int w = _config.WindowWidth * _resolutionMultiplier;
        int h = _config.WindowHeight * _resolutionMultiplier;

        if (_resolutionMultiplier > 1)
        {
            // For super-resolution, we need an FBO already rendered into.
            // If not in screenshot mode, capture at native resolution instead.
            w = _config.WindowWidth;
            h = _config.WindowHeight;
        }

        CaptureToFile(path, w, h);
        TriggerFlash();
        Console.WriteLine($"[Screenshot] Captured: {path} ({w}x{h})");
    }

    // =========================================================================
    // Screenshot mode
    // =========================================================================

    /// <summary>Enter screenshot mode: hide HUD, show toolbar, enable free camera.</summary>
    public void EnterScreenshotMode()
    {
        _inScreenshotMode = true;
        Console.WriteLine("[Screenshot] Entered screenshot mode");
    }

    /// <summary>Exit screenshot mode: restore HUD, hide toolbar, restore overrides.</summary>
    public void ExitScreenshotMode()
    {
        _inScreenshotMode = false;
        _timeOfDayOverride = float.NaN;
        _seasonOverride = -1;
        _weatherOverride = -1;
        _resolutionMultiplier = 1;
        DestroySuperFbo();
        Console.WriteLine("[Screenshot] Exited screenshot mode");
    }

    // =========================================================================
    // Capture implementation
    // =========================================================================

    /// <summary>
    /// Read the current framebuffer (or super-resolution FBO) and write a BMP file.
    /// For super-resolution captures (2x, 4x), call EnsureSuperFbo() and render
    /// into it before calling this method.
    /// </summary>
    public void CaptureToFile(string path, int width, int height)
    {
        if (_gl == null) return;

        EnsureDirectoryExists(path);

        byte[] pixels = CaptureToMemory(width, height);
        WriteBmp(path, pixels, width, height);
    }

    /// <summary>
    /// Capture the current framebuffer contents as a raw RGBA byte array.
    /// The returned array is bottom-to-top (OpenGL convention) then flipped
    /// to top-to-bottom for image file compatibility.
    /// </summary>
    public byte[] CaptureToMemory(int width, int height)
    {
        if (_gl == null)
            return Array.Empty<byte>();

        if (width <= 0 || height <= 0)
            return Array.Empty<byte>();

        int stride = width * 4; // RGBA
        byte[] pixels = new byte[stride * height];

        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                _gl.ReadPixels(0, 0, (uint)width, (uint)height,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        // Flip vertically (OpenGL reads bottom-to-top, BMP/PNG expect top-to-bottom)
        FlipVertical(pixels, width, height);

        return pixels;
    }

    // =========================================================================
    // Super-resolution FBO management
    // =========================================================================

    /// <summary>
    /// Ensure the super-resolution FBO exists at the requested dimensions.
    /// Call before rendering the scene at higher resolution.
    /// </summary>
    public void EnsureSuperFbo(int width, int height)
    {
        if (_gl == null) return;
        if (_superFbo != 0 && _superWidth == width && _superHeight == height) return;

        DestroySuperFbo();

        _superFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _superFbo);

        _superColorTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _superColorTexture);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _superColorTexture, 0);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _superWidth = width;
        _superHeight = height;
    }

    /// <summary>Bind the super-resolution FBO for rendering.</summary>
    public void BindSuperFbo()
    {
        if (_gl == null || _superFbo == 0) return;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _superFbo);
        _gl.Viewport(0, 0, (uint)_superWidth, (uint)_superHeight);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>Unbind the super-resolution FBO (return to default framebuffer).</summary>
    public void UnbindSuperFbo()
    {
        if (_gl == null) return;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)_config.WindowWidth, (uint)_config.WindowHeight);
    }

    private void DestroySuperFbo()
    {
        if (_gl == null) return;
        if (_superFbo != 0) _gl.DeleteFramebuffer(_superFbo);
        if (_superColorTexture != 0) _gl.DeleteTexture(_superColorTexture);
        _superFbo = 0;
        _superColorTexture = 0;
        _superWidth = 0;
        _superHeight = 0;
    }

    // =========================================================================
    // Flash effect
    // =========================================================================

    /// <summary>Trigger the white-flash capture feedback effect.</summary>
    private void TriggerFlash()
    {
        _flashAlpha = 0.8f;
        _flashTimer.Restart();
        Console.WriteLine("[Screenshot] *shutter click*"); // placeholder for audio
    }

    /// <summary>
    /// Update the flash effect. Call once per frame.
    /// Returns the current flash overlay alpha (0 = no flash, 0.8 = peak).
    /// </summary>
    public float UpdateFlash()
    {
        if (_flashAlpha <= 0f) return 0f;

        float elapsedMs = (float)_flashTimer.Elapsed.TotalMilliseconds;

        if (elapsedMs < FlashHoldMs)
        {
            // Hold phase: full brightness
            return _flashAlpha;
        }

        float fadeProgress = (elapsedMs - FlashHoldMs) / FlashFadeMs;
        if (fadeProgress >= 1f)
        {
            _flashAlpha = 0f;
            _flashTimer.Stop();
            return 0f;
        }

        // Linear fade
        return _flashAlpha * (1f - fadeProgress);
    }

    // =========================================================================
    // ImGui UI (toolbar at bottom of screen in screenshot mode)
    // =========================================================================

    /// <summary>
    /// Render the screenshot mode toolbar via ImGui. Only visible when in screenshot mode.
    /// </summary>
    public void RenderUI()
    {
        if (!_inScreenshotMode) return;

        var io = ImGui.GetIO();
        float toolbarHeight = 60f;
        float toolbarWidth = io.DisplaySize.X;

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, io.DisplaySize.Y - toolbarHeight));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(toolbarWidth, toolbarHeight));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("ScreenshotToolbar", flags))
        {
            // Time of Day slider
            float tod = float.IsNaN(_timeOfDayOverride) ? 12f : _timeOfDayOverride;
            ImGui.SetNextItemWidth(150f);
            if (ImGui.SliderFloat("Time", ref tod, 0f, 24f, "%.1f h"))
            {
                _timeOfDayOverride = tod;
            }
            ImGui.SameLine();

            // Season dropdown
            int seasonIdx = _seasonOverride + 1; // -1 becomes 0 ("Game")
            string[] seasonOptions = ["Game Default", .. SeasonNames];
            ImGui.SetNextItemWidth(120f);
            if (ImGui.Combo("Season", ref seasonIdx, seasonOptions, seasonOptions.Length))
            {
                _seasonOverride = seasonIdx - 1;
            }
            ImGui.SameLine();

            // Weather dropdown
            int weatherIdx = _weatherOverride + 1;
            string[] weatherOptions = ["Game Default", .. WeatherNames];
            ImGui.SetNextItemWidth(120f);
            if (ImGui.Combo("Weather", ref weatherIdx, weatherOptions, weatherOptions.Length))
            {
                _weatherOverride = weatherIdx - 1;
            }
            ImGui.SameLine();

            // Resolution multiplier
            int resIdx = _resolutionMultiplier switch { 2 => 1, 4 => 2, _ => 0 };
            string[] resOptions = ["1x", "2x", "4x"];
            ImGui.SetNextItemWidth(60f);
            if (ImGui.Combo("Res", ref resIdx, resOptions, resOptions.Length))
            {
                _resolutionMultiplier = resIdx switch { 1 => 2, 2 => 4, _ => 1 };
            }
            ImGui.SameLine();

            // Capture button
            if (ImGui.Button("Capture"))
            {
                int w = _config.WindowWidth * _resolutionMultiplier;
                int h = _config.WindowHeight * _resolutionMultiplier;
                string path = GenerateFilePath();
                CaptureToFile(path, w, h);
                TriggerFlash();
                Console.WriteLine($"[Screenshot] Mode capture: {path} ({w}x{h})");
            }
            ImGui.SameLine();

            // Exit button
            if (ImGui.Button("Exit Mode"))
            {
                ExitScreenshotMode();
            }
        }
        ImGui.End();
    }

    // =========================================================================
    // File path generation
    // =========================================================================

    /// <summary>
    /// Generate a timestamped file path for a screenshot.
    /// Format: screenshots/screenshot_2024-01-15_143052_001.bmp
    /// </summary>
    public string GenerateFilePath()
    {
        _screenshotCounter++;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string filename = $"screenshot_{timestamp}_{_screenshotCounter:D3}.{_fileFormat}";
        return Path.Combine(_screenshotDirectory, filename);
    }

    // =========================================================================
    // BMP writer (no external dependencies)
    // =========================================================================

    /// <summary>
    /// Write RGBA pixel data as a 24-bit BMP file.
    /// BMP format: 14-byte file header + 40-byte DIB header + pixel data (BGR, padded rows).
    /// </summary>
    private static void WriteBmp(string path, byte[] rgbaPixels, int width, int height)
    {
        // BMP stores pixels as BGR, bottom-to-top, with rows padded to 4-byte boundaries
        int rowStride = ((width * 3 + 3) / 4) * 4; // padded row size
        int pixelDataSize = rowStride * height;
        int fileSize = 14 + 40 + pixelDataSize; // file header + DIB header + pixels

        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);

        // BMP File Header (14 bytes)
        w.Write((byte)'B');
        w.Write((byte)'M');
        w.Write(fileSize);
        w.Write((ushort)0); // reserved1
        w.Write((ushort)0); // reserved2
        w.Write(14 + 40);   // pixel data offset

        // DIB Header (BITMAPINFOHEADER, 40 bytes)
        w.Write(40);         // header size
        w.Write(width);
        w.Write(height);     // positive = bottom-up (already flipped in CaptureToMemory, so re-flip)
        w.Write((ushort)1);  // color planes
        w.Write((ushort)24); // bits per pixel
        w.Write(0);          // compression (BI_RGB)
        w.Write(pixelDataSize);
        w.Write(2835);       // horizontal resolution (72 DPI)
        w.Write(2835);       // vertical resolution
        w.Write(0);          // palette colors
        w.Write(0);          // important colors

        // Pixel data: convert RGBA (top-to-bottom) to BGR (bottom-to-top)
        byte[] rowBuffer = new byte[rowStride];
        for (int y = height - 1; y >= 0; y--)
        {
            int srcOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int si = srcOffset + x * 4;
                int di = x * 3;
                rowBuffer[di + 0] = rgbaPixels[si + 2]; // B
                rowBuffer[di + 1] = rgbaPixels[si + 1]; // G
                rowBuffer[di + 2] = rgbaPixels[si + 0]; // R
            }
            // Zero-fill padding bytes
            for (int p = width * 3; p < rowStride; p++)
                rowBuffer[p] = 0;

            w.Write(rowBuffer);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void FlipVertical(byte[] pixels, int width, int height)
    {
        int stride = width * 4;
        byte[] tempRow = new byte[stride];

        for (int y = 0; y < height / 2; y++)
        {
            int topOffset = y * stride;
            int bottomOffset = (height - 1 - y) * stride;

            // Swap rows
            global::System.Buffer.BlockCopy(pixels, topOffset, tempRow, 0, stride);
            global::System.Buffer.BlockCopy(pixels, bottomOffset, pixels, topOffset, stride);
            global::System.Buffer.BlockCopy(tempRow, 0, pixels, bottomOffset, stride);
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public void Dispose()
    {
        DestroySuperFbo();
    }
}
