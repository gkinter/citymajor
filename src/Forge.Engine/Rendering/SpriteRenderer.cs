using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// GPU-instanced sprite batch renderer using GL_TEXTURE_2D_ARRAY for sprite atlases.
/// Designed for rendering 10,000+ instances at 60fps with a single draw call per atlas.
///
/// Instance data per sprite (16 floats):
///   - Position: x, y (screen-space)
///   - Size: w, h (pixels after zoom)
///   - Sprite index: atlasLayer (float, for texture array), unused
///   - UV coordinates: u0, v0, u1, v1
///   - Tint color: r, g, b, a
///   - Sort key: y-coordinate (for depth ordering)
///
/// Usage pattern:
///   1. Begin() - reset batch
///   2. Draw() / DrawSorted() - queue sprites (vehicles, citizens, buildings per atlas)
///   3. End() - sort by Y if needed, upload to GPU, single instanced draw call
///
/// Separate SpriteRenderer instances for buildings, vehicles, and citizens
/// allow independent atlases and draw calls without texture switching.
/// </summary>
public sealed class SpriteRenderer : IDisposable
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;           // Quad vertices (static)
    private uint _instanceVbo;   // Per-instance data (dynamic, streamed each frame)
    private ShaderProgram? _shader;

    private const int MaxSprites = 65536;
    private int _spriteCount;

    // Instance layout: x, y, w, h, layer, _pad, u0, v0, u1, v1, r, g, b, a, sortY, _pad2 = 16 floats
    private const int FloatsPerSprite = 16;

    private readonly float[] _instanceData;

    // Sorting support: indices array for Y-sorting without moving float data
    private readonly int[] _sortIndices;
    private readonly float[] _sortKeys;
    private readonly float[] _sortedInstanceData;
    private bool _needsSort;

    // Stats
    private int _drawCallsLastFrame;
    private int _instancesLastFrame;

    public int DrawCallsLastFrame => _drawCallsLastFrame;
    public int InstancesLastFrame => _instancesLastFrame;
    public int SpriteCount => _spriteCount;

    public SpriteRenderer(GL gl)
    {
        _gl = gl;
        _instanceData = new float[MaxSprites * FloatsPerSprite];
        _sortIndices = new int[MaxSprites];
        _sortKeys = new float[MaxSprites];
        _sortedInstanceData = new float[MaxSprites * FloatsPerSprite];
    }

    public void Init(ShaderProgram shader)
    {
        _shader = shader;

        // Quad vertices: 2 triangles forming a unit quad (position + UV)
        float[] quadVerts =
        [
            // pos.x, pos.y, uv.x, uv.y
            0f, 0f, 0f, 0f,
            1f, 0f, 1f, 0f,
            1f, 1f, 1f, 1f,
            0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f,
            0f, 1f, 0f, 1f,
        ];

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Quad VBO (static geometry, never changes)
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* ptr = quadVerts)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVerts.Length * sizeof(float)),
                    ptr, BufferUsageARB.StaticDraw);
            }
        }

        // Attrib 0: quad position + UV (per-vertex)
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        // Instance VBO (dynamic, re-uploaded each frame)
        _instanceVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxSprites * FloatsPerSprite * sizeof(float)),
                null, BufferUsageARB.StreamDraw);
        }

        uint stride = FloatsPerSprite * sizeof(float);

        // Attrib 1: instance position + size (x, y, w, h) -- per-instance
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 0);
        _gl.VertexAttribDivisor(1, 1);

        // Attrib 2: atlas layer + padding (layer, _pad, 0, 0) -- per-instance
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
        _gl.VertexAttribDivisor(2, 1);

        // Attrib 3: UV coordinates (u0, v0, u1, v1) -- per-instance
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        _gl.VertexAttribDivisor(3, 1);

        // Attrib 4: tint color (r, g, b, a) -- per-instance
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, 10 * sizeof(float));
        _gl.VertexAttribDivisor(4, 1);

        // Attrib 5: sort key + padding (sortY, _pad) -- per-instance (used by shader for depth if needed)
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, false, stride, 14 * sizeof(float));
        _gl.VertexAttribDivisor(5, 1);

        _gl.BindVertexArray(0);
    }

    /// <summary>Reset the batch for a new frame. Call before any Draw() calls.</summary>
    public void Begin()
    {
        _spriteCount = 0;
        _needsSort = false;
    }

    /// <summary>
    /// Queue a sprite for rendering. Call between Begin() and End().
    /// Uses the legacy 12-float interface for backward compatibility.
    /// Sprites queued this way are not Y-sorted.
    /// </summary>
    public void Draw(float x, float y, float w, float h,
                     float u0, float v0, float u1, float v1,
                     float r = 1f, float g = 1f, float b = 1f, float a = 1f)
    {
        if (_spriteCount >= MaxSprites)
            return;

        int offset = _spriteCount * FloatsPerSprite;
        _instanceData[offset + 0] = x;
        _instanceData[offset + 1] = y;
        _instanceData[offset + 2] = w;
        _instanceData[offset + 3] = h;
        _instanceData[offset + 4] = 0f; // atlas layer
        _instanceData[offset + 5] = 0f; // padding
        _instanceData[offset + 6] = u0;
        _instanceData[offset + 7] = v0;
        _instanceData[offset + 8] = u1;
        _instanceData[offset + 9] = v1;
        _instanceData[offset + 10] = r;
        _instanceData[offset + 11] = g;
        _instanceData[offset + 12] = b;
        _instanceData[offset + 13] = a;
        _instanceData[offset + 14] = y; // sortY = y position
        _instanceData[offset + 15] = 0f; // padding
        _spriteCount++;
    }

    /// <summary>
    /// Queue a sprite with explicit atlas layer and sort key.
    /// Use for GPU-instanced rendering with texture array atlases.
    /// </summary>
    /// <param name="x">Screen X position.</param>
    /// <param name="y">Screen Y position.</param>
    /// <param name="w">Width in pixels.</param>
    /// <param name="h">Height in pixels.</param>
    /// <param name="atlasLayer">Layer index in the texture 2D array.</param>
    /// <param name="u0">Left UV coordinate.</param>
    /// <param name="v0">Top UV coordinate.</param>
    /// <param name="u1">Right UV coordinate.</param>
    /// <param name="v1">Bottom UV coordinate.</param>
    /// <param name="r">Tint red (0-1).</param>
    /// <param name="g">Tint green (0-1).</param>
    /// <param name="b">Tint blue (0-1).</param>
    /// <param name="a">Tint alpha (0-1).</param>
    /// <param name="sortY">Y-coordinate for depth sorting (higher Y = drawn later = in front).</param>
    public void DrawInstanced(float x, float y, float w, float h,
                              float atlasLayer,
                              float u0, float v0, float u1, float v1,
                              float r, float g, float b, float a,
                              float sortY)
    {
        if (_spriteCount >= MaxSprites)
            return;

        int offset = _spriteCount * FloatsPerSprite;
        _instanceData[offset + 0] = x;
        _instanceData[offset + 1] = y;
        _instanceData[offset + 2] = w;
        _instanceData[offset + 3] = h;
        _instanceData[offset + 4] = atlasLayer;
        _instanceData[offset + 5] = 0f;
        _instanceData[offset + 6] = u0;
        _instanceData[offset + 7] = v0;
        _instanceData[offset + 8] = u1;
        _instanceData[offset + 9] = v1;
        _instanceData[offset + 10] = r;
        _instanceData[offset + 11] = g;
        _instanceData[offset + 12] = b;
        _instanceData[offset + 13] = a;
        _instanceData[offset + 14] = sortY;
        _instanceData[offset + 15] = 0f;
        _spriteCount++;
        _needsSort = true;
    }

    /// <summary>
    /// Flush the batch: sort by Y if needed, upload to GPU, issue a single instanced draw call.
    /// </summary>
    public void End()
    {
        if (_spriteCount == 0)
        {
            _drawCallsLastFrame = 0;
            _instancesLastFrame = 0;
            return;
        }

        float[] uploadData;
        int uploadSize;

        if (_needsSort && _spriteCount > 1)
        {
            // Sort sprites by Y-coordinate (back-to-front) for correct depth overlap
            uploadData = SortAndCopyInstances();
            uploadSize = _spriteCount * FloatsPerSprite;
        }
        else
        {
            uploadData = _instanceData;
            uploadSize = _spriteCount * FloatsPerSprite;
        }

        _shader?.Use();
        _gl.BindVertexArray(_vao);

        // Upload instance data via buffer orphaning (StreamDraw) for best streaming performance
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            // Orphan the buffer first for async upload
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(MaxSprites * FloatsPerSprite * sizeof(float)),
                null, BufferUsageARB.StreamDraw);

            fixed (float* ptr = uploadData)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                    (nuint)(uploadSize * sizeof(float)), ptr);
            }
        }

        _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)_spriteCount);

        _drawCallsLastFrame = 1;
        _instancesLastFrame = _spriteCount;

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Sort sprite instances by their Y sort key (ascending = back to front).
    /// Returns a reference to the sorted data buffer ready for upload.
    /// Uses an index-based sort to avoid moving 16 floats per swap during comparison.
    /// </summary>
    private float[] SortAndCopyInstances()
    {
        // Build sort keys and indices
        for (int i = 0; i < _spriteCount; i++)
        {
            _sortIndices[i] = i;
            _sortKeys[i] = _instanceData[i * FloatsPerSprite + 14]; // sortY field
        }

        // Sort indices by sort key
        Array.Sort(_sortKeys, _sortIndices, 0, _spriteCount);

        // Copy instance data in sorted order
        for (int i = 0; i < _spriteCount; i++)
        {
            int srcOffset = _sortIndices[i] * FloatsPerSprite;
            int dstOffset = i * FloatsPerSprite;
            Array.Copy(_instanceData, srcOffset, _sortedInstanceData, dstOffset, FloatsPerSprite);
        }

        return _sortedInstanceData;
    }

    /// <summary>
    /// Create a GL_TEXTURE_2D_ARRAY for sprite atlases. Each layer is one sprite sheet.
    /// Call once during asset loading, then bind before rendering.
    /// </summary>
    /// <param name="width">Width of each atlas layer in pixels.</param>
    /// <param name="height">Height of each atlas layer in pixels.</param>
    /// <param name="layerCount">Number of layers (sprite sheets).</param>
    /// <returns>OpenGL texture handle for the array texture.</returns>
    public uint CreateTextureArray(int width, int height, int layerCount)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DArray, tex);

        unsafe
        {
            _gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, (uint)layerCount, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        return tex;
    }

    /// <summary>
    /// Upload pixel data into a specific layer of an existing texture array.
    /// </summary>
    /// <param name="textureArray">Handle from CreateTextureArray.</param>
    /// <param name="layer">Layer index (0-based).</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="pixels">RGBA8 pixel data.</param>
    public void UploadTextureArrayLayer(uint textureArray, int layer, int width, int height, byte[] pixels)
    {
        _gl.BindTexture(TextureTarget.Texture2DArray, textureArray);
        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                _gl.TexSubImage3D(TextureTarget.Texture2DArray, 0,
                    0, 0, layer,
                    (uint)width, (uint)height, 1,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }
    }

    /// <summary>Bind a texture array to the given texture unit.</summary>
    public void BindTextureArray(uint textureArray, uint unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2DArray, textureArray);
    }

    public void Dispose()
    {
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_instanceVbo != 0) _gl.DeleteBuffer(_instanceVbo);
        _shader?.Dispose();
    }
}
