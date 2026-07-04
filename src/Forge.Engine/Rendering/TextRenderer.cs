using System.Numerics;

namespace Forge.Engine.Rendering;

/// <summary>Text horizontal alignment mode.</summary>
public enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// A single glyph (character) within a bitmap font, storing its position in the font texture
/// and its metrics for layout.
/// </summary>
public readonly struct BitmapGlyph
{
    /// <summary>Character codepoint.</summary>
    public readonly int Id;

    /// <summary>X position of the glyph in the font texture (pixels).</summary>
    public readonly int X;

    /// <summary>Y position of the glyph in the font texture (pixels).</summary>
    public readonly int Y;

    /// <summary>Width of the glyph in the font texture (pixels).</summary>
    public readonly int Width;

    /// <summary>Height of the glyph in the font texture (pixels).</summary>
    public readonly int Height;

    /// <summary>X offset when rendering (pixels).</summary>
    public readonly int XOffset;

    /// <summary>Y offset when rendering (pixels).</summary>
    public readonly int YOffset;

    /// <summary>Horizontal advance after this glyph (pixels).</summary>
    public readonly int XAdvance;

    public BitmapGlyph(int id, int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance)
    {
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        XOffset = xOffset;
        YOffset = yOffset;
        XAdvance = xAdvance;
    }
}

/// <summary>
/// A parsed BMFont (AngelCode bitmap font format) containing glyph metrics and kerning pairs.
/// Does not hold the texture — the texture is loaded separately and bound before rendering.
/// </summary>
public sealed class BitmapFont
{
    /// <summary>Font name from the .fnt file.</summary>
    public string Name { get; }

    /// <summary>Font size in pixels (from the .fnt file header).</summary>
    public int Size { get; }

    /// <summary>Line height in pixels.</summary>
    public int LineHeight { get; }

    /// <summary>Baseline offset from top of line (pixels).</summary>
    public int Base { get; }

    /// <summary>Width of the source texture page (pixels).</summary>
    public int ScaleW { get; }

    /// <summary>Height of the source texture page (pixels).</summary>
    public int ScaleH { get; }

    private readonly Dictionary<int, BitmapGlyph> _glyphs;
    private readonly Dictionary<long, int> _kernings;

    public BitmapFont(string name, int size, int lineHeight, int baseOffset, int scaleW, int scaleH,
                      Dictionary<int, BitmapGlyph> glyphs, Dictionary<long, int> kernings)
    {
        Name = name;
        Size = size;
        LineHeight = lineHeight;
        Base = baseOffset;
        ScaleW = scaleW;
        ScaleH = scaleH;
        _glyphs = glyphs;
        _kernings = kernings;
    }

    /// <summary>Try to get the glyph metrics for a character.</summary>
    public bool TryGetGlyph(int charId, out BitmapGlyph glyph) =>
        _glyphs.TryGetValue(charId, out glyph);

    /// <summary>Get kerning adjustment between two characters (pixels).</summary>
    public int GetKerning(int first, int second)
    {
        long key = ((long)first << 32) | (uint)second;
        return _kernings.TryGetValue(key, out int amount) ? amount : 0;
    }

    /// <summary>Number of glyphs loaded in this font.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>
    /// Parse a BMFont .fnt file (text format) into a BitmapFont instance.
    /// Supports the standard AngelCode BMFont text export format.
    /// </summary>
    public static BitmapFont ParseFnt(string fntContent)
    {
        string fontName = "unknown";
        int fontSize = 16;
        int lineHeight = 16;
        int baseOffset = 0;
        int scaleW = 256;
        int scaleH = 256;
        var glyphs = new Dictionary<int, BitmapGlyph>();
        var kernings = new Dictionary<long, int>();

        var lines = fntContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("info "))
            {
                fontName = GetStringValue(line, "face") ?? "unknown";
                fontSize = GetIntValue(line, "size", 16);
            }
            else if (line.StartsWith("common "))
            {
                lineHeight = GetIntValue(line, "lineHeight", 16);
                baseOffset = GetIntValue(line, "base", 0);
                scaleW = GetIntValue(line, "scaleW", 256);
                scaleH = GetIntValue(line, "scaleH", 256);
            }
            else if (line.StartsWith("char ") && !line.StartsWith("chars "))
            {
                int id = GetIntValue(line, "id", -1);
                if (id < 0) continue;

                var glyph = new BitmapGlyph(
                    id,
                    GetIntValue(line, "x", 0),
                    GetIntValue(line, "y", 0),
                    GetIntValue(line, "width", 0),
                    GetIntValue(line, "height", 0),
                    GetIntValue(line, "xoffset", 0),
                    GetIntValue(line, "yoffset", 0),
                    GetIntValue(line, "xadvance", 0)
                );
                glyphs[id] = glyph;
            }
            else if (line.StartsWith("kerning "))
            {
                int first = GetIntValue(line, "first", -1);
                int second = GetIntValue(line, "second", -1);
                int amount = GetIntValue(line, "amount", 0);

                if (first >= 0 && second >= 0)
                {
                    long key = ((long)first << 32) | (uint)second;
                    kernings[key] = amount;
                }
            }
        }

        return new BitmapFont(fontName, fontSize, lineHeight, baseOffset, scaleW, scaleH, glyphs, kernings);
    }

    private static int GetIntValue(string line, string key, int defaultValue)
    {
        string search = key + "=";
        int idx = line.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return defaultValue;

        int start = idx + search.Length;
        int end = start;
        while (end < line.Length && line[end] != ' ' && line[end] != '\t' && line[end] != '\r')
            end++;

        if (int.TryParse(line.AsSpan(start, end - start), out int result))
            return result;
        return defaultValue;
    }

    private static string? GetStringValue(string line, string key)
    {
        string search = key + "=\"";
        int idx = line.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0)
        {
            // Try without quotes
            search = key + "=";
            idx = line.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int s = idx + search.Length;
            int e = s;
            while (e < line.Length && line[e] != ' ' && line[e] != '\t' && line[e] != '\r')
                e++;
            return line[s..e];
        }

        int start = idx + search.Length;
        int end = line.IndexOf('"', start);
        if (end < 0) return line[start..];
        return line[start..end];
    }
}

