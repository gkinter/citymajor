using System.Numerics;
using System.Runtime.CompilerServices;

namespace Forge.Engine.Rendering;

/// <summary>
/// Defines which easing function to apply to a tween interpolation.
/// All 15 functions are mathematically correct: f(0)=0, f(1)=1 (for non-overshoot types).
/// </summary>
public enum EaseType
{
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,
    EaseInElastic,
    EaseOutElastic,
    EaseInBounce,
    EaseOutBounce,
    Spring
}

/// <summary>
/// Static helper with all 15 easing function implementations.
/// Each function maps t in [0,1] to a curved output.
/// Back/Elastic/Spring may overshoot outside [0,1] mid-curve but always land at f(0)=0, f(1)=1.
/// </summary>
public static class Easing
{
    private const float Pi = MathF.PI;
    private const float HalfPi = MathF.PI * 0.5f;
    private const float BackOvershoot = 1.70158f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(EaseType type, float t)
    {
        return type switch
        {
            EaseType.Linear => t,
            EaseType.EaseInQuad => t * t,
            EaseType.EaseOutQuad => t * (2f - t),
            EaseType.EaseInOutQuad => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t,
            EaseType.EaseInCubic => t * t * t,
            EaseType.EaseOutCubic => EaseOutCubicImpl(t),
            EaseType.EaseInOutCubic => EaseInOutCubicImpl(t),
            EaseType.EaseInBack => t * t * ((BackOvershoot + 1f) * t - BackOvershoot),
            EaseType.EaseOutBack => EaseOutBackImpl(t),
            EaseType.EaseInOutBack => EaseInOutBackImpl(t),
            EaseType.EaseInElastic => EaseInElasticImpl(t),
            EaseType.EaseOutElastic => EaseOutElasticImpl(t),
            EaseType.EaseInBounce => 1f - EaseOutBounceImpl(1f - t),
            EaseType.EaseOutBounce => EaseOutBounceImpl(t),
            EaseType.Spring => SpringImpl(t),
            _ => t,
        };
    }

    private static float EaseOutCubicImpl(float t)
    {
        float f = t - 1f;
        return f * f * f + 1f;
    }

    private static float EaseInOutCubicImpl(float t)
    {
        if (t < 0.5f)
            return 4f * t * t * t;
        float f = 2f * t - 2f;
        return 0.5f * f * f * f + 1f;
    }

    private static float EaseOutBackImpl(float t)
    {
        float f = t - 1f;
        return f * f * ((BackOvershoot + 1f) * f + BackOvershoot) + 1f;
    }

    private static float EaseInOutBackImpl(float t)
    {
        const float s = BackOvershoot * 1.525f;
        if (t < 0.5f)
        {
            float f = 2f * t;
            return 0.5f * (f * f * ((s + 1f) * f - s));
        }
        else
        {
            float f = 2f * t - 2f;
            return 0.5f * (f * f * ((s + 1f) * f + s) + 2f);
        }
    }

    private static float EaseInElasticImpl(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * (2f * Pi / 3f));
    }

    private static float EaseOutElasticImpl(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (2f * Pi / 3f)) + 1f;
    }

    private static float EaseOutBounceImpl(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }
        else
        {
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }

    /// <summary>
    /// Damped spring: overshoots then settles at 1.0.
    /// Uses a decaying sinusoid: 1 - e^(-6t) * cos(4.5 * pi * t)
    /// </summary>
    private static float SpringImpl(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return 1f - MathF.Exp(-6f * t) * MathF.Cos(4.5f * Pi * t);
    }
}

// ─── Sprite Animation (frame-based) ─────────────────────────────────────────

/// <summary>
/// Defines a named sprite animation with per-frame timing, loop, and ping-pong support.
/// Frame indices reference sprite positions within a texture atlas layer.
/// </summary>
public sealed class SpriteAnimation
{
    /// <summary>Human-readable name for lookup (e.g., "building_construct", "vehicle_drive").</summary>
    public string Name;

    /// <summary>Which texture array layer this animation's sprites live on.</summary>
    public int AtlasIndex;

    /// <summary>Sprite indices per frame (references into the atlas region grid).</summary>
    public int[] FrameIndices;

    /// <summary>Duration of each frame in seconds. Variable timing allows weight/anticipation effects.</summary>
    public float[] FrameDurations;

    /// <summary>Whether the animation loops when it reaches the last frame.</summary>
    public bool Loop;

    /// <summary>If true, plays forward then backward (1-2-3-2-1) before looping or finishing.</summary>
    public bool PingPong;

