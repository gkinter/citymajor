using System.Diagnostics;

namespace Forge.Engine.Core;

/// <summary>
/// Fixed-timestep game loop with variable rendering. Handles game speed
/// (pause/1x/2x/3x) and prevents spiral of death by capping accumulated time.
/// </summary>
public sealed class GameLoop
{
    private readonly Config _config;
    private readonly TimeManager _time;
    private readonly Stopwatch _stopwatch = new();

    private double _accumulator;
    private double _previousTime;
    private bool _running;

    public delegate void UpdateHandler(double fixedDt);
    public delegate void RenderHandler(double alpha);
    public delegate void InputHandler();

    /// <summary>Called once per fixed timestep for simulation updates.</summary>
    public event UpdateHandler? OnFixedUpdate;

    /// <summary>Called once per frame for rendering with interpolation alpha.</summary>
    public event RenderHandler? OnRender;

    /// <summary>Called once per frame before updates for input processing.</summary>
    public event InputHandler? OnProcessInput;

    /// <summary>Called once per frame after input processing, before fixed updates.
    /// Use this for game input handling (tool clicks, keyboard shortcuts) that must
    /// run exactly once per frame to avoid missed or duplicate input events.</summary>
    public event Action? OnInputProcessed;

    /// <summary>Called once per frame for ImGui and other overlay rendering.</summary>
    public event Action? OnRenderUI;

    /// <summary>Called once per frame at the very end (swap buffers, etc.).</summary>
    public event Action? OnFrameEnd;

    public GameLoop(Config config, TimeManager time)
    {
        _config = config;
        _time = time;
    }

    public void Run()
    {
        _running = true;
        _stopwatch.Start();
        _previousTime = _stopwatch.Elapsed.TotalSeconds;

        while (_running)
        {
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double frameTime = currentTime - _previousTime;
            _previousTime = currentTime;

            // Clamp frame time to prevent spiral of death (e.g., after breakpoint)
            if (frameTime > 0.25)
                frameTime = 0.25;

            _time.Update(frameTime);

            // Process input once per frame
            OnProcessInput?.Invoke();

            // Per-frame input handling (tool clicks, shortcuts) — runs exactly once
            // per frame so transient input states (pressed/released) are never missed
            OnInputProcessed?.Invoke();

            // Accumulate time scaled by game speed
            double scaledFrameTime = frameTime * _time.SpeedMultiplier;
            _accumulator += scaledFrameTime;

            double fixedDt = _config.FixedTimestep;
            int updates = 0;

            // Fixed timestep updates
            while (_accumulator >= fixedDt && updates < _config.MaxUpdatesPerFrame)
            {
                OnFixedUpdate?.Invoke(fixedDt);
                _accumulator -= fixedDt;
                updates++;
            }

            // If we hit the cap, discard remaining accumulator to prevent spiral
            if (updates >= _config.MaxUpdatesPerFrame)
                _accumulator = 0;

            // Interpolation alpha for smooth rendering between fixed steps
            double alpha = _accumulator / fixedDt;

            OnRender?.Invoke(alpha);
            OnRenderUI?.Invoke();
            OnFrameEnd?.Invoke();
        }
    }

    public void Stop()
    {
        _running = false;
    }
}
