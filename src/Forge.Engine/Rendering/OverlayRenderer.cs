using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Renders heatmap overlays (land value, crime, pollution, etc.) as semi-transparent
/// texture overlays on the tilemap. Each overlay is a float[] grid that gets uploaded
/// to a GPU texture and rendered with a color ramp shader.
/// </summary>
public sealed class OverlayRenderer : IDisposable
{
    private readonly GL _gl;
    private uint _overlayTexture;
    private int _texWidth;
    private int _texHeight;
    private bool _textureCreated;

    public enum OverlayType
    {
        None,
        LandValue,
        Crime,
        Pollution,
        Fire,
        Traffic,
        Desirability,
        Power,
        Water
    }

    public OverlayType ActiveOverlay { get; set; } = OverlayType.None;
    public float Opacity { get; set; } = 0.5f;

    public OverlayRenderer(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Create or resize the overlay texture to match the world grid.
    /// </summary>
    public void Init(int worldWidth, int worldHeight)
    {
        _texWidth = worldWidth;
        _texHeight = worldHeight;

        if (_textureCreated)
        {
            _gl.DeleteTexture(_overlayTexture);
        }

        _overlayTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _overlayTexture);

        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R32f,
                (uint)_texWidth, (uint)_texHeight, 0,
                PixelFormat.Red, PixelType.Float, null);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _textureCreated = true;
    }

    /// <summary>
    /// Upload overlay data (values 0.0-1.0) to the GPU texture.
    /// </summary>
    public void Upload(float[] data)
    {
        if (!_textureCreated || data.Length < _texWidth * _texHeight)
            return;

        _gl.BindTexture(TextureTarget.Texture2D, _overlayTexture);
        unsafe
        {
            fixed (float* ptr = data)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                    (uint)_texWidth, (uint)_texHeight,
                    PixelFormat.Red, PixelType.Float, ptr);
            }
        }
    }

    public void Bind(uint unit = 1)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2D, _overlayTexture);
    }

    public void Dispose()
    {
        if (_textureCreated)
        {
            _gl.DeleteTexture(_overlayTexture);
            _textureCreated = false;
        }
    }
}
