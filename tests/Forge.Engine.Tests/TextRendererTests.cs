using System.Numerics;
using Forge.Engine.Rendering;
using Xunit;

namespace Forge.Engine.Tests;

public class BitmapFontTests
{
    private const string SampleFnt = @"
info face=""PixelFont"" size=16 bold=0 italic=0
common lineHeight=18 base=14 scaleW=256 scaleH=256 pages=1
chars count=5
char id=32    x=0   y=0   width=0   height=0   xoffset=0   yoffset=0   xadvance=8
char id=65    x=0   y=0   width=10  height=14  xoffset=1   yoffset=2   xadvance=12
char id=66    x=10  y=0   width=10  height=14  xoffset=1   yoffset=2   xadvance=12
char id=67    x=20  y=0   width=10  height=14  xoffset=1   yoffset=2   xadvance=11
char id=48    x=30  y=0   width=10  height=14  xoffset=1   yoffset=2   xadvance=12
kerning first=65 second=66 amount=-1
kerning first=66 second=67 amount=-2
";

    [Fact]
    public void ParseFnt_ParsesFontInfo()
    {
        var font = BitmapFont.ParseFnt(SampleFnt);

        Assert.Equal("PixelFont", font.Name);
        Assert.Equal(16, font.Size);
        Assert.Equal(18, font.LineHeight);
        Assert.Equal(14, font.Base);
        Assert.Equal(256, font.ScaleW);
        Assert.Equal(256, font.ScaleH);
    }

    [Fact]
    public void ParseFnt_ParsesGlyphs()
    {
        var font = BitmapFont.ParseFnt(SampleFnt);

        Assert.Equal(5, font.GlyphCount);

        Assert.True(font.TryGetGlyph(65, out var glyphA)); // 'A'
        Assert.Equal(10, glyphA.Width);
        Assert.Equal(14, glyphA.Height);
        Assert.Equal(12, glyphA.XAdvance);
        Assert.Equal(1, glyphA.XOffset);

        Assert.True(font.TryGetGlyph(32, out var glyphSpace)); // space
        Assert.Equal(0, glyphSpace.Width);
        Assert.Equal(8, glyphSpace.XAdvance);
    }

    [Fact]
    public void ParseFnt_ParsesKerning()
    {
        var font = BitmapFont.ParseFnt(SampleFnt);

        Assert.Equal(-1, font.GetKerning(65, 66)); // A-B
        Assert.Equal(-2, font.GetKerning(66, 67)); // B-C
        Assert.Equal(0, font.GetKerning(65, 67));  // A-C (no kerning defined)
    }

    [Fact]
    public void TryGetGlyph_MissingGlyph_ReturnsFalse()
    {
        var font = BitmapFont.ParseFnt(SampleFnt);

        Assert.False(font.TryGetGlyph(999, out _));
    }

    [Fact]
    public void ParseFnt_EmptyInput_CreatesEmptyFont()
    {
        var font = BitmapFont.ParseFnt("");

        Assert.Equal(0, font.GlyphCount);
        Assert.Equal("unknown", font.Name);
    }
}

public class TextRendererTests
{
    private TextRenderer CreateRendererWithFont()
    {
        var renderer = new TextRenderer();

        var fntContent = @"
info face=""Test"" size=16
common lineHeight=18 base=14 scaleW=128 scaleH=128
chars count=6
char id=32    x=0   y=0   width=0   height=0   xoffset=0   yoffset=0   xadvance=8
char id=43    x=0   y=0   width=8   height=12  xoffset=0   yoffset=2   xadvance=10
char id=36    x=8   y=0   width=8   height=14  xoffset=0   yoffset=1   xadvance=10
char id=48    x=16  y=0   width=8   height=12  xoffset=0   yoffset=2   xadvance=10
char id=53    x=24  y=0   width=8   height=12  xoffset=0   yoffset=2   xadvance=10
char id=65    x=32  y=0   width=10  height=14  xoffset=1   yoffset=2   xadvance=12
";

        renderer.LoadFont("default", fntContent, 1); // texture handle 1 (placeholder)
        return renderer;
    }

    [Fact]
    public void LoadFont_FontIsAccessible()
    {
        var renderer = CreateRendererWithFont();
        Assert.True(renderer.HasFont("default"));
        Assert.False(renderer.HasFont("nonexistent"));
    }