    public SpriteAnimation(string name, int atlasIndex, int[] frameIndices, float[] frameDurations,
                           bool loop = false, bool pingPong = false)
    {
        if (frameIndices.Length == 0)
            throw new ArgumentException("Animation must have at least one frame.", nameof(frameIndices));
        if (frameIndices.Length != frameDurations.Length)
            throw new ArgumentException("Frame indices and durations must have the same length.");

        Name = name;
        AtlasIndex = atlasIndex;
        FrameIndices = frameIndices;
        FrameDurations = frameDurations;
        Loop = loop;
        PingPong = pingPong;
    }

    /// <summary>Total number of frames including ping-pong reversal frames (if applicable).</summary>
    public int TotalFrameCount => PingPong && FrameIndices.Length > 1
        ? FrameIndices.Length * 2 - 2
        : FrameIndices.Length;

    /// <summary>
    /// Get the sprite index and frame duration for a logical frame index (handles ping-pong mapping).
    /// </summary>
    public (int spriteIndex, float duration) GetFrame(int logicalFrame)
    {
        int baseCount = FrameIndices.Length;

        if (!PingPong || baseCount <= 1)
        {
            int idx = logicalFrame % baseCount;
            return (FrameIndices[idx], FrameDurations[idx]);
        }

        int total = TotalFrameCount;
        int wrapped = logicalFrame % total;

        if (wrapped < baseCount)
        {
            return (FrameIndices[wrapped], FrameDurations[wrapped]);
        }
        else
        {
            int reverseIdx = total - wrapped;
            return (FrameIndices[reverseIdx], FrameDurations[reverseIdx]);
        }
    }
}

/// <summary>
/// Plays a SpriteAnimation with speed control, pause/resume, and completion callbacks.
/// Tracks the current frame, accumulated time, and exposes the current sprite index
/// for the renderer to use.
/// </summary>
public sealed class AnimationPlayer
{
    private readonly Dictionary<string, SpriteAnimation> _animations = new();
    private SpriteAnimation? _current;
    private int _logicalFrame;
    private float _frameTimer;
    private float _speed = 1f;
    private bool _isPlaying;
    private bool _isFinished;
    private bool _isPaused;

    /// <summary>Current logical frame index within the animation.</summary>
    public int CurrentFrame => _logicalFrame;

    /// <summary>Sprite index in the atlas for the current frame (ready for rendering).</summary>
    public int CurrentSpriteIndex
    {
        get
        {
            if (_current == null) return 0;
            var (sprite, _) = _current.GetFrame(_logicalFrame);
            return sprite;
        }
    }

    /// <summary>Atlas layer index of the current animation.</summary>
    public int CurrentAtlasIndex => _current?.AtlasIndex ?? 0;

    /// <summary>Whether an animation is actively playing (not paused, not finished).</summary>
    public bool IsPlaying => _isPlaying && !_isPaused;

    /// <summary>Whether the current animation has finished (only true for non-looping animations).</summary>
    public bool IsFinished => _isFinished;

    /// <summary>Name of the currently loaded animation, or null if none.</summary>
    public string? CurrentAnimationName => _current?.Name;

    /// <summary>Fired when a non-looping animation completes. Parameter is the animation name.</summary>
    public event Action<string>? OnAnimationComplete;

    /// <summary>Register an animation by name for later playback.</summary>
    public void AddAnimation(SpriteAnimation animation)
    {
        _animations[animation.Name] = animation;
    }

    /// <summary>Start playing a named animation from the beginning.</summary>
    public void Play(string animationName)
    {
        if (!_animations.TryGetValue(animationName, out var anim))
            throw new KeyNotFoundException($"Animation '{animationName}' not registered.");

        _current = anim;
        _logicalFrame = 0;
        _frameTimer = 0f;
        _isPlaying = true;
        _isFinished = false;
        _isPaused = false;
    }

    /// <summary>Stop the current animation and reset to frame 0.</summary>
    public void Stop()
    {
        _isPlaying = false;
        _isPaused = false;
        _logicalFrame = 0;
        _frameTimer = 0f;
    }

    /// <summary>Pause the current animation at the current frame.</summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>Resume a paused animation.</summary>
    public void Resume()
    {
        _isPaused = false;
    }

    /// <summary>Set the playback speed multiplier. 1.0 = normal, 2.0 = double speed.</summary>
    public void SetSpeed(float multiplier)
    {
        _speed = MathF.Max(0f, multiplier);
    }

    /// <summary>
    /// Advance the animation by dt seconds. Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        if (!_isPlaying || _isPaused || _isFinished || _current == null)
            return;

        _frameTimer += dt * _speed;

        var (_, frameDuration) = _current.GetFrame(_logicalFrame);

