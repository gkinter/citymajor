using Forge.Engine.Rendering;
using Xunit;

namespace Forge.Engine.Tests;

public class EasingTests
{
    /// <summary>
    /// Every easing function must return 0.0 when t=0.
    /// </summary>
    [Theory]
    [InlineData(EaseType.Linear)]
    [InlineData(EaseType.EaseInQuad)]
    [InlineData(EaseType.EaseOutQuad)]
    [InlineData(EaseType.EaseInOutQuad)]
    [InlineData(EaseType.EaseInCubic)]
    [InlineData(EaseType.EaseOutCubic)]
    [InlineData(EaseType.EaseInOutCubic)]
    [InlineData(EaseType.EaseInBack)]
    [InlineData(EaseType.EaseOutBack)]
    [InlineData(EaseType.EaseInOutBack)]
    [InlineData(EaseType.EaseInElastic)]
    [InlineData(EaseType.EaseOutElastic)]
    [InlineData(EaseType.EaseInBounce)]
    [InlineData(EaseType.EaseOutBounce)]
    [InlineData(EaseType.Spring)]
    public void AllEasingFunctions_StartAtZero(EaseType type)
    {
        float result = Easing.Evaluate(type, 0f);
        Assert.InRange(result, -0.001f, 0.001f);
    }

    /// <summary>
    /// Every easing function must return 1.0 when t=1.
    /// </summary>
    [Theory]
    [InlineData(EaseType.Linear)]
    [InlineData(EaseType.EaseInQuad)]
    [InlineData(EaseType.EaseOutQuad)]
    [InlineData(EaseType.EaseInOutQuad)]
    [InlineData(EaseType.EaseInCubic)]
    [InlineData(EaseType.EaseOutCubic)]
    [InlineData(EaseType.EaseInOutCubic)]
    [InlineData(EaseType.EaseInBack)]
    [InlineData(EaseType.EaseOutBack)]
    [InlineData(EaseType.EaseInOutBack)]
    [InlineData(EaseType.EaseInElastic)]
    [InlineData(EaseType.EaseOutElastic)]
    [InlineData(EaseType.EaseInBounce)]
    [InlineData(EaseType.EaseOutBounce)]
    [InlineData(EaseType.Spring)]
    public void AllEasingFunctions_EndAtOne(EaseType type)
    {
        float result = Easing.Evaluate(type, 1f);
        Assert.InRange(result, 0.999f, 1.001f);
    }

    [Fact]
    public void Linear_MidpointIsHalf()
    {
        float result = Easing.Evaluate(EaseType.Linear, 0.5f);
        Assert.Equal(0.5f, result, 3);
    }

    [Fact]
    public void EaseInQuad_MidpointIsQuarter()
    {
        // f(0.5) = 0.5^2 = 0.25
        float result = Easing.Evaluate(EaseType.EaseInQuad, 0.5f);
        Assert.Equal(0.25f, result, 3);
    }

    [Fact]
    public void EaseOutQuad_MidpointIsThreeQuarters()
    {
        // f(0.5) = 0.5 * (2 - 0.5) = 0.75
        float result = Easing.Evaluate(EaseType.EaseOutQuad, 0.5f);
        Assert.Equal(0.75f, result, 3);
    }

    [Fact]
    public void EaseInCubic_MidpointIsOneEighth()
    {
        // f(0.5) = 0.5^3 = 0.125
        float result = Easing.Evaluate(EaseType.EaseInCubic, 0.5f);
        Assert.Equal(0.125f, result, 3);
    }

    [Fact]
    public void EaseOutBack_Overshoots()
    {
        // EaseOutBack should overshoot past 1.0 at some point
        float max = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            float v = Easing.Evaluate(EaseType.EaseOutBack, t);
            if (v > max) max = v;
        }
        Assert.True(max > 1.0f, $"EaseOutBack should overshoot. Max was {max}");
    }

    [Fact]
    public void EaseOutElastic_Overshoots()
    {
        float max = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            float v = Easing.Evaluate(EaseType.EaseOutElastic, t);
            if (v > max) max = v;
        }
        Assert.True(max > 1.0f, $"EaseOutElastic should overshoot. Max was {max}");
    }

    [Fact]
    public void EaseOutBounce_NeverNegative()
    {
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            float v = Easing.Evaluate(EaseType.EaseOutBounce, t);
            Assert.True(v >= -0.001f, $"EaseOutBounce should not go negative at t={t}, got {v}");
        }
    }

    [Fact]
    public void Spring_OscillatesAroundTarget()
    {
        // Spring should cross 1.0 at least once before settling
        bool crossedAbove = false;
        bool crossedBelow = false;
        float prev = 0f;

        for (float t = 0.01f; t < 1f; t += 0.01f)
        {
            float v = Easing.Evaluate(EaseType.Spring, t);
            if (v > 1.0f) crossedAbove = true;
            if (crossedAbove && v < 1.0f) crossedBelow = true;
            prev = v;
        }

        Assert.True(crossedAbove, "Spring should overshoot above 1.0");
    }

    [Fact]
    public void AllEasingFunctions_AreContinuous()
    {
        // No easing function should jump more than 0.3 between t steps of 0.01
        foreach (EaseType type in Enum.GetValues<EaseType>())
        {
            float prev = Easing.Evaluate(type, 0f);
            for (float t = 0.01f; t <= 1f; t += 0.01f)
            {
                float curr = Easing.Evaluate(type, t);
                float delta = MathF.Abs(curr - prev);
                Assert.True(delta < 0.5f,
                    $"{type} is discontinuous at t={t}: delta={delta} (prev={prev}, curr={curr})");
                prev = curr;
            }
        }
    }
}