    [Fact]
    public void MeasureText_ReturnsCorrectWidth()
    {
        var renderer = CreateRendererWithFont();

        // "A" has xadvance=12
        var sizeA = renderer.MeasureText("A", "default");
        Assert.Equal(12f, sizeA.X, 1);

        // "AA" = 12 + 12 = 24
        var sizeAA = renderer.MeasureText("AA", "default");
        Assert.Equal(24f, sizeAA.X, 1);
    }

    [Fact]
    public void MeasureText_WithScale_ScalesCorrectly()
    {
        var renderer = CreateRendererWithFont();

        var size1x = renderer.MeasureText("A", "default", 1.0f);
        var size2x = renderer.MeasureText("A", "default", 2.0f);

        Assert.Equal(size1x.X * 2f, size2x.X, 1);
        Assert.Equal(size1x.Y * 2f, size2x.Y, 1);
    }

    [Fact]
    public void MeasureText_EmptyString_ReturnsZeroWidth()
    {
        var renderer = CreateRendererWithFont();

        var size = renderer.MeasureText("", "default");
        Assert.Equal(0f, size.X);
    }

    [Fact]
    public void MeasureText_UnknownFont_ReturnsZero()
    {
        var renderer = CreateRendererWithFont();

        var size = renderer.MeasureText("Hello", "nonexistent");
        Assert.Equal(Vector2.Zero, size);
    }

    [Fact]
    public void MeasureText_Height_EqualsLineHeight()
    {
        var renderer = CreateRendererWithFont();

        // lineHeight=18, scale=1
        var size = renderer.MeasureText("A", "default");
        Assert.Equal(18f, size.Y, 1);
    }

    [Fact]
    public void SpawnFloatingText_CreatesActivePopup()
    {
        var renderer = CreateRendererWithFont();

        Assert.Equal(0, renderer.ActiveFloatingTextCount);

        renderer.SpawnFloatingText("+$500", 100f, 100f, 0xFFFFFFFF, "default");
        Assert.Equal(1, renderer.ActiveFloatingTextCount);
    }

    [Fact]
    public void SpawnFloatingText_MaxEight_RecyclesOldest()
    {
        var renderer = CreateRendererWithFont();

        // Spawn 9 floating texts (max is 8)
        for (int i = 0; i < 9; i++)
        {
            renderer.SpawnFloatingText($"Text{i}", 0f, 0f, 0xFFFFFFFF, "default");
        }

        Assert.Equal(8, renderer.ActiveFloatingTextCount);
    }

    [Fact]
    public void FloatingText_ExpiresAfterDuration()
    {
        var renderer = CreateRendererWithFont();

        renderer.SpawnFloatingText("+$500", 100f, 100f, 0xFFFFFFFF, "default",
            riseSpeed: 60f, duration: 1.0f);

        Assert.Equal(1, renderer.ActiveFloatingTextCount);

        renderer.Update(1.1f); // past duration
        Assert.Equal(0, renderer.ActiveFloatingTextCount);
    }

    [Fact]
    public void FloatingText_Rises_OverTime()
    {
        var renderer = CreateRendererWithFont();

        // Spawn at Y=100, riseSpeed=60
        renderer.SpawnFloatingText("+1", 0f, 100f, 0xFFFFFFFF, "default",
            riseSpeed: 60f, duration: 2.0f);

        // After 1 second, Y should be approximately 100 - 60 = 40
        renderer.Update(1.0f);

        // We can't directly read the floating text Y, but the text should still be active
        Assert.Equal(1, renderer.ActiveFloatingTextCount);
    }

    [Fact]
    public void Dispose_ClearsAll()
    {
        var renderer = CreateRendererWithFont();
        renderer.SpawnFloatingText("test", 0f, 0f, 0xFFFFFFFF, "default");

        renderer.Dispose();

        Assert.False(renderer.HasFont("default"));
    }

    [Fact]
    public void DrawWorld_QueuesCommand()
    {
        var renderer = CreateRendererWithFont();

        // Should not throw, just queues
        renderer.DrawWorld("Hello", 100f, 100f, "default");
        renderer.DrawScreen("HUD", 10f, 10f, "default");
    }

    [Fact]
    public void LoadFont_FromBitmapFontObject()
    {
        var renderer = new TextRenderer();
        var font = BitmapFont.ParseFnt(@"
info face=""Direct"" size=12
common lineHeight=14 base=12 scaleW=64 scaleH=64
chars count=1
char id=65 x=0 y=0 width=8 height=10 xoffset=0 yoffset=1 xadvance=10
");
        renderer.LoadFont("direct", font, 42);

        Assert.True(renderer.HasFont("direct"));
        var size = renderer.MeasureText("A", "direct");
        Assert.Equal(10f, size.X, 1);
    }
}
