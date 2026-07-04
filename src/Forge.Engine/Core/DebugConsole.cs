using System.Numerics;
using System.Text;
using ImGuiNET;
using Forge.Engine.Input;

namespace Forge.Engine.Core;

/// <summary>
/// Entry in the console output history. Stores the message text and its display color.
/// </summary>
public readonly struct ConsoleEntry
{
    public readonly string Text;
    public readonly uint PackedColor;
    public readonly DateTime Timestamp;

    public ConsoleEntry(string text, uint packedColor)
    {
        Text = text;
        PackedColor = packedColor;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// A registered console command with its name, description, and execution handler.
/// </summary>
internal sealed class ConsoleCommand
{
    public string Name { get; }
    public string Description { get; }
    public Func<string[], string?> Handler { get; }

    public ConsoleCommand(string name, string description, Func<string[], string?> handler)
    {
        Name = name;
        Description = description;
        Handler = handler;
    }
}

/// <summary>
/// In-game developer console inspired by Quake/Source engine consoles.
/// Supports command registration, execution, tab-completion, command history recall,
/// and ImGui-rendered UI as a semi-transparent dropdown from the top of the screen.
///
/// Thread safety: NOT thread-safe. All calls must be from the main thread,
/// which is standard for input handling and UI rendering.
/// </summary>
public sealed class DebugConsole
{
    private readonly Dictionary<string, ConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ConsoleEntry> _history = new();
    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;

    private string _inputBuffer = string.Empty;
    private bool _scrollToBottom;
    private bool _focusInput;
    private string[] _completionCandidates = Array.Empty<string>();
    private int _completionIndex = -1;

    // Color constants packed as ABGR uint for ImGui
    private const uint ColorWhite = 0xFFFFFFFF;
    private const uint ColorYellow = 0xFF00FFFF;
    private const uint ColorRed = 0xFF0000FF;
    private const uint ColorGreen = 0xFF00FF00;
    private const uint ColorCyan = 0xFFFFFF00;
    private const uint ColorGray = 0xFFAAAAAA;

    /// <summary>Whether the console overlay is currently visible.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Scrollable output history entries.</summary>
    public IReadOnlyList<ConsoleEntry> History => _history;

    /// <summary>Previously executed command strings for up/down arrow recall.</summary>
    public IReadOnlyList<string> CommandHistory => _commandHistory;

    /// <summary>Number of registered commands.</summary>
    public int CommandCount => _commands.Count;

    public DebugConsole()
    {
        RegisterBuiltInCommands();
    }

    // =========================================================================
    // Command registration
    // =========================================================================

    /// <summary>
    /// Register a command that performs an action without returning output.
    /// </summary>
    public void RegisterCommand(string name, string description, Action<string[]> handler)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(handler);

        _commands[name.ToLowerInvariant()] = new ConsoleCommand(
            name.ToLowerInvariant(),
            description,
            args => { handler(args); return null; }
        );
    }

    /// <summary>
    /// Register a command that returns output text to display in the console.
    /// </summary>
    public void RegisterCommand(string name, string description, Func<string[], string> handler)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(handler);

        _commands[name.ToLowerInvariant()] = new ConsoleCommand(
            name.ToLowerInvariant(),
            description,
            args => handler(args)
        );
    }

    /// <summary>
    /// Check whether a command with the given name is registered.
    /// </summary>
    public bool HasCommand(string name) => _commands.ContainsKey(name.ToLowerInvariant());

    // =========================================================================
    // Execution
    // =========================================================================

    /// <summary>
    /// Parse and execute a command line string. The first token is the command name,
    /// remaining tokens are arguments split by whitespace.
    /// </summary>
    public void Execute(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return;

        string trimmed = commandLine.Trim();

        // Add to command history (avoid consecutive duplicates)
        if (_commandHistory.Count == 0 || !string.Equals(_commandHistory[^1], trimmed, StringComparison.Ordinal))
        {
            _commandHistory.Add(trimmed);
        }
        _commandHistoryIndex = -1;

        // Echo the command in the console
        Log($"> {trimmed}", ColorGray);

        // Parse: first token = command, rest = args
        string[] tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return;

        string cmdName = tokens[0].ToLowerInvariant();
        string[] args = tokens.Length > 1 ? tokens[1..] : Array.Empty<string>();

        if (!_commands.TryGetValue(cmdName, out var command))
        {
            LogError($"Unknown command: {cmdName}. Type 'help' for a list of commands.");
            return;
        }

        try
        {
            string? result = command.Handler(args);
            if (!string.IsNullOrEmpty(result))
                Log(result);
        }
        catch (Exception ex)
        {
            LogError($"Command '{cmdName}' failed: {ex.Message}");
        }
    }

    // =========================================================================
    // Output
    // =========================================================================

    /// <summary>Log a message to the console with the specified packed ABGR color.</summary>
    public void Log(string message, uint packedColor)
    {
        _history.Add(new ConsoleEntry(message, packedColor));
        _scrollToBottom = true;
    }

    /// <summary>Log a white message to the console.</summary>
    public void Log(string message) => Log(message, ColorWhite);

    /// <summary>Log a yellow warning message.</summary>
    public void LogWarning(string message) => Log($"[WARN] {message}", ColorYellow);

    /// <summary>Log a red error message.</summary>
    public void LogError(string message) => Log($"[ERROR] {message}", ColorRed);

    /// <summary>Clear all console output history.</summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    // =========================================================================
    // Auto-complete
    // =========================================================================

    /// <summary>
    /// Get command name completions matching a partial input string.
    /// Returns all registered command names that start with the partial text.
    /// </summary>
    public string[] GetCompletions(string partial)
    {
        if (string.IsNullOrEmpty(partial))
            return _commands.Keys.OrderBy(k => k).ToArray();

        string lower = partial.ToLowerInvariant();
        return _commands.Keys
            .Where(k => k.StartsWith(lower, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .ToArray();
    }

    // =========================================================================
    // Input handling
    // =========================================================================

    /// <summary>
    /// Handle input for toggling the console and navigating command history.
    /// Call once per frame before RenderUI.
    /// </summary>
    public void HandleInput(InputManager input)
    {
        // Toggle console with backtick/grave key
        if (input.IsKeyPressed(SDL2.SDL.SDL_Scancode.SDL_SCANCODE_GRAVE))
        {
            IsOpen = !IsOpen;
            if (IsOpen)
                _focusInput = true;
        }
    }

    // =========================================================================
    // ImGui rendering
    // =========================================================================

    /// <summary>
    /// Render the console UI as a semi-transparent ImGui window at the top of the screen.
    /// Call during the UI pass when IsOpen is true.
    /// </summary>
    public void RenderUI()
    {
        if (!IsOpen)
            return;

        var viewport = ImGui.GetMainViewport();
        float consoleHeight = viewport.Size.Y * 0.4f;

        ImGui.SetNextWindowPos(viewport.Pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(viewport.Size.X, consoleHeight), ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.05f, 0.05f, 0.1f, 0.88f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.1f, 0.1f, 0.2f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.15f, 0.15f, 0.3f, 0.95f));

        var flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

        bool isOpenRef = IsOpen;
        if (ImGui.Begin("Developer Console", ref isOpenRef, flags))
        {
            IsOpen = isOpenRef;

            // Scrollable output region
            float footerHeight = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
            if (ImGui.BeginChild("ConsoleOutput", new Vector2(0, -footerHeight), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
            {
                for (int i = 0; i < _history.Count; i++)
                {
                    var entry = _history[i];
                    ImGui.PushStyleColor(ImGuiCol.Text, entry.PackedColor);
                    ImGui.TextUnformatted(entry.Text);
                    ImGui.PopStyleColor();
                }

                if (_scrollToBottom)
                {
                    ImGui.SetScrollHereY(1.0f);
                    _scrollToBottom = false;
                }
            }
            ImGui.EndChild();

            // Input field
            ImGui.Separator();

            if (_focusInput)
            {
                ImGui.SetKeyboardFocusHere();
                _focusInput = false;
            }

            var inputFlags = ImGuiInputTextFlags.EnterReturnsTrue |
                             ImGuiInputTextFlags.CallbackHistory |
                             ImGuiInputTextFlags.CallbackCompletion;

            bool submitted;
            unsafe
            {
                submitted = ImGui.InputText("##ConsoleInput", ref _inputBuffer, 1024, inputFlags, InputCallback);
            }

            ImGui.SameLine();
            if (ImGui.Button("Submit") || submitted)
            {
                if (!string.IsNullOrWhiteSpace(_inputBuffer))
                {
                    Execute(_inputBuffer);
                    _inputBuffer = string.Empty;
                }
                _focusInput = true;
            }
        }

        ImGui.End();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();
    }

    private unsafe int InputCallback(ImGuiInputTextCallbackData* rawData)
    {
        var data = new ImGuiInputTextCallbackDataPtr(rawData);

        switch (data.EventFlag)
        {
            case ImGuiInputTextFlags.CallbackHistory:
                HandleHistoryCallback(data);
                break;

            case ImGuiInputTextFlags.CallbackCompletion:
                HandleCompletionCallback(data);
                break;
        }
        return 0;
    }

    private void HandleHistoryCallback(ImGuiInputTextCallbackDataPtr data)
    {
        if (_commandHistory.Count == 0)
            return;

        if (data.EventKey == ImGuiKey.UpArrow)
        {
            if (_commandHistoryIndex < 0)
                _commandHistoryIndex = _commandHistory.Count - 1;
            else if (_commandHistoryIndex > 0)
                _commandHistoryIndex--;
        }
        else if (data.EventKey == ImGuiKey.DownArrow)
        {
            if (_commandHistoryIndex >= 0)
                _commandHistoryIndex++;

            if (_commandHistoryIndex >= _commandHistory.Count)
            {
                _commandHistoryIndex = -1;
                data.DeleteChars(0, data.BufTextLen);
                return;
            }
        }

        if (_commandHistoryIndex >= 0 && _commandHistoryIndex < _commandHistory.Count)
        {
            string histEntry = _commandHistory[_commandHistoryIndex];
            data.DeleteChars(0, data.BufTextLen);
            data.InsertChars(0, histEntry);
        }
    }

    private void HandleCompletionCallback(ImGuiInputTextCallbackDataPtr data)
    {
        // Build current text from the managed buffer
        unsafe
        {
            string current = Encoding.UTF8.GetString(data.NativePtr->Buf, data.BufTextLen);

            string[] tokens = current.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 1)
                return; // Only complete the command name (first token)

            string partial = tokens.Length > 0 ? tokens[0] : string.Empty;
            var completions = GetCompletions(partial);

            if (completions.Length == 1)
            {
                data.DeleteChars(0, data.BufTextLen);
                data.InsertChars(0, completions[0] + " ");
            }
            else if (completions.Length > 1)
            {
                string prefix = FindCommonPrefix(completions);
                if (prefix.Length > partial.Length)
                {
                    data.DeleteChars(0, data.BufTextLen);
                    data.InsertChars(0, prefix);
                }

                Log(string.Join("  ", completions), ColorCyan);
            }
        }
    }

    private static string FindCommonPrefix(string[] strings)
    {
        if (strings.Length == 0) return string.Empty;
        if (strings.Length == 1) return strings[0];

        string first = strings[0];
        int prefixLen = first.Length;

        for (int i = 1; i < strings.Length; i++)
        {
            prefixLen = System.Math.Min(prefixLen, strings[i].Length);
            for (int j = 0; j < prefixLen; j++)
            {
                if (char.ToLowerInvariant(first[j]) != char.ToLowerInvariant(strings[i][j]))
                {
                    prefixLen = j;
                    break;
                }
            }
        }

        return first[..prefixLen];
    }

    // =========================================================================
    // Built-in commands
    // =========================================================================

    private void RegisterBuiltInCommands()
    {
        RegisterCommand("help", "List all commands, or 'help <command>' for details", args =>
        {
            if (args.Length > 0)
            {
                string cmdName = args[0].ToLowerInvariant();
                if (_commands.TryGetValue(cmdName, out var cmd))
                    return $"{cmd.Name}: {cmd.Description}";
                return $"Unknown command: {cmdName}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Available commands:");
            foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
            {
                sb.AppendLine($"  {cmd.Name,-24} {cmd.Description}");
            }
            return sb.ToString().TrimEnd();
        });

        RegisterCommand("clear", "Clear console output", _ =>
        {
            ClearHistory();
        });

        RegisterCommand("quit", "Quit the game", _ =>
        {
            Log("Quitting...", ColorYellow);
            Environment.Exit(0);
        });

        RegisterCommand("exit", "Quit the game (alias for quit)", _ =>
        {
            Log("Quitting...", ColorYellow);
            Environment.Exit(0);
        });

        RegisterCommand("fps", "Toggle FPS overlay display", _ =>
        {
            _fpsOverlayVisible = !_fpsOverlayVisible;
            return $"FPS overlay: {(_fpsOverlayVisible ? "ON" : "OFF")}";
        });

        RegisterCommand("speed", "Set game speed (0=paused, 1=normal, 2=fast, 3=faster, 4=fastest)", args =>
        {
            if (args.Length == 0)
                return $"Current speed level: {_gameSpeedLevel}. Usage: speed <0-4>";

            if (!int.TryParse(args[0], out int level) || level < 0 || level > 4)
                return "Invalid speed level. Use 0-4.";

            _gameSpeedLevel = level;
            GameSpeedChanged?.Invoke(level);
            return $"Game speed set to {level}";
        });

        RegisterCommand("pause", "Toggle pause state", _ =>
        {
            _gameSpeedLevel = _gameSpeedLevel == 0 ? 1 : 0;
            GameSpeedChanged?.Invoke(_gameSpeedLevel);
            return _gameSpeedLevel == 0 ? "Game paused" : "Game unpaused";
        });

        RegisterCommand("money", "Add or set treasury amount. Usage: money <amount>", args =>
        {
            if (args.Length == 0)
                return "Usage: money <amount>";

            if (!long.TryParse(args[0], out long amount))
                return "Invalid amount. Provide a number.";

            MoneyChanged?.Invoke(amount);
            return $"Treasury set to {amount:N0}";
        });

        RegisterCommand("pop", "Set population. Usage: pop <amount>", args =>
        {
            if (args.Length == 0)
                return "Usage: pop <amount>";

            if (!int.TryParse(args[0], out int amount) || amount < 0)
                return "Invalid amount. Provide a non-negative number.";

            PopulationChanged?.Invoke(amount);
            return $"Population set to {amount:N0}";
        });

        RegisterCommand("spawn_building", "Place a building. Usage: spawn_building <typeId> <x> <y>", args =>
        {
            if (args.Length < 3)
                return "Usage: spawn_building <typeId> <x> <y>";

            if (!ushort.TryParse(args[0], out ushort typeId))
                return "Invalid typeId.";
            if (!int.TryParse(args[1], out int x))
                return "Invalid x coordinate.";
            if (!int.TryParse(args[2], out int y))
                return "Invalid y coordinate.";

            BuildingSpawned?.Invoke(typeId, x, y);
            return $"Spawned building type {typeId} at ({x}, {y})";
        });

        RegisterCommand("demolish", "Remove building at tile. Usage: demolish <x> <y>", args =>
        {
            if (args.Length < 2)
                return "Usage: demolish <x> <y>";

            if (!int.TryParse(args[0], out int x))
                return "Invalid x coordinate.";
            if (!int.TryParse(args[1], out int y))
                return "Invalid y coordinate.";

            TileDemolished?.Invoke(x, y);
            return $"Demolished tile at ({x}, {y})";
        });

        RegisterCommand("set_zoom", "Set camera zoom level. Usage: set_zoom <1-8>", args =>
        {
            if (args.Length == 0)
                return "Usage: set_zoom <1-8>";

            if (!int.TryParse(args[0], out int zoom) || zoom < 1 || zoom > 8)
                return "Invalid zoom level. Use 1-8.";

            ZoomChanged?.Invoke(zoom);
            return $"Camera zoom set to {zoom}";
        });

        RegisterCommand("goto", "Move camera to tile. Usage: goto <x> <y>", args =>
        {
            if (args.Length < 2)
                return "Usage: goto <x> <y>";

            if (!int.TryParse(args[0], out int x))
                return "Invalid x coordinate.";
            if (!int.TryParse(args[1], out int y))
                return "Invalid y coordinate.";

            CameraGoto?.Invoke(x, y);
            return $"Camera moved to ({x}, {y})";
        });

        RegisterCommand("weather", "Set weather. Usage: weather <clear|rain|snow|fog>", args =>
        {
            if (args.Length == 0)
                return "Usage: weather <clear|rain|snow|fog>";

            string w = args[0].ToLowerInvariant();
            if (w is not ("clear" or "rain" or "snow" or "fog"))
                return "Invalid weather type. Use: clear, rain, snow, fog";

            WeatherChanged?.Invoke(w);
            return $"Weather set to {w}";
        });

        RegisterCommand("time", "Set time of day (hour). Usage: time <0-23>", args =>
        {
            if (args.Length == 0)
                return "Usage: time <0-23>";

            if (!int.TryParse(args[0], out int hour) || hour < 0 || hour > 23)
                return "Invalid hour. Use 0-23.";

            TimeOfDayChanged?.Invoke(hour);
            return $"Time of day set to {hour}:00";
        });

        RegisterCommand("season", "Set season. Usage: season <spring|summer|autumn|winter>", args =>
        {
            if (args.Length == 0)
                return "Usage: season <spring|summer|autumn|winter>";

            string s = args[0].ToLowerInvariant();
            if (s is not ("spring" or "summer" or "autumn" or "winter"))
                return "Invalid season. Use: spring, summer, autumn, winter";

            SeasonChanged?.Invoke(s);
            return $"Season set to {s}";
        });

        RegisterCommand("era", "Set era. Usage: era <1-5>", args =>
        {
            if (args.Length == 0)
                return "Usage: era <1-5>";

            if (!int.TryParse(args[0], out int era) || era < 1 || era > 5)
                return "Invalid era. Use 1-5.";

            EraChanged?.Invoke(era);
            return $"Era set to {era}";
        });

        RegisterCommand("unlock_all_tech", "Unlock all technologies", _ =>
        {
            UnlockAllTech?.Invoke();
            return "All technologies unlocked.";
        });

        RegisterCommand("save", "Save game. Usage: save <name>", args =>
        {
            if (args.Length == 0)
                return "Usage: save <name>";

            SaveRequested?.Invoke(args[0]);
            return $"Save requested: {args[0]}";
        });

        RegisterCommand("load", "Load game. Usage: load <name>", args =>
        {
            if (args.Length == 0)
                return "Usage: load <name>";

            LoadRequested?.Invoke(args[0]);
            return $"Load requested: {args[0]}";
        });

        RegisterCommand("map_info", "Print map statistics", _ =>
        {
            var info = MapInfoRequested?.Invoke();
            return info ?? "Map info not available.";
        });

        RegisterCommand("sim_info", "Print simulation statistics", _ =>
        {
            var info = SimInfoRequested?.Invoke();
            return info ?? "Simulation info not available.";
        });

        RegisterCommand("mem_info", "Print memory usage", _ =>
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            long workingSet = proc.WorkingSet64;
            long gcTotal = GC.GetTotalMemory(false);
            long gen0 = GC.CollectionCount(0);
            long gen1 = GC.CollectionCount(1);
            long gen2 = GC.CollectionCount(2);

            var sb = new StringBuilder();
            sb.AppendLine("Memory Info:");
            sb.AppendLine($"  Working set:    {workingSet / (1024.0 * 1024.0):F1} MB");
            sb.AppendLine($"  GC heap:        {gcTotal / (1024.0 * 1024.0):F1} MB");
            sb.AppendLine($"  GC collections: Gen0={gen0} Gen1={gen1} Gen2={gen2}");
            return sb.ToString().TrimEnd();
        });

        RegisterCommand("reload_data", "Hot reload JSON data files", _ =>
        {
            DataReloadRequested?.Invoke();
            return "Data reload triggered.";
        });

        RegisterCommand("screenshot", "Take a screenshot", _ =>
        {
            ScreenshotRequested?.Invoke();
            return "Screenshot requested.";
        });

        RegisterCommand("debug_overlay", "Toggle a debug overlay. Usage: debug_overlay <name>", args =>
        {
            if (args.Length == 0)
                return "Usage: debug_overlay <name>";

            DebugOverlayToggled?.Invoke(args[0]);
            return $"Debug overlay '{args[0]}' toggled.";
        });
    }

    // =========================================================================
    // Console state used by built-in commands
    // =========================================================================

    private bool _fpsOverlayVisible;
    private int _gameSpeedLevel = 1;

    /// <summary>Whether the FPS overlay should be shown (toggled by 'fps' command).</summary>
    public bool FpsOverlayVisible => _fpsOverlayVisible;

    /// <summary>Current game speed level set via console commands.</summary>
    public int GameSpeedLevel => _gameSpeedLevel;

    // =========================================================================
    // Events for game integration (game systems subscribe to these)
    // =========================================================================

    /// <summary>Fired when game speed is changed via console. Arg: speed level 0-4.</summary>
    public event Action<int>? GameSpeedChanged;

    /// <summary>Fired when money cheat is used. Arg: amount.</summary>
    public event Action<long>? MoneyChanged;

    /// <summary>Fired when population cheat is used. Arg: count.</summary>
    public event Action<int>? PopulationChanged;

    /// <summary>Fired when a building is spawned via console. Args: typeId, x, y.</summary>
    public event Action<ushort, int, int>? BuildingSpawned;

    /// <summary>Fired when a tile demolish is requested. Args: x, y.</summary>
    public event Action<int, int>? TileDemolished;

    /// <summary>Fired when zoom level changes. Arg: zoom 1-8.</summary>
    public event Action<int>? ZoomChanged;

    /// <summary>Fired when camera should move to a tile. Args: x, y.</summary>
    public event Action<int, int>? CameraGoto;

    /// <summary>Fired when weather is set. Arg: weather type string.</summary>
    public event Action<string>? WeatherChanged;

    /// <summary>Fired when time of day changes. Arg: hour 0-23.</summary>
    public event Action<int>? TimeOfDayChanged;

    /// <summary>Fired when season is set. Arg: season name string.</summary>
    public event Action<string>? SeasonChanged;

    /// <summary>Fired when era is set. Arg: era 1-5.</summary>
    public event Action<int>? EraChanged;

    /// <summary>Fired when all tech unlock is requested.</summary>
    public event Action? UnlockAllTech;

    /// <summary>Fired when a save is requested. Arg: save name.</summary>
    public event Action<string>? SaveRequested;

    /// <summary>Fired when a load is requested. Arg: save name.</summary>
    public event Action<string>? LoadRequested;

    /// <summary>Delegate to provide map info string. Return null if unavailable.</summary>
    public Func<string?>? MapInfoRequested { get; set; }

    /// <summary>Delegate to provide sim info string. Return null if unavailable.</summary>
    public Func<string?>? SimInfoRequested { get; set; }

    /// <summary>Fired when data reload is requested.</summary>
    public event Action? DataReloadRequested;

    /// <summary>Fired when a screenshot is requested.</summary>
    public event Action? ScreenshotRequested;

    /// <summary>Fired when a debug overlay is toggled. Arg: overlay name.</summary>
    public event Action<string>? DebugOverlayToggled;
}

