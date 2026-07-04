using System.Numerics;
using ImGuiNET;

namespace Forge.Engine.Core;

/// <summary>
/// Interface for game scenes (states). Each scene manages its own lifecycle,
/// update logic, rendering, and UI. Scenes are stacked, allowing overlays
/// (e.g., pause menu drawn over gameplay).
/// </summary>
public interface IScene : IDisposable
{
    /// <summary>Called when the scene becomes active (pushed or revealed by pop above).</summary>
    void Enter();

    /// <summary>Called when the scene is deactivated (popped or covered by another scene).</summary>
    void Exit();

    /// <summary>Called each fixed timestep for game logic.</summary>
    void Update(double dt);

    /// <summary>Called each frame for rendering game content.</summary>
    void Render(double alpha);

    /// <summary>Called each frame for ImGui UI rendering.</summary>
    void RenderUI();

    /// <summary>If true, scenes below this one do not receive Update calls.</summary>
    bool BlocksUpdate { get; }

    /// <summary>If true, scenes below this one do not receive Render calls.</summary>
    bool BlocksRender { get; }
}

/// <summary>
/// Manages a stack of game scenes with proper enter/exit lifecycle.
/// Supports push (overlay), pop (close overlay), and replace (swap) operations.
/// The game loop calls Update/Render/RenderUI which propagate through the stack.
/// </summary>
public sealed class SceneManager
{
    private readonly Stack<IScene> _sceneStack = new();
    private readonly List<Action> _pendingOperations = new();
    private bool _isIterating;

    // Cached scene array, rebuilt only when the stack changes (push/pop/replace)
    // to avoid allocating a new array on every Update/Render/RenderUI call.
    private IScene[] _cachedScenes = Array.Empty<IScene>();
    private bool _scenesCacheStale = true;

    /// <summary>The topmost (active) scene, or null if empty.</summary>
    public IScene? CurrentScene => _sceneStack.Count > 0 ? _sceneStack.Peek() : null;

    /// <summary>Number of scenes in the stack.</summary>
    public int SceneCount => _sceneStack.Count;

    /// <summary>
    /// Push a new scene on top of the stack. The current scene's Exit() is NOT called
    /// because it's just being covered, not removed. The new scene's Enter() IS called.
    /// </summary>
    public void PushScene(IScene scene)
    {
        if (_isIterating)
        {
            _pendingOperations.Add(() => PushScene(scene));
            return;
        }

        _sceneStack.Push(scene);
        _scenesCacheStale = true;
        scene.Enter();
    }

    /// <summary>
    /// Remove the top scene. Its Exit() and Dispose() are called.
    /// The scene below (if any) is NOT re-entered (it was never exited).
    /// </summary>
    public void PopScene()
    {
        if (_isIterating)
        {
            _pendingOperations.Add(PopScene);
            return;
        }

        if (_sceneStack.Count == 0) return;

        var top = _sceneStack.Pop();
        _scenesCacheStale = true;
        top.Exit();
        top.Dispose();
    }

    /// <summary>
    /// Replace the top scene with a new one. The old scene's Exit() and Dispose()
    /// are called, then the new scene's Enter() is called.
    /// </summary>
    public void ReplaceScene(IScene scene)
    {
        if (_isIterating)
        {
            _pendingOperations.Add(() => ReplaceScene(scene));
            return;
        }

        if (_sceneStack.Count > 0)
        {
            var old = _sceneStack.Pop();
            old.Exit();
            old.Dispose();
        }

        _sceneStack.Push(scene);
        _scenesCacheStale = true;
        scene.Enter();
    }