/// <summary>
/// Internal representation of a floating text popup ("+$500", "-10 HP", etc.)
/// that rises and fades over its lifetime.
/// </summary>
internal struct FloatingText
{
    public bool Active;
    public string Text;
    public string Font;
    public float WorldX;
    public float WorldY;
    public float OriginY;
    public uint Color;
    public float RiseSpeed;
    public float Duration;
    public float Elapsed;
    public float Scale;
    public float BaseScale;
}

/// <summary>
/// Renders bitmap pixel fonts in the game world and on screen.
/// Supports BMFont format (.fnt + texture), world-space text (affected by camera),
/// screen-space text (fixed HUD), and floating number popups with rise-and-fade animation.
///
/// Floating text spec (Game Feel Bible):
///   - Rise at 60px/sec
///   - 1.5s duration
///   - Initial scale pop: 0.5 -> 1.2 -> 1.0 over 180ms (EaseOutBack)
///   - Fade starts at 60% of duration
///   - Max 8 simultaneous, object pooled
///
/// Each glyph is rendered as one instanced quad via the SpriteRenderer.
/// </summary>
public sealed class TextRenderer : IDisposable
{
    /// <summary>Max number of simultaneous floating text popups.</summary>
    private const int MaxFloatingTexts = 8;

    /// <summary>Duration of the initial scale pop effect in seconds.</summary>
    private const float ScalePopDuration = 0.18f;

    /// <summary>Fraction of total duration at which fade-out begins.</summary>
    private const float FadeStartFraction = 0.6f;

    private readonly Dictionary<string, BitmapFont> _fonts = new();
    private readonly Dictionary<string, uint> _fontTextures = new();
    private readonly FloatingText[] _floatingTexts;

    // Queued draw commands for batch rendering
    private readonly List<TextDrawCommand> _worldCommands = new();
    private readonly List<TextDrawCommand> _screenCommands = new();

    public TextRenderer()
    {
        _floatingTexts = new FloatingText[MaxFloatingTexts];
        for (int i = 0; i < MaxFloatingTexts; i++)
        {
            _floatingTexts[i] = new FloatingText { Active = false, Text = string.Empty, Font = string.Empty };
        }
    }

    /// <summary>
    /// Load a bitmap font from a parsed .fnt content string and associate a texture handle.
    /// </summary>
    /// <param name="name">Lookup name for this font (e.g., "default", "small", "title").</param>
    /// <param name="fntContent">Content of the .fnt file (text format).</param>
    /// <param name="textureHandle">OpenGL texture handle for the font atlas page.</param>
    public void LoadFont(string name, string fntContent, uint textureHandle)
    {
        var font = BitmapFont.ParseFnt(fntContent);
        _fonts[name] = font;
        _fontTextures[name] = textureHandle;
    }

    /// <summary>
    /// Load a bitmap font from a pre-parsed BitmapFont object.
    /// </summary>
    public void LoadFont(string name, BitmapFont font, uint textureHandle)
    {
        _fonts[name] = font;
        _fontTextures[name] = textureHandle;
    }

    /// <summary>
    /// Queue world-space text for rendering. Text is affected by camera pan/zoom.
    /// </summary>
    public void DrawWorld(string text, float worldX, float worldY, string font,
                          uint color = 0xFFFFFFFF, float scale = 1.0f, TextAlign align = TextAlign.Center)
    {
        _worldCommands.Add(new TextDrawCommand
        {
            Text = text,
            X = worldX,
            Y = worldY,
            Font = font,
            Color = color,
            Scale = scale,
            Align = align,
        });
    }

