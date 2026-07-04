using System.Text.Json;
using SDL2;

namespace Forge.Engine.Input;

/// <summary>
/// Rebindable key system. Maps action names to SDL scancodes.
/// Supports save/load to persist user preferences as JSON.
/// </summary>
public sealed class KeyBindings
{
    private readonly Dictionary<string, SDL.SDL_Scancode> _bindings = new();
    private readonly Dictionary<string, SDL.SDL_Scancode> _defaults = new();

    public KeyBindings()
    {
        // Camera controls
        SetDefault("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_A);
        SetDefault("pan_right", SDL.SDL_Scancode.SDL_SCANCODE_D);
        SetDefault("pan_up", SDL.SDL_Scancode.SDL_SCANCODE_W);
        SetDefault("pan_down", SDL.SDL_Scancode.SDL_SCANCODE_S);
        SetDefault("rotate_left", SDL.SDL_Scancode.SDL_SCANCODE_Q);
        SetDefault("rotate_right", SDL.SDL_Scancode.SDL_SCANCODE_E);
        SetDefault("zoom_in", SDL.SDL_Scancode.SDL_SCANCODE_EQUALS);
        SetDefault("zoom_out", SDL.SDL_Scancode.SDL_SCANCODE_MINUS);

        // Game speed
        SetDefault("pause", SDL.SDL_Scancode.SDL_SCANCODE_SPACE);
        SetDefault("speed_cycle", SDL.SDL_Scancode.SDL_SCANCODE_F1);
        SetDefault("speed_1", SDL.SDL_Scancode.SDL_SCANCODE_1);
        SetDefault("speed_2", SDL.SDL_Scancode.SDL_SCANCODE_2);
        SetDefault("speed_3", SDL.SDL_Scancode.SDL_SCANCODE_3);

        // Tools
        SetDefault("bulldoze", SDL.SDL_Scancode.SDL_SCANCODE_B);
        SetDefault("road_tool", SDL.SDL_Scancode.SDL_SCANCODE_R);
        SetDefault("zone_tool", SDL.SDL_Scancode.SDL_SCANCODE_Z);
        SetDefault("power_tool", SDL.SDL_Scancode.SDL_SCANCODE_P);
        SetDefault("water_tool", SDL.SDL_Scancode.SDL_SCANCODE_U);
        SetDefault("terraform_tool", SDL.SDL_Scancode.SDL_SCANCODE_T);

        // UI
        SetDefault("cancel", SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE);
        SetDefault("quicksave", SDL.SDL_Scancode.SDL_SCANCODE_F5);
        SetDefault("quickload", SDL.SDL_Scancode.SDL_SCANCODE_F9);
        SetDefault("toggle_overlay", SDL.SDL_Scancode.SDL_SCANCODE_TAB);
        SetDefault("toggle_grid", SDL.SDL_Scancode.SDL_SCANCODE_G);
        SetDefault("budget_panel", SDL.SDL_Scancode.SDL_SCANCODE_F2);
        SetDefault("research_panel", SDL.SDL_Scancode.SDL_SCANCODE_F3);
        SetDefault("ordinance_panel", SDL.SDL_Scancode.SDL_SCANCODE_F4);
        SetDefault("screenshot", SDL.SDL_Scancode.SDL_SCANCODE_F12);
    }

    private void SetDefault(string action, SDL.SDL_Scancode key)
    {
        _defaults[action] = key;
        _bindings[action] = key;
    }

    /// <summary>Get the scancode bound to an action, or SDL_SCANCODE_UNKNOWN if not found.</summary>
    public SDL.SDL_Scancode Get(string action) =>
        _bindings.GetValueOrDefault(action, SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN);

    /// <summary>Alias for Get(). Returns the scancode bound to the given action.</summary>
    public SDL.SDL_Scancode GetBinding(string action) => Get(action);

    public void Rebind(string action, SDL.SDL_Scancode key) =>
        _bindings[action] = key;

    public void ResetToDefault(string action)
    {
        if (_defaults.TryGetValue(action, out var key))
            _bindings[action] = key;
    }

    public void ResetAllToDefaults()
    {
        _bindings.Clear();
        foreach (var (action, key) in _defaults)
            _bindings[action] = key;
    }

    /// <summary>Check if the action's bound key is currently held.</summary>
    public bool IsActionHeld(string action, InputManager input) =>
        input.IsKeyHeld(Get(action));

    /// <summary>Check if the action's bound key was just pressed this frame.</summary>
    public bool IsActionPressed(string action, InputManager input) =>
        input.IsKeyPressed(Get(action));

    public IReadOnlyDictionary<string, SDL.SDL_Scancode> AllBindings => _bindings;

    /// <summary>
    /// Save current bindings to a JSON file. Only writes bindings that differ from defaults.
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var customBindings = new Dictionary<string, int>();
        foreach (var (action, scancode) in _bindings)
        {
            if (!_defaults.TryGetValue(action, out var defaultKey) || defaultKey != scancode)
            {
                customBindings[action] = (int)scancode;
            }
        }

        var json = JsonSerializer.Serialize(customBindings, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Load bindings from a JSON file. Resets to defaults first, then applies overrides
    /// from the file. Invalid or unknown actions are silently ignored.
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        ResetAllToDefaults();

        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        var overrides = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        if (overrides == null)
            return;

        foreach (var (action, scancodeInt) in overrides)
        {
            if (_defaults.ContainsKey(action) && Enum.IsDefined(typeof(SDL.SDL_Scancode), scancodeInt))
            {
                _bindings[action] = (SDL.SDL_Scancode)scancodeInt;
            }
        }
    }
}