    /// <summary>
    /// Update the scene stack. Iterates top-down; stops at the first scene
    /// that blocks updates.
    /// </summary>
    public void Update(double dt)
    {
        if (_sceneStack.Count == 0) return;

        // Find the lowest scene that should receive updates
        _isIterating = true;
        var scenes = GetCachedScenes();
        int startFrom = 0;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].BlocksUpdate)
            {
                startFrom = i;
                break;
            }
            startFrom = i;
        }

        // Update from bottom of visible range to top
        for (int i = startFrom; i >= 0; i--)
        {
            scenes[i].Update(dt);
        }
        _isIterating = false;
        FlushPending();
    }

    /// <summary>
    /// Render the scene stack bottom-up. Only renders scenes that aren't blocked
    /// by a scene above them that blocks rendering.
    /// </summary>
    public void Render(double alpha)
    {
        if (_sceneStack.Count == 0) return;

        _isIterating = true;
        var scenes = GetCachedScenes();

        // Find the lowest visible scene (first blocking render scene from top)
        int renderFrom = scenes.Length - 1;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].BlocksRender)
            {
                renderFrom = i;
                break;
            }
        }

        // Render bottom-up from the lowest visible scene
        for (int i = renderFrom; i >= 0; i--)
        {
            scenes[i].Render(alpha);
        }
        _isIterating = false;
        FlushPending();
    }

    /// <summary>
    /// Render UI for all visible scenes, bottom-up.
    /// </summary>
    public void RenderUI()
    {
        if (_sceneStack.Count == 0) return;

        _isIterating = true;
        var scenes = GetCachedScenes();

        int renderFrom = scenes.Length - 1;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].BlocksRender)
            {
                renderFrom = i;
                break;
            }
        }

        for (int i = renderFrom; i >= 0; i--)
        {
            scenes[i].RenderUI();
        }
        _isIterating = false;
        FlushPending();
    }

    /// <summary>
    /// Get the cached scene array, rebuilding only when the stack has changed.
    /// </summary>
    private IScene[] GetCachedScenes()
    {
        if (_scenesCacheStale)
        {
            _cachedScenes = _sceneStack.ToArray();
            _scenesCacheStale = false;
        }
        return _cachedScenes;
    }

    private void FlushPending()
    {
        if (_pendingOperations.Count == 0) return;
        var ops = _pendingOperations.ToList();
        _pendingOperations.Clear();
        foreach (var op in ops)
            op();
    }
}

// =========================================================================
// Concrete Scenes
// =========================================================================

/// <summary>
/// Main menu: title screen with New Game, Load, Settings, and Quit buttons.
/// </summary>
public class MainMenuScene : IScene
{
    private readonly SceneManager _sceneManager;
    private readonly Action? _onQuit;

    public bool BlocksUpdate => true;
    public bool BlocksRender => true;

    /// <summary>
    /// Create the main menu scene.
    /// </summary>
    /// <param name="sceneManager">For scene transitions.</param>
    /// <param name="onQuit">Callback to exit the application.</param>
    public MainMenuScene(SceneManager sceneManager, Action? onQuit = null)
    {
        _sceneManager = sceneManager;
        _onQuit = onQuit;
    }

    public void Enter() => Console.WriteLine("[Scene] MainMenu entered.");
    public void Exit() => Console.WriteLine("[Scene] MainMenu exited.");
    public void Update(double dt) { }
    public void Render(double alpha) { }

    public void RenderUI()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float windowWidth = 320f;
        float windowHeight = 280f;

