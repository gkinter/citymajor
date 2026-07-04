using System.Runtime.InteropServices;
using Forge.Engine.Core;
using ImGuiNET;
using SDL2;
using Silk.NET.OpenGL;

namespace Forge.Engine.UI;

/// <summary>
/// ImGui integration for SDL2 + OpenGL 3.3. Handles input forwarding,
/// font loading, and GL-based rendering of ImGui draw data.
/// </summary>
public sealed class ImGuiController : IDisposable
{
    private readonly IntPtr _window;
    private readonly IntPtr _glContext;
    private readonly Config _config;

    private GL? _gl;
    private uint _fontTexture;
    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private int _attribLocationTex;
    private int _attribLocationProjMtx;
    private int _attribLocationVtxPos;
    private int _attribLocationVtxUV;
    private int _attribLocationVtxColor;

    private bool _initialized;

    public ImGuiController(IntPtr window, IntPtr glContext, Config config)
    {
        _window = window;
        _glContext = glContext;
        _config = config;
    }

    public void Init()
    {
        _gl = GL.GetApi(SDL.SDL_GL_GetProcAddress);

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;

        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        io.DisplaySize = new System.Numerics.Vector2(w, h);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);

        // Apply pixel art styling
        PixelArtStyle.Apply();

        // Create font texture
        CreateFontTexture();

        // Create shader + buffers
        CreateDeviceObjects();

