namespace Forge.Engine.Core;

/// <summary>
/// Manages delta time, game speed, and pause state.
/// </summary>
public sealed class TimeManager
{
    private long _frameCount;
    private double _totalTime;
    private double _unscaledDeltaTime;

    /// <summary>Game speed multiplier: 0 = paused, 1 = normal, 2 = fast, 3 = fastest.</summary>
    public int SpeedLevel { get; private set; } = 1;

    /// <summary>Actual speed multiplier applied to simulation time.</summary>
    public float SpeedMultiplier => SpeedLevel switch
    {
        0 => 0f,
        1 => 1f,
        2 => 2f,
        3 => 4f,
        _ => 1f
    };

    public bool IsPaused => SpeedLevel == 0;

    /// <summary>Unscaled real-time delta (seconds) since last frame.</summary>
    public double UnscaledDeltaTime => _unscaledDeltaTime;

    /// <summary>Game-speed-scaled delta time (seconds).</summary>
    public double DeltaTime => _unscaledDeltaTime * SpeedMultiplier;

    /// <summary>Total elapsed game time in seconds (paused time excluded).</summary>
    public double TotalTime => _totalTime;

    /// <summary>Total frames rendered since startup.</summary>
    public long FrameCount => _frameCount;

    /// <summary>Frames per second (smoothed).</summary>
    public double Fps { get; private set; }

    private double _fpsAccumulator;
    private int _fpsFrameCount;
    private double _fpsTimer;

    public void Update(double realDeltaTime)
    {
        _unscaledDeltaTime = realDeltaTime;
        _totalTime += DeltaTime;
        _frameCount++;

        _fpsAccumulator += realDeltaTime;
        _fpsFrameCount++;
        _fpsTimer += realDeltaTime;

        if (_fpsTimer >= 0.5)
        {
            Fps = _fpsFrameCount / _fpsAccumulator;
            _fpsAccumulator = 0;
            _fpsFrameCount = 0;
            _fpsTimer = 0;
        }
    }

    public void SetSpeed(int level)
    {
        SpeedLevel = System.Math.Clamp(level, 0, 3);
    }

    public void TogglePause()
    {
        if (IsPaused)
            SetSpeed(1);
        else
            SetSpeed(0);
    }

    public void CycleSpeed()
    {
        SetSpeed(SpeedLevel >= 3 ? 0 : SpeedLevel + 1);
    }
}
