using System.Runtime.InteropServices;
using Forge.Engine.Input;
using Forge.Engine.Rendering;
using Forge.Engine.UI;
using SDL2;

namespace Forge.Engine.Core;

/// <summary>
/// Main application class. Initializes SDL2, creates an OpenGL 3.3 context,
/// sets up ImGui, and runs the game loop. Subclass this or subscribe to events
/// to build a game on top of the engine.
/// </summary>
public class ForgeApp : IDisposable
{
    private readonly Config _config;
    private readonly TimeManager _time;
    private readonly GameLoop _gameLoop;
    private readonly InputManager _input;

    private IntPtr _window;
    private IntPtr _glContext;
    private bool _disposed;
    private bool _shutdownCalled;

    // Subsystems (initialized in Init)
    private Renderer? _renderer;
    private ImGuiController? _imguiController;

    public Config Config => _config;
    public TimeManager Time => _time;
    public GameLoop Loop => _gameLoop;
    public InputManager Input => _input;
    public Renderer? Renderer => _renderer;
    public IntPtr WindowHandle => _window;

    /// <summary>Called after all engine subsystems are initialized, before the loop starts.</summary>
    public event Action? OnInitialized;

    /// <summary>Called each fixed timestep for game logic.</summary>
    public event GameLoop.UpdateHandler? OnUpdate;

    /// <summary>Called once per frame after input polling, before fixed updates.
    /// Use for input-dependent game logic (tool clicks, shortcuts) that must not
    /// be skipped or duplicated by the fixed timestep accumulator.</summary>
    public event Action? OnInputProcessed;

    /// <summary>Called each frame for rendering game content (before UI).</summary>
    public event GameLoop.RenderHandler? OnRenderGame;

    /// <summary>Called each frame for ImGui UI rendering.</summary>
    public event Action? OnRenderUI;

    /// <summary>Called on shutdown before resources are released.</summary>
    public event Action? OnShutdown;

    public ForgeApp(Config? config = null)
    {
        _config = config ?? new Config();
        _time = new TimeManager();
        _gameLoop = new GameLoop(_config, _time);
        _input = new InputManager();
    }

    public void Run()
    {
        Init();
        OnInitialized?.Invoke();

        // Wire up the game loop
        _gameLoop.OnProcessInput += ProcessInput;
        _gameLoop.OnInputProcessed += () => OnInputProcessed?.Invoke();
        _gameLoop.OnFixedUpdate += dt => OnUpdate?.Invoke(dt);
        _gameLoop.OnRender += RenderFrame;
        _gameLoop.OnRenderUI += RenderUI;
        _gameLoop.OnFrameEnd += FrameEnd;

        _gameLoop.Run();

        Shutdown();
    }