public class SpriteAnimationTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesAnimation()
    {
        var anim = new SpriteAnimation("test", 0, new[] { 0, 1, 2 }, new[] { 0.1f, 0.1f, 0.1f });

        Assert.Equal("test", anim.Name);
        Assert.Equal(0, anim.AtlasIndex);
        Assert.Equal(3, anim.FrameIndices.Length);
        Assert.False(anim.Loop);
        Assert.False(anim.PingPong);
    }

    [Fact]
    public void Constructor_EmptyFrames_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SpriteAnimation("test", 0, Array.Empty<int>(), Array.Empty<float>()));
    }

    [Fact]
    public void Constructor_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SpriteAnimation("test", 0, new[] { 0, 1 }, new[] { 0.1f }));
    }

    [Fact]
    public void TotalFrameCount_NoPingPong_EqualsFrameCount()
    {
        var anim = new SpriteAnimation("test", 0, new[] { 0, 1, 2, 3 }, new[] { 0.1f, 0.1f, 0.1f, 0.1f });
        Assert.Equal(4, anim.TotalFrameCount);
    }

    [Fact]
    public void TotalFrameCount_PingPong_DoubleMinusTwo()
    {
        var anim = new SpriteAnimation("test", 0, new[] { 0, 1, 2, 3 }, new[] { 0.1f, 0.1f, 0.1f, 0.1f },
            pingPong: true);
        // 4 frames: 0,1,2,3,2,1 = 6 total
        Assert.Equal(6, anim.TotalFrameCount);
    }

    [Fact]
    public void GetFrame_ReturnsCorrectSpriteIndex()
    {
        var anim = new SpriteAnimation("test", 0, new[] { 10, 20, 30 }, new[] { 0.1f, 0.2f, 0.3f });

        var (sprite0, dur0) = anim.GetFrame(0);
        Assert.Equal(10, sprite0);
        Assert.Equal(0.1f, dur0);

        var (sprite1, dur1) = anim.GetFrame(1);
        Assert.Equal(20, sprite1);
        Assert.Equal(0.2f, dur1);

        var (sprite2, dur2) = anim.GetFrame(2);
        Assert.Equal(30, sprite2);
        Assert.Equal(0.3f, dur2);
    }

    [Fact]
    public void GetFrame_PingPong_ReversesCorrectly()
    {
        var anim = new SpriteAnimation("test", 0, new[] { 0, 1, 2 }, new[] { 0.1f, 0.1f, 0.1f },
            pingPong: true);

        // Total = 4: frames are 0,1,2,1
        Assert.Equal(4, anim.TotalFrameCount);

        Assert.Equal(0, anim.GetFrame(0).spriteIndex);
        Assert.Equal(1, anim.GetFrame(1).spriteIndex);
        Assert.Equal(2, anim.GetFrame(2).spriteIndex);
        Assert.Equal(1, anim.GetFrame(3).spriteIndex);
    }
}

public class AnimationPlayerTests
{
    private AnimationPlayer CreatePlayerWithAnim(bool loop = false, bool pingPong = false)
    {
        var player = new AnimationPlayer();
        var anim = new SpriteAnimation("walk", 0,
            new[] { 0, 1, 2, 3 },
            new[] { 0.1f, 0.1f, 0.1f, 0.1f },
            loop, pingPong);
        player.AddAnimation(anim);
        return player;
    }

    [Fact]
    public void InitialState_NotPlaying()
    {
        var player = new AnimationPlayer();
        Assert.False(player.IsPlaying);
        Assert.False(player.IsFinished);
        Assert.Equal(0, player.CurrentFrame);
    }

    [Fact]
    public void Play_StartsAnimation()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");

