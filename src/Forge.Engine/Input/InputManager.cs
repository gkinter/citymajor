using SDL2;

namespace Forge.Engine.Input;

/// <summary>
/// Keyboard, mouse, and gamepad input abstraction. Tracks current and previous
/// frame state for press/release detection.
/// </summary>
public sealed class InputManager
{
    private readonly HashSet<SDL.SDL_Scancode> _keysDown = new();
    private readonly HashSet<SDL.SDL_Scancode> _keysPressed = new();  // Just pressed this frame
    private readonly HashSet<SDL.SDL_Scancode> _keysReleased = new(); // Just released this frame
    private readonly HashSet<SDL.SDL_Scancode> _prevKeysDown = new();

    private readonly HashSet<byte> _mouseDown = new();
    private readonly HashSet<byte> _mousePressed = new();
    private readonly HashSet<byte> _mouseReleased = new();
    private readonly HashSet<byte> _prevMouseDown = new();

    private int _mouseX;
    private int _mouseY;
    private int _mouseRelX;
    private int _mouseRelY;
    private int _scrollDelta;

    public int MouseX => _mouseX;
    public int MouseY => _mouseY;
    public int MouseRelX => _mouseRelX;
    public int MouseRelY => _mouseRelY;
    public int ScrollDelta => _scrollDelta;

    /// <summary>Call at the start of each frame, before processing events.</summary>
    public void BeginFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mousePressed.Clear();
        _mouseReleased.Clear();
        _mouseRelX = 0;
        _mouseRelY = 0;
        _scrollDelta = 0;
    }

    /// <summary>Call at the end of event processing to finalize state.</summary>
    public void EndFrame()
    {
        // Detect just-pressed and just-released keys
        foreach (var key in _keysDown)
        {
            if (!_prevKeysDown.Contains(key))
                _keysPressed.Add(key);
        }
        foreach (var key in _prevKeysDown)
        {
            if (!_keysDown.Contains(key))
                _keysReleased.Add(key);
        }

        // Same for mouse
        foreach (var btn in _mouseDown)
        {
            if (!_prevMouseDown.Contains(btn))
                _mousePressed.Add(btn);
        }
        foreach (var btn in _prevMouseDown)
        {
            if (!_mouseDown.Contains(btn))
                _mouseReleased.Add(btn);
        }

        _prevKeysDown.Clear();
        foreach (var k in _keysDown)
            _prevKeysDown.Add(k);

        _prevMouseDown.Clear();
        foreach (var b in _mouseDown)
            _prevMouseDown.Add(b);
    }

    // Event handlers called by ForgeApp
    public void OnKeyDown(SDL.SDL_Scancode scancode) => _keysDown.Add(scancode);
    public void OnKeyUp(SDL.SDL_Scancode scancode) => _keysDown.Remove(scancode);

    public void OnMouseMove(int x, int y, int relX, int relY)
    {
        _mouseX = x;
        _mouseY = y;
        _mouseRelX += relX;
        _mouseRelY += relY;
    }

    public void OnMouseDown(byte button) => _mouseDown.Add(button);
    public void OnMouseUp(byte button) => _mouseDown.Remove(button);
    public void OnMouseWheel(int delta) => _scrollDelta += delta;

    // Query methods
    public bool IsKeyHeld(SDL.SDL_Scancode key) => _keysDown.Contains(key);
    public bool IsKeyPressed(SDL.SDL_Scancode key) => _keysPressed.Contains(key);
    public bool IsKeyReleased(SDL.SDL_Scancode key) => _keysReleased.Contains(key);

    public bool IsMouseHeld(byte button) => _mouseDown.Contains(button);
    public bool IsMousePressed(byte button) => _mousePressed.Contains(button);
    public bool IsMouseReleased(byte button) => _mouseReleased.Contains(button);

    // Convenience aliases
    public bool IsLeftMouseHeld => IsMouseHeld((byte)SDL.SDL_BUTTON_LEFT);
    public bool IsRightMouseHeld => IsMouseHeld((byte)SDL.SDL_BUTTON_RIGHT);
    public bool IsMiddleMouseHeld => IsMouseHeld((byte)SDL.SDL_BUTTON_MIDDLE);
    public bool IsLeftMousePressed => IsMousePressed((byte)SDL.SDL_BUTTON_LEFT);
    public bool IsRightMousePressed => IsMousePressed((byte)SDL.SDL_BUTTON_RIGHT);
}