        while (_frameTimer >= frameDuration && !_isFinished)
        {
            _frameTimer -= frameDuration;
            _logicalFrame++;

            int totalFrames = _current.TotalFrameCount;

            if (_logicalFrame >= totalFrames)
            {
                if (_current.Loop)
                {
                    _logicalFrame = 0;
                }
                else
                {
                    _logicalFrame = totalFrames - 1;
                    _isFinished = true;
                    _isPlaying = false;
                    OnAnimationComplete?.Invoke(_current.Name);
                    return;
                }
            }

            (_, frameDuration) = _current.GetFrame(_logicalFrame);
        }
    }
}

// ─── Tween System (value interpolation) ─────────────────────────────────────

/// <summary>
/// Represents a single active tween that interpolates a value over time.
/// Supports delay, looping, completion callbacks, and cancellation.
/// </summary>
public sealed class Tween
{
    internal int Id;
    internal float StartValue;
    internal float EndValue;
    internal float Duration;
    internal float Elapsed;
    internal float Delay;
    internal float DelayRemaining;
    internal EaseType Ease;
    internal int LoopCount;      // -1 = infinite, 0 = done, >0 = remaining
    internal int TotalLoops;     // original loop setting
    internal bool IsCancelled;
    internal bool IsComplete;
    internal Action? CompletionCallback;

    // For multi-component tweens (Vec2 = 2 channels, Color = 4 channels)
    internal int ChannelCount;
    internal float[] StartValues;
    internal float[] EndValues;
    internal float[]? TargetArray;  // the array being modified (for multi-channel)
    internal int TargetOffset;      // offset into the target array

    // For single-float tweens using a delegate to write back
    internal Action<float>? SingleWriter;

    internal Tween()
    {
        StartValues = Array.Empty<float>();
        EndValues = Array.Empty<float>();
    }

    /// <summary>Register a callback to invoke when the tween completes (or each loop iteration).</summary>
    public Tween OnComplete(Action callback)
    {
        CompletionCallback = callback;
        return this;
    }

    /// <summary>Set an initial delay in seconds before the tween begins interpolating.</summary>
    public Tween SetDelay(float seconds)
    {
        Delay = MathF.Max(0f, seconds);
        DelayRemaining = Delay;
        return this;
    }

    /// <summary>Set the number of loops. -1 = infinite, 0 or 1 = play once.</summary>
    public Tween SetLoops(int count)
    {
        LoopCount = count <= 0 && count != -1 ? 0 : count;
        TotalLoops = LoopCount;
        return this;
    }

    /// <summary>Cancel this tween immediately. The value stays at its current interpolated position.</summary>
    public void Cancel()
    {
        IsCancelled = true;
    }
}

/// <summary>
/// Manages a pool of active tweens. Supports float, Vector2, and uint color interpolation.
/// Call Update(dt) each frame to advance all tweens. Thread-safe for reads of ActiveCount.
/// </summary>
public sealed class TweenManager
{
    private readonly List<Tween> _tweens = new();
    private int _nextId;