        Assert.True(player.IsPlaying);
        Assert.False(player.IsFinished);
        Assert.Equal("walk", player.CurrentAnimationName);
        Assert.Equal(0, player.CurrentFrame);
        Assert.Equal(0, player.CurrentSpriteIndex);
    }

    [Fact]
    public void Play_UnknownName_Throws()
    {
        var player = new AnimationPlayer();
        Assert.Throws<KeyNotFoundException>(() => player.Play("nonexistent"));
    }

    [Fact]
    public void Update_AdvancesFrames()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");

        // Each frame is 0.1s, advance 0.15s = should be on frame 1
        player.Update(0.15f);
        Assert.Equal(1, player.CurrentFrame);
        Assert.Equal(1, player.CurrentSpriteIndex);
    }

    [Fact]
    public void Update_NonLooping_FinishesAtLastFrame()
    {
        var player = CreatePlayerWithAnim(loop: false);
        player.Play("walk");

        string? completedName = null;
        player.OnAnimationComplete += name => completedName = name;

        // Advance past all 4 frames (0.4s total)
        player.Update(0.5f);

        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
        Assert.Equal("walk", completedName);
        Assert.Equal(3, player.CurrentFrame); // stays on last frame
    }

    [Fact]
    public void Update_Looping_WrapsAround()
    {
        var player = CreatePlayerWithAnim(loop: true);
        player.Play("walk");

        // Advance past one full cycle (4 frames * 0.1s = 0.4s) + a bit more
        player.Update(0.45f);

        Assert.True(player.IsPlaying);
        Assert.False(player.IsFinished);
        Assert.Equal(0, player.CurrentFrame); // wrapped back to start
    }

    [Fact]
    public void Stop_ResetsToFrameZero()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");
        player.Update(0.15f);
        Assert.Equal(1, player.CurrentFrame);

        player.Stop();
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.CurrentFrame);
    }

    [Fact]
    public void Pause_FreezesCurrent()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");
        player.Update(0.05f);

        player.Pause();
        Assert.False(player.IsPlaying);

        // Update should not advance while paused
        player.Update(1.0f);
        Assert.Equal(0, player.CurrentFrame);

        player.Resume();
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void SetSpeed_AffectsPlaybackRate()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");
        player.SetSpeed(2.0f);

        // At 2x speed, 0.05s real time = 0.1s animation time = 1 frame
        player.Update(0.05f);
        Assert.Equal(1, player.CurrentFrame);
    }

    [Fact]
    public void SetSpeed_Zero_Freezes()
    {
        var player = CreatePlayerWithAnim();
        player.Play("walk");
        player.SetSpeed(0f);

        player.Update(1.0f);
        Assert.Equal(0, player.CurrentFrame);
    }

    [Fact]
    public void CompletionCallback_FiresOnce()
    {
        var player = CreatePlayerWithAnim(loop: false);
        player.Play("walk");

        int callbackCount = 0;
        player.OnAnimationComplete += _ => callbackCount++;

        player.Update(0.5f); // finish
        player.Update(0.5f); // extra updates shouldn't re-fire

        Assert.Equal(1, callbackCount);
    }
}

public class TweenManagerTests
{
    [Fact]
    public void TweenFloat_InterpolatesToTarget()
    {
        var mgr = new TweenManager();
        float value = 0f;
        mgr.TweenFloat(0f, 100f, 1f, EaseType.Linear, v => value = v);

        mgr.Update(0.5f);
        Assert.InRange(value, 49f, 51f); // ~50 at halfway

        mgr.Update(0.5f);
        Assert.InRange(value, 99f, 101f); // ~100 at completion
    }

    [Fact]
    public void TweenFloat_CompletionCallback_Fires()
    {
        var mgr = new TweenManager();
        bool completed = false;
        float value = 0f;
        mgr.TweenFloat(0f, 1f, 0.1f, EaseType.Linear, v => value = v)
           .OnComplete(() => completed = true);

        mgr.Update(0.2f);
        Assert.True(completed);
    }

    [Fact]
    public void TweenFloat_WithDelay_WaitsBeforeStarting()
    {
        var mgr = new TweenManager();
        float value = 0f;
        mgr.TweenFloat(0f, 100f, 1f, EaseType.Linear, v => value = v)
           .SetDelay(0.5f);

        mgr.Update(0.3f); // still in delay
        Assert.Equal(0f, value);

        mgr.Update(0.7f); // 0.5s used for remaining delay, 0.5s for tween
        Assert.InRange(value, 45f, 55f); // approximately halfway through the tween
    }

