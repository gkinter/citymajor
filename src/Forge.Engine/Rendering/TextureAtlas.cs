using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Loads and manages a texture atlas. Stores UV coordinates for named sprite regions.
/// </summary>
public sealed class TextureAtlas : IDisposable
{
    private readonly GL _gl;
    private uint _textureHandle;
    private int _width;
    private int _height;
    private readonly Dictionary<string, SpriteRegion> _regions = new();

    public uint TextureHandle => _textureHandle;
    public int Width => _width;
    public int Height => _height;

    public readonly record struct SpriteRegion(float U0, float V0, float U1, float V1, int PixelW, int PixelH);

    public TextureAtlas(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Create a 1x1 white placeholder texture (for testing without assets).
    /// </summary>
    public void CreatePlaceholder()
    {
        _textureHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);

        byte[] white = [255, 255, 255, 255];
        unsafe
        {
            fixed (byte* ptr = white)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        _width = 1;
        _height = 1;

        _regions["white"] = new SpriteRegion(0, 0, 1, 1, 1, 1);
    }

    /// <summary>
    /// Register a named sprite region within the atlas.
    /// </summary>
    public void AddRegion(string name, int x, int y, int w, int h)
    {
        float u0 = (float)x / _width;
        float v0 = (float)y / _height;
        float u1 = (float)(x + w) / _width;
        float v1 = (float)(y + h) / _height;
        _regions[name] = new SpriteRegion(u0, v0, u1, v1, w, h);
    }

    public bool TryGetRegion(string name, out SpriteRegion region) =>
        _regions.TryGetValue(name, out region);

    public SpriteRegion GetRegion(string name) =>
        _regions.TryGetValue(name, out var region)
            ? region
            : throw new KeyNotFoundException($"Sprite region '{name}' not found in atlas.");

    public void Bind(uint unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2D, _textureHandle);
    }

    public void Dispose()
    {
        if (_textureHandle != 0)
        {
            _gl.DeleteTexture(_textureHandle);
            _textureHandle = 0;
        }
    }
}