    /// <summary>Number of currently active (not cancelled, not complete) tweens.</summary>
    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _tweens.Count; i++)
            {
                if (!_tweens[i].IsCancelled && !_tweens[i].IsComplete)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Tween a float value from its current value to a target over duration seconds.
    /// The writer action is called each frame with the interpolated value.
    /// </summary>
    public Tween TweenFloat(float current, float target, float duration, EaseType ease, Action<float> writer)
    {
        var tween = CreateTween(duration, ease);
        tween.ChannelCount = 1;
        tween.StartValues = new[] { current };
        tween.EndValues = new[] { target };
        tween.SingleWriter = writer;
        _tweens.Add(tween);
        return tween;
    }

    /// <summary>
    /// Tween a Vector2 from current to target over duration seconds.
    /// The writer action is called each frame with the interpolated value.
    /// </summary>
    public Tween TweenVec2(Vector2 current, Vector2 target, float duration, EaseType ease, Action<Vector2> writer)
    {
        var tween = CreateTween(duration, ease);
        tween.ChannelCount = 2;
        tween.StartValues = new[] { current.X, current.Y };
        tween.EndValues = new[] { target.X, target.Y };

        // Wrap the Vector2 writer into the single-writer path using a closure
        tween.SingleWriter = null;
        tween.TargetArray = new float[2];
        tween.TargetOffset = 0;

        // Store the vec2 writer in the completion callback slot? No -- use a dedicated path.
        // We'll use a special approach: write to TargetArray, then call the writer from Update.
        // Store the writer as a tagged object. Simplify: just use the TargetArray approach.
        var arr = tween.TargetArray;
        arr[0] = current.X;
        arr[1] = current.Y;

        // We need to call the writer each frame. Use a helper.
        tween.SingleWriter = _ => writer(new Vector2(arr[0], arr[1]));

        _tweens.Add(tween);
        return tween;
    }

    /// <summary>
    /// Tween a packed RGBA color (0xRRGGBBAA) from current to target over duration seconds.
    /// Interpolation happens in linear color space per-channel.
    /// </summary>
    public Tween TweenColor(uint current, uint target, float duration, EaseType ease, Action<uint> writer)
    {
        UnpackColor(current, out float cr, out float cg, out float cb, out float ca);
        UnpackColor(target, out float tr, out float tg, out float tb, out float ta);

        var tween = CreateTween(duration, ease);
        tween.ChannelCount = 4;
        tween.StartValues = new[] { cr, cg, cb, ca };
        tween.EndValues = new[] { tr, tg, tb, ta };
        tween.TargetArray = new float[4];
        tween.TargetOffset = 0;

        var arr = tween.TargetArray;
        tween.SingleWriter = _ =>
        {
            uint packed = PackColor(arr[0], arr[1], arr[2], arr[3]);
            writer(packed);
        };

        _tweens.Add(tween);
        return tween;
    }

    /// <summary>
    /// Advance all active tweens by dt seconds. Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        for (int i = _tweens.Count - 1; i >= 0; i--)
        {
            var tween = _tweens[i];

            if (tween.IsCancelled)
            {
                _tweens.RemoveAt(i);
                continue;
            }

            if (tween.IsComplete)
            {
                _tweens.RemoveAt(i);
                continue;
            }

            // Handle delay
            if (tween.DelayRemaining > 0f)
            {
                tween.DelayRemaining -= dt;
                if (tween.DelayRemaining > 0f)
                    continue;

                // Consume leftover time
                dt = -tween.DelayRemaining;
                tween.DelayRemaining = 0f;
            }

            tween.Elapsed += dt;

            bool finished = tween.Elapsed >= tween.Duration;
            float t = finished ? 1f : tween.Elapsed / tween.Duration;
            float eased = Easing.Evaluate(tween.Ease, t);

            // Interpolate all channels
            if (tween.TargetArray != null)
            {
                for (int c = 0; c < tween.ChannelCount; c++)
                {
                    tween.TargetArray[tween.TargetOffset + c] =
                        tween.StartValues[c] + (tween.EndValues[c] - tween.StartValues[c]) * eased;
                }
                // Call the writer with a dummy value; the writer reads from TargetArray
                tween.SingleWriter?.Invoke(0f);
            }
            else if (tween.SingleWriter != null && tween.ChannelCount == 1)
            {
                float val = tween.StartValues[0] + (tween.EndValues[0] - tween.StartValues[0]) * eased;
                tween.SingleWriter(val);
            }

            if (finished)
            {
                // Handle looping
                if (tween.LoopCount == -1 || tween.LoopCount > 1)
                {
                    if (tween.LoopCount > 1)
                        tween.LoopCount--;
                    tween.Elapsed = 0f;
                    tween.DelayRemaining = tween.Delay;
                    tween.CompletionCallback?.Invoke();
                }
                else
                {
                    tween.IsComplete = true;
                    tween.CompletionCallback?.Invoke();
                }
            }
        }
    }

    /// <summary>Cancel all active tweens immediately.</summary>
    public void CancelAll()
    {
        for (int i = 0; i < _tweens.Count; i++)
            _tweens[i].IsCancelled = true;
        _tweens.Clear();
    }

    private Tween CreateTween(float duration, EaseType ease)
    {
        return new Tween
        {
            Id = _nextId++,
            Duration = MathF.Max(0.001f, duration),
            Ease = ease,
            LoopCount = 0,
            TotalLoops = 0,
        };
    }

    private static void UnpackColor(uint packed, out float r, out float g, out float b, out float a)
    {
        r = ((packed >> 24) & 0xFF) / 255f;
        g = ((packed >> 16) & 0xFF) / 255f;
        b = ((packed >> 8) & 0xFF) / 255f;
        a = (packed & 0xFF) / 255f;
    }

    private static uint PackColor(float r, float g, float b, float a)
    {
        uint ri = (uint)System.Math.Clamp((int)(r * 255f + 0.5f), 0, 255);
        uint gi = (uint)System.Math.Clamp((int)(g * 255f + 0.5f), 0, 255);
        uint bi = (uint)System.Math.Clamp((int)(b * 255f + 0.5f), 0, 255);
        uint ai = (uint)System.Math.Clamp((int)(a * 255f + 0.5f), 0, 255);
        return (ri << 24) | (gi << 16) | (bi << 8) | ai;
    }
}