    [Fact]
    public void TweenFloat_WithLoops_RestartsAfterCompletion()
    {
        var mgr = new TweenManager();
        int completionCount = 0;
        float value = 0f;
        mgr.TweenFloat(0f, 100f, 0.1f, EaseType.Linear, v => value = v)
           .SetLoops(3)
           .OnComplete(() => completionCount++);

        // Run through 3 loops (each 0.1s)
        for (int i = 0; i < 10; i++)
            mgr.Update(0.05f);

        Assert.True(completionCount >= 2, $"Expected at least 2 loop completions, got {completionCount}");
    }

    [Fact]
    public void TweenFloat_Cancel_StopsImmediately()
    {
        var mgr = new TweenManager();
        float value = 0f;
        var tween = mgr.TweenFloat(0f, 100f, 1f, EaseType.Linear, v => value = v);

        mgr.Update(0.3f);
        float valueAtCancel = value;
        tween.Cancel();

        mgr.Update(0.5f);
        Assert.Equal(valueAtCancel, value); // should not have changed after cancel
    }

    [Fact]
    public void CancelAll_StopsAllTweens()
    {
        var mgr = new TweenManager();
        float v1 = 0f, v2 = 0f;
        mgr.TweenFloat(0f, 100f, 1f, EaseType.Linear, v => v1 = v);
        mgr.TweenFloat(0f, 200f, 1f, EaseType.Linear, v => v2 = v);

        Assert.Equal(2, mgr.ActiveCount);

        mgr.CancelAll();
        Assert.Equal(0, mgr.ActiveCount);
    }

    [Fact]
    public void TweenVec2_InterpolatesToTarget()
    {
        var mgr = new TweenManager();
        System.Numerics.Vector2 value = System.Numerics.Vector2.Zero;
        var target = new System.Numerics.Vector2(100f, 200f);

        mgr.TweenVec2(System.Numerics.Vector2.Zero, target, 1f, EaseType.Linear, v => value = v);

        mgr.Update(1.0f); // complete
        Assert.InRange(value.X, 99f, 101f);
        Assert.InRange(value.Y, 199f, 201f);
    }

    [Fact]
    public void TweenColor_InterpolatesChannels()
    {
        var mgr = new TweenManager();
        uint result = 0;

        uint from = 0xFF0000FF; // red, full alpha
        uint to = 0x00FF00FF;   // green, full alpha

        mgr.TweenColor(from, to, 1f, EaseType.Linear, v => result = v);

        mgr.Update(1.0f); // complete
        // Should be approximately 0x00FF00FF
        int r = (int)((result >> 24) & 0xFF);
        int g = (int)((result >> 16) & 0xFF);
        Assert.InRange(r, 0, 5);     // ~0
        Assert.InRange(g, 250, 255); // ~255
    }

    [Fact]
    public void ActiveCount_TracksCorrectly()
    {
        var mgr = new TweenManager();
        Assert.Equal(0, mgr.ActiveCount);

        float v1 = 0f;
        mgr.TweenFloat(0f, 1f, 1f, EaseType.Linear, v => v1 = v);
        Assert.Equal(1, mgr.ActiveCount);

        float v2 = 0f;
        mgr.TweenFloat(0f, 1f, 0.5f, EaseType.Linear, v => v2 = v);
        Assert.Equal(2, mgr.ActiveCount);

        mgr.Update(0.6f); // second tween completes
        mgr.Update(0f);   // cleanup pass
        Assert.Equal(1, mgr.ActiveCount);

        mgr.Update(0.5f); // first tween completes
        mgr.Update(0f);   // cleanup pass
        Assert.Equal(0, mgr.ActiveCount);
    }

    [Fact]
    public void TweenFloat_EaseOutBack_OvershotsThenSettles()
    {
        var mgr = new TweenManager();
        float maxValue = 0f;
        float finalValue = 0f;

        mgr.TweenFloat(0f, 1f, 1f, EaseType.EaseOutBack, v =>
        {
            if (v > maxValue) maxValue = v;
            finalValue = v;
        });

        // Update in small steps
        for (int i = 0; i < 100; i++)
            mgr.Update(0.01f);

        Assert.True(maxValue > 1.0f, "EaseOutBack tween should overshoot");
        Assert.InRange(finalValue, 0.99f, 1.01f);
    }

    [Fact]
    public void TweenFloat_InfiniteLoops_KeepsRunning()
    {
        var mgr = new TweenManager();
        int loopCount = 0;
        float value = 0f;

        mgr.TweenFloat(0f, 1f, 0.1f, EaseType.Linear, v => value = v)
           .SetLoops(-1)
           .OnComplete(() => loopCount++);

        for (int i = 0; i < 50; i++)
            mgr.Update(0.05f);

        Assert.True(loopCount >= 10, $"Infinite loop should have completed many times, got {loopCount}");
        Assert.Equal(1, mgr.ActiveCount); // still running
    }
}