        _initialized = true;
    }

    public void ProcessEvent(ref SDL.SDL_Event e)
    {
        var io = ImGui.GetIO();

        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_MOUSEMOTION:
                io.AddMousePosEvent(e.motion.x, e.motion.y);
                break;

            case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                io.AddMouseButtonEvent(SdlButtonToImGui(e.button.button), true);
                break;

            case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                io.AddMouseButtonEvent(SdlButtonToImGui(e.button.button), false);
                break;

            case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                io.AddMouseWheelEvent(e.wheel.x, e.wheel.y);
                break;

            case SDL.SDL_EventType.SDL_KEYDOWN:
            case SDL.SDL_EventType.SDL_KEYUP:
                bool down = e.type == SDL.SDL_EventType.SDL_KEYDOWN;
                var key = SdlKeyToImGui(e.key.keysym.scancode);
                if (key != ImGuiKey.None)
                    io.AddKeyEvent(key, down);
                break;

            case SDL.SDL_EventType.SDL_TEXTINPUT:
                unsafe
                {
                    fixed (byte* textPtr = e.text.text)
                    {
                        string? text = Marshal.PtrToStringUTF8((IntPtr)textPtr);
                        if (text != null)
                            io.AddInputCharactersUTF8(text);
                    }
                }
                break;
        }
    }

    public void BeginFrame(float deltaTime)
    {
        var io = ImGui.GetIO();
        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        io.DisplaySize = new System.Numerics.Vector2(w, h);
        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;
        ImGui.NewFrame();
    }

    public void EndFrame()
    {
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData());
    }

    private void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int _);

        _fontTexture = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, (void*)pixels);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }

    private void CreateDeviceObjects()
    {
        string vertexShader = @"
            #version 330 core
            layout(location = 0) in vec2 Position;
            layout(location = 1) in vec2 UV;
            layout(location = 2) in vec4 Color;
            uniform mat4 ProjMtx;
            out vec2 Frag_UV;
            out vec4 Frag_Color;
            void main() {
                Frag_UV = UV;
                Frag_Color = Color;
                gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
            }
        ";

        string fragmentShader = @"
            #version 330 core
            in vec2 Frag_UV;
            in vec4 Frag_Color;
            uniform sampler2D Texture;
            layout(location = 0) out vec4 Out_Color;
            void main() {
                Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
            }
        ";

        uint vert = CompileShader(ShaderType.VertexShader, vertexShader);
        uint frag = CompileShader(ShaderType.FragmentShader, fragmentShader);

        _shaderProgram = _gl!.CreateProgram();
        _gl.AttachShader(_shaderProgram, vert);
        _gl.AttachShader(_shaderProgram, frag);
        _gl.LinkProgram(_shaderProgram);
        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);

        _attribLocationTex = _gl.GetUniformLocation(_shaderProgram, "Texture");
        _attribLocationProjMtx = _gl.GetUniformLocation(_shaderProgram, "ProjMtx");
        _attribLocationVtxPos = _gl.GetAttribLocation(_shaderProgram, "Position");
        _attribLocationVtxUV = _gl.GetAttribLocation(_shaderProgram, "UV");
        _attribLocationVtxColor = _gl.GetAttribLocation(_shaderProgram, "Color");

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        int stride = Marshal.SizeOf<ImDrawVert>();
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        _gl.VertexAttribPointer((uint)_attribLocationVtxPos, 2, VertexAttribPointerType.Float, false, (uint)stride, 0);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxUV);
        _gl.VertexAttribPointer((uint)_attribLocationVtxUV, 2, VertexAttribPointerType.Float, false, (uint)stride, 8);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        _gl.VertexAttribPointer((uint)_attribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, (uint)stride, 16);

        _gl.BindVertexArray(0);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl!.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"ImGui shader compile error: {log}");
        }
        return shader;
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        // Save GL state
        _gl!.GetInteger(GetPName.ActiveTexture, out int lastActiveTexture);
        _gl.GetInteger(GetPName.CurrentProgram, out int lastProgram);
        _gl.GetInteger(GetPName.TextureBinding2D, out int lastTexture);
        _gl.GetInteger(GetPName.VertexArrayBinding, out int lastVao);
        bool lastBlend = _gl.IsEnabled(EnableCap.Blend);
        bool lastCull = _gl.IsEnabled(EnableCap.CullFace);
        bool lastDepth = _gl.IsEnabled(EnableCap.DepthTest);
        bool lastScissor = _gl.IsEnabled(EnableCap.ScissorTest);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
            BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ScissorTest);

        _gl.Viewport(0, 0, (uint)fbWidth, (uint)fbHeight);

        float l = drawData.DisplayPos.X;
        float r = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float t = drawData.DisplayPos.Y;
        float b = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        float[] orthoProjection =
        [
            2f / (r - l), 0, 0, 0,
            0, 2f / (t - b), 0, 0,
            0, 0, -1f, 0,
            (r + l) / (l - r), (t + b) / (b - t), 0, 1f,
        ];

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform1(_attribLocationTex, 0);
        fixed (float* ptr = orthoProjection)
        {
            _gl.UniformMatrix4(_attribLocationProjMtx, 1, false, ptr);
        }

        _gl.BindVertexArray(_vao);

        var clipOff = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(cmdList.VtxBuffer.Size * Marshal.SizeOf<ImDrawVert>()),
                (void*)cmdList.VtxBuffer.Data, BufferUsageARB.StreamDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdList.IdxBuffer.Data, BufferUsageARB.StreamDraw);

            for (int cmd_i = 0; cmd_i < cmdList.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmd_i];

                var clipRect = new System.Numerics.Vector4(
                    (pcmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y,
                    (pcmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y
                );

                if (clipRect.X < fbWidth && clipRect.Y < fbHeight && clipRect.Z >= 0 && clipRect.W >= 0)
                {
                    _gl.Scissor(
                        (int)clipRect.X,
                        fbHeight - (int)clipRect.W,
                        (uint)(clipRect.Z - clipRect.X),
                        (uint)(clipRect.W - clipRect.Y)
                    );

                    _gl.BindTexture(TextureTarget.Texture2D, (uint)(int)pcmd.TextureId);
                    _gl.DrawElementsBaseVertex(PrimitiveType.Triangles, pcmd.ElemCount,
                        DrawElementsType.UnsignedShort,
                        (void*)(pcmd.IdxOffset * sizeof(ushort)),
                        (int)pcmd.VtxOffset);
                }
            }
        }

        // Restore GL state
        _gl.UseProgram((uint)lastProgram);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)lastTexture);
        _gl.ActiveTexture((TextureUnit)lastActiveTexture);
        _gl.BindVertexArray((uint)lastVao);
        if (lastBlend) _gl.Enable(EnableCap.Blend); else _gl.Disable(EnableCap.Blend);
        if (lastCull) _gl.Enable(EnableCap.CullFace); else _gl.Disable(EnableCap.CullFace);
        if (lastDepth) _gl.Enable(EnableCap.DepthTest); else _gl.Disable(EnableCap.DepthTest);
        if (lastScissor) _gl.Enable(EnableCap.ScissorTest); else _gl.Disable(EnableCap.ScissorTest);
    }

    private static int SdlButtonToImGui(byte button) => button switch
    {
        (byte)SDL.SDL_BUTTON_LEFT => 0,
        (byte)SDL.SDL_BUTTON_RIGHT => 1,
        (byte)SDL.SDL_BUTTON_MIDDLE => 2,
        _ => -1
    };

    private static ImGuiKey SdlKeyToImGui(SDL.SDL_Scancode scancode) => scancode switch
    {
        SDL.SDL_Scancode.SDL_SCANCODE_TAB => ImGuiKey.Tab,
        SDL.SDL_Scancode.SDL_SCANCODE_LEFT => ImGuiKey.LeftArrow,
        SDL.SDL_Scancode.SDL_SCANCODE_RIGHT => ImGuiKey.RightArrow,
        SDL.SDL_Scancode.SDL_SCANCODE_UP => ImGuiKey.UpArrow,
        SDL.SDL_Scancode.SDL_SCANCODE_DOWN => ImGuiKey.DownArrow,
        SDL.SDL_Scancode.SDL_SCANCODE_HOME => ImGuiKey.Home,
        SDL.SDL_Scancode.SDL_SCANCODE_END => ImGuiKey.End,
        SDL.SDL_Scancode.SDL_SCANCODE_DELETE => ImGuiKey.Delete,
        SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE => ImGuiKey.Backspace,
        SDL.SDL_Scancode.SDL_SCANCODE_RETURN => ImGuiKey.Enter,
        SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE => ImGuiKey.Escape,
        SDL.SDL_Scancode.SDL_SCANCODE_SPACE => ImGuiKey.Space,
        SDL.SDL_Scancode.SDL_SCANCODE_A => ImGuiKey.A,
        SDL.SDL_Scancode.SDL_SCANCODE_C => ImGuiKey.C,
        SDL.SDL_Scancode.SDL_SCANCODE_V => ImGuiKey.V,
        SDL.SDL_Scancode.SDL_SCANCODE_X => ImGuiKey.X,
        SDL.SDL_Scancode.SDL_SCANCODE_Y => ImGuiKey.Y,
        SDL.SDL_Scancode.SDL_SCANCODE_Z => ImGuiKey.Z,
        _ => ImGuiKey.None
    };

    public void Dispose()
    {
        if (!_initialized) return;

        if (_fontTexture != 0) _gl?.DeleteTexture(_fontTexture);
        if (_shaderProgram != 0) _gl?.DeleteProgram(_shaderProgram);
        if (_vao != 0) _gl?.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl?.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl?.DeleteBuffer(_ebo);

        ImGui.DestroyContext();
        _gl?.Dispose();
        _initialized = false;
    }
}