    /// <summary>
    /// Queue screen-space text for rendering. Text is at a fixed screen position, unaffected by camera.
    /// </summary>
    public void DrawScreen(string text, float screenX, float screenY, string font,
                           uint color = 0xFFFFFFFF, float scale = 1.0f, TextAlign align = TextAlign.Left)
    {
        _screenCommands.Add(new TextDrawCommand
        {
            Text = text,
            X = screenX,
            Y = screenY,
            Font = font,
            Color = color,
            Scale = scale,
            Align = align,
        });
    }

    /// <summary>
    /// Spawn a floating text popup that rises and fades.
    /// Uses object pooling with a max of 8 simultaneous popups.
    /// If all slots are full, the oldest popup is replaced.
    /// </summary>
    public void SpawnFloatingText(string text, float worldX, float worldY, uint color,
                                  float riseSpeed = 60f, float duration = 1.5f, float scale = 1.0f)
    {
        SpawnFloatingText(text, worldX, worldY, color, "default", riseSpeed, duration, scale);
    }

    /// <summary>
    /// Spawn a floating text popup with a specific font.
    /// </summary>
    public void SpawnFloatingText(string text, float worldX, float worldY, uint color,
                                  string font, float riseSpeed = 60f, float duration = 1.5f, float scale = 1.0f)
    {
        // Find a free slot, or the oldest active slot
        int bestSlot = 0;
        float oldestElapsed = -1f;

        for (int i = 0; i < MaxFloatingTexts; i++)
        {
            if (!_floatingTexts[i].Active)
            {
                bestSlot = i;
                break;
            }
            if (_floatingTexts[i].Elapsed > oldestElapsed)
            {
                oldestElapsed = _floatingTexts[i].Elapsed;
                bestSlot = i;
            }
        }

        _floatingTexts[bestSlot] = new FloatingText
        {
            Active = true,
            Text = text,
            Font = font,
            WorldX = worldX,
            WorldY = worldY,
            OriginY = worldY,
            Color = color,
            RiseSpeed = riseSpeed,
            Duration = duration,
            Elapsed = 0f,
            Scale = 0.5f, // Start small for the pop effect
            BaseScale = scale,
        };
    }