        ImGui.SetNextWindowPos(
            new Vector2((displaySize.X - windowWidth) * 0.5f, (displaySize.Y - windowHeight) * 0.5f),
            ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar;

        if (ImGui.Begin("MainMenu", flags))
        {
            // Title
            float titleWidth = ImGui.CalcTextSize("IRON & OAK").X;
            ImGui.SetCursorPosX((windowWidth - titleWidth) * 0.5f);
            ImGui.TextColored(new Vector4(0.9f, 0.75f, 0.4f, 1f), "IRON & OAK");

            ImGui.Dummy(new Vector2(0, 8));

            float subtitleWidth = ImGui.CalcTextSize("A City Building Game").X;
            ImGui.SetCursorPosX((windowWidth - subtitleWidth) * 0.5f);
            ImGui.TextDisabled("A City Building Game");

            ImGui.Dummy(new Vector2(0, 24));

            float buttonWidth = 200f;
            float buttonX = (windowWidth - buttonWidth) * 0.5f;

            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("New Game", new Vector2(buttonWidth, 36)))
            {
                _sceneManager.ReplaceScene(new NewGameScene(_sceneManager, _onQuit));
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Load Game", new Vector2(buttonWidth, 36)))
            {
                // Loading is handled via LoadingScene which transitions to GameplayScene
                _sceneManager.ReplaceScene(new LoadingScene(_sceneManager, "Loading save...", 2.0f, () =>
                {
                    _sceneManager.ReplaceScene(new GameplayScene(_sceneManager, _onQuit));
                }));
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Settings", new Vector2(buttonWidth, 36)))
            {
                // Settings would be another scene in a full implementation
                Console.WriteLine("[MainMenu] Settings clicked (not yet implemented).");
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Quit", new Vector2(buttonWidth, 36)))
            {
                _onQuit?.Invoke();
            }
        }
        ImGui.End();
    }

    public void Dispose() { }
}

/// <summary>
/// New game setup: map type, size, climate, difficulty selection with a Generate button.
/// </summary>
public class NewGameScene : IScene
{
    private readonly SceneManager _sceneManager;
    private readonly Action? _onQuit;

    // Configuration state
    private int _mapTypeIndex;
    private int _mapSizeIndex = 1;    // default: Medium
    private int _climateIndex;
    private int _difficultyIndex = 1; // default: Normal

    private static readonly string[] MapTypes = ["Mainland", "Archipelago", "Peninsula", "Valley", "Delta"];
    private static readonly string[] MapSizes = ["Small (256)", "Medium (512)", "Large (1024)"];
    private static readonly string[] Climates = ["Temperate", "Tropical", "Arid", "Arctic", "Mediterranean"];
    private static readonly string[] Difficulties = ["Easy", "Normal", "Hard", "Expert"];

    public bool BlocksUpdate => true;
    public bool BlocksRender => true;

    public NewGameScene(SceneManager sceneManager, Action? onQuit = null)
    {
        _sceneManager = sceneManager;
        _onQuit = onQuit;
    }

    public void Enter() => Console.WriteLine("[Scene] NewGame entered.");
    public void Exit() => Console.WriteLine("[Scene] NewGame exited.");
    public void Update(double dt) { }
    public void Render(double alpha) { }

    public void RenderUI()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float windowWidth = 380f;
        float windowHeight = 360f;

        ImGui.SetNextWindowPos(
            new Vector2((displaySize.X - windowWidth) * 0.5f, (displaySize.Y - windowHeight) * 0.5f),
            ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("New Game", flags))
        {
            ImGui.Text("Map Type:");
            ImGui.Combo("##maptype", ref _mapTypeIndex, MapTypes, MapTypes.Length);

            ImGui.Dummy(new Vector2(0, 8));
            ImGui.Text("Map Size:");
            ImGui.Combo("##mapsize", ref _mapSizeIndex, MapSizes, MapSizes.Length);

            ImGui.Dummy(new Vector2(0, 8));
            ImGui.Text("Climate:");
            ImGui.Combo("##climate", ref _climateIndex, Climates, Climates.Length);

            ImGui.Dummy(new Vector2(0, 8));
            ImGui.Text("Difficulty:");
            ImGui.Combo("##difficulty", ref _difficultyIndex, Difficulties, Difficulties.Length);

            ImGui.Dummy(new Vector2(0, 16));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 8));

            // Summary
            ImGui.TextDisabled($"Map: {MapTypes[_mapTypeIndex]} | {MapSizes[_mapSizeIndex]}");
            ImGui.TextDisabled($"Climate: {Climates[_climateIndex]} | {Difficulties[_difficultyIndex]}");

            ImGui.Dummy(new Vector2(0, 12));

            float totalWidth = ImGui.GetContentRegionAvail().X;
            float buttonWidth = 140f;
            float spacing = 12f;
            float startX = (totalWidth - buttonWidth * 2 - spacing) * 0.5f;

            ImGui.SetCursorPosX(startX);
            if (ImGui.Button("Back", new Vector2(buttonWidth, 36)))
            {
                _sceneManager.ReplaceScene(new MainMenuScene(_sceneManager, _onQuit));
            }

            ImGui.SameLine(0, spacing);

            if (ImGui.Button("Generate", new Vector2(buttonWidth, 36)))
            {
                int worldSize = _mapSizeIndex switch
                {
                    0 => 256,
                    1 => 512,
                    2 => 1024,
                    _ => 512,
                };

                string loadingMsg = $"Generating {MapTypes[_mapTypeIndex]} ({worldSize}x{worldSize})...";
                _sceneManager.ReplaceScene(new LoadingScene(_sceneManager, loadingMsg, 3.0f, () =>
                {
                    _sceneManager.ReplaceScene(new GameplayScene(_sceneManager, _onQuit));
                }));
            }
        }
        ImGui.End();
    }

    public void Dispose() { }
}