    private void Init()
    {
        // Validate configuration before initializing any subsystems
        _config.Validate();

        // Initialize SDL2
        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_GAMECONTROLLER) < 0)
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");
        }

        // Request OpenGL 3.3 Core Profile
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK,
            (int)SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);

        // Create window
        var windowFlags = SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL
                        | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                        | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN;

        if (_config.Fullscreen)
            windowFlags |= SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;

        _window = SDL.SDL_CreateWindow(
            _config.WindowTitle,
            SDL.SDL_WINDOWPOS_CENTERED,
            SDL.SDL_WINDOWPOS_CENTERED,
            _config.WindowWidth,
            _config.WindowHeight,
            windowFlags
        );

        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.SDL_GetError()}");
        }

        // Create OpenGL context
        _glContext = SDL.SDL_GL_CreateContext(_window);
        if (_glContext == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.SDL_GetError()}");
        }

        SDL.SDL_GL_MakeCurrent(_window, _glContext);

        // Raise window to front (macOS: SDL2 windows can open behind other apps)
        SDL.SDL_RaiseWindow(_window);
        SDL.SDL_SetWindowInputFocus(_window);

        // VSync
        SDL.SDL_GL_SetSwapInterval(_config.VSync ? 1 : 0);

        // Initialize renderer
        _renderer = new Renderer(_config);
        _renderer.Init();

        // Initialize ImGui
        if (_config.DebugOverlay)
        {
            _imguiController = new ImGuiController(_window, _glContext, _config);
            _imguiController.Init();
        }

        Console.WriteLine($"[ForgeApp] Initialized: {_config.WindowWidth}x{_config.WindowHeight}");
        Console.WriteLine($"[ForgeApp] OpenGL: {Marshal.PtrToStringAnsi(SDL.SDL_GL_GetCurrentWindow())}");
    }

    private void ProcessInput()
    {
        _input.BeginFrame();

        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
        {
            // Let ImGui handle events first
            _imguiController?.ProcessEvent(ref e);

            switch (e.type)
            {
                case SDL.SDL_EventType.SDL_QUIT:
                    _gameLoop.Stop();
                    break;

                case SDL.SDL_EventType.SDL_WINDOWEVENT:
                    if (e.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED)
                    {
                        int w = e.window.data1;
                        int h = e.window.data2;
                        _renderer?.Resize(w, h);
                    }
                    break;

                case SDL.SDL_EventType.SDL_KEYDOWN:
                    _input.OnKeyDown(e.key.keysym.scancode);
                    // Global hotkeys
                    if (e.key.keysym.scancode == SDL.SDL_Scancode.SDL_SCANCODE_SPACE)
                        _time.TogglePause();
                    if (e.key.keysym.scancode == SDL.SDL_Scancode.SDL_SCANCODE_F1)
                        _time.CycleSpeed();
                    if (e.key.keysym.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE)
                        _gameLoop.Stop();
                    break;

                case SDL.SDL_EventType.SDL_KEYUP:
                    _input.OnKeyUp(e.key.keysym.scancode);
                    break;

                case SDL.SDL_EventType.SDL_MOUSEMOTION:
                    _input.OnMouseMove(e.motion.x, e.motion.y, e.motion.xrel, e.motion.yrel);
                    break;

                case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    _input.OnMouseDown(e.button.button);
                    break;

                case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                    _input.OnMouseUp(e.button.button);
                    break;

                case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                    _input.OnMouseWheel(e.wheel.y);
                    break;
            }
        }

        _input.EndFrame();
    }

    private void RenderFrame(double alpha)
    {
        _renderer?.BeginFrame();
        OnRenderGame?.Invoke(alpha);
    }

    private void RenderUI()
    {
        if (_imguiController != null)
        {
            _imguiController.BeginFrame((float)_time.UnscaledDeltaTime);

            // Built-in debug panel
            if (_config.DebugOverlay)
            {
                RenderDebugPanel();
            }

            OnRenderUI?.Invoke();
            _imguiController.EndFrame();
        }
    }

    private void RenderDebugPanel()
    {
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiNET.ImGuiCond.FirstUseEver);
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiNET.ImGuiCond.FirstUseEver);

        if (ImGuiNET.ImGui.Begin("Forge Engine Debug"))
        {
            ImGuiNET.ImGui.Text($"FPS: {_time.Fps:F1}");
            ImGuiNET.ImGui.Text($"Frame: {_time.FrameCount}");
            ImGuiNET.ImGui.Text($"Game Time: {_time.TotalTime:F2}s");
            ImGuiNET.ImGui.Text($"Speed: {_time.SpeedLevel}x (mult: {_time.SpeedMultiplier:F1})");
            ImGuiNET.ImGui.Text($"Paused: {_time.IsPaused}");

            ImGuiNET.ImGui.Separator();

            if (ImGuiNET.ImGui.Button("Pause/Resume"))
                _time.TogglePause();
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button("Speed+"))
                _time.CycleSpeed();

            ImGuiNET.ImGui.Separator();
            ImGuiNET.ImGui.Text($"World: {_config.WorldSize}x{_config.WorldSize}");
            ImGuiNET.ImGui.Text($"Chunks: {_config.TotalChunks} ({_config.ChunkSize}x{_config.ChunkSize})");
        }
        ImGuiNET.ImGui.End();
    }

    private void FrameEnd()
    {
        SDL.SDL_GL_SwapWindow(_window);
    }

    private void Shutdown()
    {
        if (_shutdownCalled)
            return;
        _shutdownCalled = true;

        OnShutdown?.Invoke();

        _imguiController?.Dispose();
        _renderer?.Dispose();

        if (_glContext != IntPtr.Zero)
            SDL.SDL_GL_DeleteContext(_glContext);
        if (_window != IntPtr.Zero)
            SDL.SDL_DestroyWindow(_window);

        SDL.SDL_Quit();
        Console.WriteLine("[ForgeApp] Shutdown complete.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Shutdown();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
