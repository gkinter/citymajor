using Forge.Engine.Input;
using SDL2;
using Xunit;

namespace Forge.Engine.Tests;

public class KeyBindingsTests
{
    [Fact]
    public void DefaultBindings_ExistForCommonActions()
    {
        var kb = new KeyBindings();

        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("pan_left"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("pan_right"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("pan_up"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("pan_down"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("pause"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("cancel"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("bulldoze"));
        Assert.NotEqual(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("road_tool"));
    }

    [Fact]
    public void DefaultBindings_CorrectKeys()
    {
        var kb = new KeyBindings();

        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_A, kb.Get("pan_left"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_D, kb.Get("pan_right"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_W, kb.Get("pan_up"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_S, kb.Get("pan_down"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_SPACE, kb.Get("pause"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE, kb.Get("cancel"));
    }

    [Fact]
    public void Get_UnknownAction_ReturnsUnknown()
    {
        var kb = new KeyBindings();
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN, kb.Get("nonexistent_action"));
    }

    [Fact]
    public void Rebind_OverwritesOldBinding()
    {
        var kb = new KeyBindings();

        kb.Rebind("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_LEFT);

        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_LEFT, kb.Get("pan_left"));
    }

    [Fact]
    public void Rebind_DoesNotAffectOtherBindings()
    {
        var kb = new KeyBindings();
        var originalPanRight = kb.Get("pan_right");

        kb.Rebind("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_LEFT);

        Assert.Equal(originalPanRight, kb.Get("pan_right"));
    }

    [Fact]
    public void ResetToDefault_RestoresSingleBinding()
    {
        var kb = new KeyBindings();
        var original = kb.Get("pan_left");

        kb.Rebind("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_LEFT);
        Assert.NotEqual(original, kb.Get("pan_left"));

        kb.ResetToDefault("pan_left");
        Assert.Equal(original, kb.Get("pan_left"));
    }

    [Fact]
    public void ResetAllToDefaults_RestoresEverything()
    {
        var kb = new KeyBindings();

        // Store originals
        var origLeft = kb.Get("pan_left");
        var origRight = kb.Get("pan_right");

        // Rebind several
        kb.Rebind("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_LEFT);
        kb.Rebind("pan_right", SDL.SDL_Scancode.SDL_SCANCODE_RIGHT);

        kb.ResetAllToDefaults();

        Assert.Equal(origLeft, kb.Get("pan_left"));
        Assert.Equal(origRight, kb.Get("pan_right"));
    }

    [Fact]
    public void AllBindings_ReturnsAllDefaults()
    {
        var kb = new KeyBindings();
        var all = kb.AllBindings;

        Assert.True(all.Count >= 18, $"Expected >= 18 default bindings, got {all.Count}");
        Assert.True(all.ContainsKey("pan_left"));
        Assert.True(all.ContainsKey("quicksave"));
        Assert.True(all.ContainsKey("quickload"));
    }

    [Fact]
    public void Rebind_NewAction_AddsBinding()
    {
        var kb = new KeyBindings();

        kb.Rebind("custom_action", SDL.SDL_Scancode.SDL_SCANCODE_F12);

        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_F12, kb.Get("custom_action"));
    }

    [Fact]
    public void ResetToDefault_UnknownAction_DoesNotThrow()
    {
        var kb = new KeyBindings();
        kb.ResetToDefault("nonexistent"); // Should not throw
    }

    [Fact]
    public void SpeedBindings_DefaultsExist()
    {
        var kb = new KeyBindings();

        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_F1, kb.Get("speed_cycle"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_1, kb.Get("speed_1"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_2, kb.Get("speed_2"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_3, kb.Get("speed_3"));
    }

    [Fact]
    public void Rebind_SameKeyTwoActions_BothHaveSameKey()
    {
        var kb = new KeyBindings();

        kb.Rebind("pan_left", SDL.SDL_Scancode.SDL_SCANCODE_X);
        kb.Rebind("pan_right", SDL.SDL_Scancode.SDL_SCANCODE_X);

        // Both should map to X (no conflict prevention at this level)
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_X, kb.Get("pan_left"));
        Assert.Equal(SDL.SDL_Scancode.SDL_SCANCODE_X, kb.Get("pan_right"));
    }
}