/// <summary>
/// Loading screen: shows a progress bar during map generation or save loading.
/// Calls a completion callback when done, which typically transitions to GameplayScene.
/// </summary>
public class LoadingScene : IScene
{
    private readonly SceneManager _sceneManager;
    private readonly string _message;
    private readonly float _estimatedDuration;
    private readonly Action _onComplete;

    private float _elapsed;
    private float _progress;
    private bool _completed;

    public bool BlocksUpdate => true;
    public bool BlocksRender => true;

    /// <summary>
    /// Create a loading screen.
    /// </summary>
    /// <param name="sceneManager">For scene transitions.</param>
    /// <param name="message">Display message (e.g., "Generating terrain...").</param>
    /// <param name="estimatedDuration">Approximate duration in seconds for progress bar.</param>
    /// <param name="onComplete">Called when loading finishes. Should transition to the next scene.</param>
    public LoadingScene(SceneManager sceneManager, string message, float estimatedDuration, Action onComplete)
    {
        _sceneManager = sceneManager;
        _message = message;
        _estimatedDuration = System.Math.Max(0.1f, estimatedDuration);
        _onComplete = onComplete;
    }

    public void Enter() => Console.WriteLine($"[Scene] Loading entered: {_message}");
    public void Exit() => Console.WriteLine("[Scene] Loading exited.");

    public void Update(double dt)
    {
        if (_completed) return;

        _elapsed += (float)dt;
        // Ease-out progress curve: fast at start, slows near end
        float t = System.Math.Clamp(_elapsed / _estimatedDuration, 0f, 1f);
        _progress = 1f - (1f - t) * (1f - t); // quadratic ease-out

        if (_elapsed >= _estimatedDuration && !_completed)
        {
            _completed = true;
            _progress = 1f;
            _onComplete();
        }
    }

    public void Render(double alpha) { }

    public void RenderUI()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        float windowWidth = 400f;
        float windowHeight = 100f;

        ImGui.SetNextWindowPos(
            new Vector2((displaySize.X - windowWidth) * 0.5f, (displaySize.Y - windowHeight) * 0.5f),
            ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar;

        if (ImGui.Begin("Loading", flags))
        {
            ImGui.Text(_message);
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.ProgressBar(_progress, new Vector2(-1, 24), $"{_progress * 100f:F0}%");
        }
        ImGui.End();
    }

    public void Dispose() { }
}

/// <summary>
/// Main gameplay scene: the actual city building game state.
/// Reads from a SimSnapshot reference provided by the simulation loop.
/// Does NOT maintain its own simulation state -- all data comes from the snapshot.
/// </summary>
public class GameplayScene : IScene
{
    private readonly SceneManager _sceneManager;
    private readonly Action? _onQuit;
    private readonly Func<Simulation.SimSnapshot>? _snapshotProvider;

    public bool BlocksUpdate => false;
    public bool BlocksRender => false;

    /// <summary>
    /// Create the gameplay scene.
    /// </summary>
    /// <param name="sceneManager">For scene transitions.</param>
    /// <param name="onQuit">Callback to exit the application.</param>
    /// <param name="snapshotProvider">Function that returns the current SimSnapshot from the simulation loop. If null, displays placeholder data.</param>
    public GameplayScene(SceneManager sceneManager, Action? onQuit = null,
                         Func<Simulation.SimSnapshot>? snapshotProvider = null)
    {
        _sceneManager = sceneManager;
        _onQuit = onQuit;
        _snapshotProvider = snapshotProvider;
    }

    public void Enter() => Console.WriteLine("[Scene] Gameplay entered.");
    public void Exit() => Console.WriteLine("[Scene] Gameplay exited.");

    public void Update(double dt)
    {
        // No local simulation -- all state is driven by SimulationLoop via the snapshot.
    }

    public void Render(double alpha)
    {
        // Real rendering is driven by IronAndOakGame via ChunkRenderer, SpriteRenderer, etc.
    }