    /// <summary>
    /// Update floating text animations. Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        for (int i = 0; i < MaxFloatingTexts; i++)
        {
            ref var ft = ref _floatingTexts[i];
            if (!ft.Active) continue;

            ft.Elapsed += dt;

            if (ft.Elapsed >= ft.Duration)
            {
                ft.Active = false;
                continue;
            }

            // Rise upward
            ft.WorldY = ft.OriginY - ft.RiseSpeed * ft.Elapsed;

            // Scale pop effect over first 180ms: 0.5 -> 1.2 -> 1.0 using EaseOutBack
            if (ft.Elapsed < ScalePopDuration)
            {
                float popT = ft.Elapsed / ScalePopDuration;
                float eased = Easing.Evaluate(EaseType.EaseOutBack, popT);
                // Interpolate from 0.5 to 1.0 with an overshoot to ~1.2 due to EaseOutBack
                ft.Scale = ft.BaseScale * (0.5f + 0.5f * eased);
            }
            else
            {
                ft.Scale = ft.BaseScale;
            }
        }
    }

    /// <summary>
    /// Render all queued text and floating texts using the SpriteRenderer.
    /// Call during the render pass after Begin() and before End().
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;

        // Render world-space text commands
        foreach (var cmd in _worldCommands)
        {
            if (!_fonts.TryGetValue(cmd.Font, out var font)) continue;

            float screenX = cmd.X * zoom + offsetX;
            float screenY = cmd.Y * zoom + offsetY;
            float renderScale = cmd.Scale * zoom;

            RenderTextToSprites(sprites, font, cmd.Text, screenX, screenY, cmd.Color, renderScale, cmd.Align);
        }
        _worldCommands.Clear();

        // Render floating texts (world-space)
        for (int i = 0; i < MaxFloatingTexts; i++)
        {
            ref var ft = ref _floatingTexts[i];
            if (!ft.Active) continue;

            if (!_fonts.TryGetValue(ft.Font, out var font)) continue;

            float screenX = ft.WorldX * zoom + offsetX;
            float screenY = ft.WorldY * zoom + offsetY;
            float renderScale = ft.Scale * zoom;

            // Calculate alpha with fade
            float alpha = 1f;
            float fadeStart = ft.Duration * FadeStartFraction;
            if (ft.Elapsed > fadeStart)
            {
                float fadeT = (ft.Elapsed - fadeStart) / (ft.Duration - fadeStart);
                alpha = 1f - fadeT;
            }

            // Modify color alpha channel
            uint color = ApplyAlpha(ft.Color, alpha);

            RenderTextToSprites(sprites, font, ft.Text, screenX, screenY, color, renderScale, TextAlign.Center);
        }

        // Render screen-space text commands (no camera transform)
        foreach (var cmd in _screenCommands)
        {
            if (!_fonts.TryGetValue(cmd.Font, out var font)) continue;
            RenderTextToSprites(sprites, font, cmd.Text, cmd.X, cmd.Y, cmd.Color, cmd.Scale, cmd.Align);
        }
        _screenCommands.Clear();
    }

    /// <summary>
    /// Measure the pixel dimensions of a text string using the given font and scale.
    /// Returns (width, height) in pixels.
    /// </summary>
    public Vector2 MeasureText(string text, string font, float scale = 1.0f)
    {
        if (!_fonts.TryGetValue(font, out var f))
            return Vector2.Zero;

        return MeasureTextInternal(f, text, scale);
    }

    /// <summary>Check if a font with the given name has been loaded.</summary>
    public bool HasFont(string name) => _fonts.ContainsKey(name);

    /// <summary>Get the number of active floating text popups.</summary>
    public int ActiveFloatingTextCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxFloatingTexts; i++)
                if (_floatingTexts[i].Active) count++;
            return count;
        }
    }

    public void Dispose()
    {
        _fonts.Clear();
        _fontTextures.Clear();
        _worldCommands.Clear();
        _screenCommands.Clear();
    }

    // ─── Internal rendering ─────────────────────────────────────────────────

    private void RenderTextToSprites(SpriteRenderer sprites, BitmapFont font, string text,
                                     float screenX, float screenY, uint color, float scale, TextAlign align)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Measure for alignment
        var measured = MeasureTextInternal(font, text, scale);
        float startX = align switch
        {
            TextAlign.Center => screenX - measured.X * 0.5f,
            TextAlign.Right => screenX - measured.X,
            _ => screenX,
        };

        // Unpack color
        float r = ((color >> 24) & 0xFF) / 255f;
        float g = ((color >> 16) & 0xFF) / 255f;
        float b = ((color >> 8) & 0xFF) / 255f;
        float a = (color & 0xFF) / 255f;

        float cursorX = startX;
        int prevChar = -1;

        for (int i = 0; i < text.Length; i++)
        {
            int charId = text[i];
            if (!font.TryGetGlyph(charId, out var glyph))
            {
                // Skip unknown characters but advance by half the font size as fallback
                cursorX += font.Size * 0.5f * scale;
                prevChar = charId;
                continue;
            }

            // Apply kerning
            if (prevChar >= 0)
            {
                cursorX += font.GetKerning(prevChar, charId) * scale;
            }

            if (glyph.Width > 0 && glyph.Height > 0)
            {
                float glyphX = cursorX + glyph.XOffset * scale;
                float glyphY = screenY + glyph.YOffset * scale;
                float glyphW = glyph.Width * scale;
                float glyphH = glyph.Height * scale;

                // UV coordinates within the font texture
                float u0 = (float)glyph.X / font.ScaleW;
                float v0 = (float)glyph.Y / font.ScaleH;
                float u1 = (float)(glyph.X + glyph.Width) / font.ScaleW;
                float v1 = (float)(glyph.Y + glyph.Height) / font.ScaleH;

                sprites.Draw(glyphX, glyphY, glyphW, glyphH, u0, v0, u1, v1, r, g, b, a);
            }

            cursorX += glyph.XAdvance * scale;
            prevChar = charId;
        }
    }

    private static Vector2 MeasureTextInternal(BitmapFont font, string text, float scale)
    {
        float width = 0f;
        float maxHeight = font.LineHeight * scale;
        int prevChar = -1;

        for (int i = 0; i < text.Length; i++)
        {
            int charId = text[i];
            if (!font.TryGetGlyph(charId, out var glyph))
            {
                width += font.Size * 0.5f * scale;
                prevChar = charId;
                continue;
            }

            if (prevChar >= 0)
            {
                width += font.GetKerning(prevChar, charId) * scale;
            }

            width += glyph.XAdvance * scale;
            prevChar = charId;
        }

        return new Vector2(width, maxHeight);
    }

    private static uint ApplyAlpha(uint color, float alpha)
    {
        uint existingAlpha = color & 0xFF;
        uint newAlpha = (uint)System.Math.Clamp((int)(existingAlpha * alpha + 0.5f), 0, 255);
        return (color & 0xFFFFFF00) | newAlpha;
    }

    private struct TextDrawCommand
    {
        public string Text;
        public float X;
        public float Y;
        public string Font;
        public uint Color;
        public float Scale;
        public TextAlign Align;
    }
}
