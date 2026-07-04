using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Post-processing effects: bloom, vignette, CRT scanline filter, and palette swap.
/// Renders the scene to a framebuffer, then applies fullscreen quad effects.
/// </summary>
public sealed class PostProcess : IDisposable
{
    private readonly GL _gl;
    private uint _fbo;
    private uint _colorTexture;
    private uint _quadVao;
    private uint _quadVbo;
    private int _width;
    private int _height;
    private bool _initialized;

    public bool BloomEnabled { get; set; } = true;
    public float BloomIntensity { get; set; } = 0.3f;
    public bool VignetteEnabled { get; set; } = true;
    public float VignetteStrength { get; set; } = 0.4f;
    public bool CrtEnabled { get; set; } = false;
    public float CrtScanlineIntensity { get; set; } = 0.15f;
    public bool PaletteSwapEnabled { get; set; } = false;

    // Tilt-shift depth of field
    public bool TiltShiftEnabled { get; set; } = true;
    public float TiltShiftStrength { get; set; } = 0.6f;
    public float FocusBandCenter { get; set; } = 0.5f;
    public float FocusBandWidth { get; set; } = 0.4f;

    // Atmospheric fog / depth fade
    public bool FogEnabled { get; set; } = true;
    public (float R, float G, float B) FogColor { get; set; } = (0.75f, 0.78f, 0.85f);

    // Bloom color tint
    public (float R, float G, float B) BloomTint { get; set; } = (1.0f, 0.95f, 0.85f);

    public PostProcess(GL gl)
    {
        _gl = gl;
    }

    public void Init(int width, int height)
    {
        _width = width;
        _height = height;

        // Framebuffer
        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

        // Color attachment
        _colorTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _colorTexture, 0);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Fullscreen quad
        float[] quadVerts =
        [
            -1f, -1f, 0f, 0f,
             1f, -1f, 1f, 0f,
             1f,  1f, 1f, 1f,
            -1f, -1f, 0f, 0f,
             1f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f,
        ];

        _quadVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_quadVao);

        _quadVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
        unsafe
        {
            fixed (float* ptr = quadVerts)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVerts.Length * sizeof(float)),
                    ptr, BufferUsageARB.StaticDraw);
            }
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        _gl.BindVertexArray(0);
        _initialized = true;
    }

    /// <summary>Bind the offscreen framebuffer for scene rendering.</summary>
    public void BeginCapture()
    {
        if (!_initialized) return;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>Render the captured scene to the default framebuffer with post-processing.</summary>
    public void Apply(ShaderProgram postShader)
    {
        if (!_initialized) return;

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);

        postShader.Use();
        postShader.SetUniform("u_bloomEnabled", BloomEnabled ? 1 : 0);
        postShader.SetUniform("u_bloomIntensity", BloomIntensity);
        postShader.SetUniform("u_vignetteEnabled", VignetteEnabled ? 1 : 0);
        postShader.SetUniform("u_vignetteStrength", VignetteStrength);
        postShader.SetUniform("u_crtEnabled", CrtEnabled ? 1 : 0);
        postShader.SetUniform("u_crtScanlineIntensity", CrtScanlineIntensity);
        postShader.SetUniform("u_resolution", (float)_width, (float)_height);

        // Tilt-shift uniforms
        postShader.SetUniform("u_tiltShiftEnabled", TiltShiftEnabled ? 1 : 0);
        postShader.SetUniform("u_tiltShiftStrength", TiltShiftStrength);
        postShader.SetUniform("u_focusBandCenter", FocusBandCenter);
        postShader.SetUniform("u_focusBandWidth", FocusBandWidth);

        // Atmospheric fog uniforms
        postShader.SetUniform("u_fogEnabled", FogEnabled ? 1 : 0);
        postShader.SetUniform("u_fogColor", FogColor.R, FogColor.G, FogColor.B);

        // Bloom tint
        postShader.SetUniform("u_bloomTint", BloomTint.R, BloomTint.G, BloomTint.B);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
        postShader.SetUniform("u_sceneTexture", 0);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    public void Resize(int width, int height)
    {
        if (!_initialized) return;
        Dispose();
        Init(width, height);
    }

    public void Dispose()
    {
        if (_fbo != 0) _gl.DeleteFramebuffer(_fbo);
        if (_colorTexture != 0) _gl.DeleteTexture(_colorTexture);
        if (_quadVao != 0) _gl.DeleteVertexArray(_quadVao);
        if (_quadVbo != 0) _gl.DeleteBuffer(_quadVbo);
        _fbo = 0;
        _colorTexture = 0;
        _quadVao = 0;
        _quadVbo = 0;
        _initialized = false;
    }
}