    public void RenderUI()
    {
        var snapshot = _snapshotProvider?.Invoke();

        // Top bar
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(ImGui.GetIO().DisplaySize.X, 40), ImGuiCond.Always);
        var barFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                       ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar |
                       ImGuiWindowFlags.NoScrollbar;

        if (ImGui.Begin("TopBar", barFlags))
        {
            if (snapshot != null)
            {
                ImGui.Text($"Pop: {snapshot.Population:N0}");
                ImGui.SameLine(0, 20);
                ImGui.Text($"${snapshot.CityFunds:N0}");
                ImGui.SameLine(0, 20);
                ImGui.Text($"Happy: {snapshot.Happiness * 100:F0}%");
                ImGui.SameLine(0, 20);
                ImGui.Text(snapshot.DateString);
                ImGui.SameLine(0, 20);
                ImGui.Text($"Tick: {snapshot.TickCount}");
            }
            else
            {
                ImGui.Text("Waiting for simulation...");
            }

            // Pause button at the right
            float pauseX = ImGui.GetWindowWidth() - 80;
            ImGui.SameLine(pauseX);
            if (ImGui.Button("Pause"))
            {
                _sceneManager.PushScene(new PauseMenuScene(_sceneManager, _onQuit));
            }
        }
        ImGui.End();
    }

    public void Dispose()
    {
        Console.WriteLine("[Scene] Gameplay disposed.");
    }
}

/// <summary>
/// Pause menu overlay: drawn on top of gameplay. Blocks update (pauses the game)
/// but does not block render (gameplay still visible behind the semi-transparent overlay).
/// </summary>
public class PauseMenuScene : IScene
{
    private readonly SceneManager _sceneManager;
    private readonly Action? _onQuit;

    public bool BlocksUpdate => true;
    public bool BlocksRender => false; // gameplay visible behind

    public PauseMenuScene(SceneManager sceneManager, Action? onQuit = null)
    {
        _sceneManager = sceneManager;
        _onQuit = onQuit;
    }

    public void Enter() => Console.WriteLine("[Scene] PauseMenu entered.");
    public void Exit() => Console.WriteLine("[Scene] PauseMenu exited.");
    public void Update(double dt) { }
    public void Render(double alpha) { }

    public void RenderUI()
    {
        // Semi-transparent fullscreen overlay
        var displaySize = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(displaySize, ImGuiCond.Always);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.6f));
        var overlayFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                           ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar |
                           ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBringToFrontOnFocus;

        if (ImGui.Begin("PauseOverlay", overlayFlags))
        {
            // Intentionally empty - just the darkened background
        }
        ImGui.End();
        ImGui.PopStyleColor();

        // Centered pause menu panel
        float windowWidth = 260f;
        float windowHeight = 300f;

        ImGui.SetNextWindowPos(
            new Vector2((displaySize.X - windowWidth) * 0.5f, (displaySize.Y - windowHeight) * 0.5f),
            ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight), ImGuiCond.Always);

        var panelFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("Paused", panelFlags))
        {
            float buttonWidth = 200f;
            float buttonX = (windowWidth - buttonWidth) * 0.5f;

            ImGui.Dummy(new Vector2(0, 8));

            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Resume", new Vector2(buttonWidth, 36)))
            {
                _sceneManager.PopScene();
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Save Game", new Vector2(buttonWidth, 36)))
            {
                Console.WriteLine("[PauseMenu] Save game triggered.");
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Load Game", new Vector2(buttonWidth, 36)))
            {
                Console.WriteLine("[PauseMenu] Load game triggered.");
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Settings", new Vector2(buttonWidth, 36)))
            {
                Console.WriteLine("[PauseMenu] Settings triggered.");
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 4));

            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Quit to Menu", new Vector2(buttonWidth, 36)))
            {
                // Pop pause, then replace gameplay with main menu
                _sceneManager.PopScene();
                _sceneManager.ReplaceScene(new MainMenuScene(_sceneManager, _onQuit));
            }

            ImGui.Dummy(new Vector2(0, 4));
            ImGui.SetCursorPosX(buttonX);
            if (ImGui.Button("Quit to Desktop", new Vector2(buttonWidth, 36)))
            {
                _onQuit?.Invoke();
            }
        }
        ImGui.End();
    }

    public void Dispose() { }
}
