using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class TimeManagerTests
{
    [Fact]
    public void InitialState_SpeedLevel1_NotPaused()
    {
        var tm = new TimeManager();

        Assert.Equal(1, tm.SpeedLevel);
        Assert.False(tm.IsPaused);
        Assert.Equal(1f, tm.SpeedMultiplier);
        Assert.Equal(0, tm.FrameCount);
        Assert.Equal(0.0, tm.TotalTime);
    }

    [Fact]
    public void TogglePause_PausesAndUnpauses()
    {
        var tm = new TimeManager();

        tm.TogglePause();
        Assert.True(tm.IsPaused);
        Assert.Equal(0, tm.SpeedLevel);
        Assert.Equal(0f, tm.SpeedMultiplier);

        tm.TogglePause();
        Assert.False(tm.IsPaused);
        Assert.Equal(1, tm.SpeedLevel);
    }

    [Fact]
    public void CycleSpeed_CyclesThrough0123()
    {
        var tm = new TimeManager();

        // Start at 1
        Assert.Equal(1, tm.SpeedLevel);

        tm.CycleSpeed(); // 1 -> 2
        Assert.Equal(2, tm.SpeedLevel);

        tm.CycleSpeed(); // 2 -> 3
        Assert.Equal(3, tm.SpeedLevel);

        tm.CycleSpeed(); // 3 -> 0 (pause)
        Assert.Equal(0, tm.SpeedLevel);

        tm.CycleSpeed(); // 0 -> 1
        Assert.Equal(1, tm.SpeedLevel);
    }

    [Fact]
    public void SpeedMultiplier_ReturnsCorrectValues()
    {
        var tm = new TimeManager();

        tm.SetSpeed(0);
        Assert.Equal(0f, tm.SpeedMultiplier);

        tm.SetSpeed(1);
        Assert.Equal(1f, tm.SpeedMultiplier);

        tm.SetSpeed(2);
        Assert.Equal(2f, tm.SpeedMultiplier);

        tm.SetSpeed(3);
        Assert.Equal(4f, tm.SpeedMultiplier);
    }

    [Fact]
    public void SetSpeed_ClampsToRange()
    {
        var tm = new TimeManager();

        tm.SetSpeed(-5);
        Assert.Equal(0, tm.SpeedLevel);

        tm.SetSpeed(100);
        Assert.Equal(3, tm.SpeedLevel);
    }

    [Fact]
    public void Update_IncrementsFrameCount()
    {
        var tm = new TimeManager();

        tm.Update(0.016);
        Assert.Equal(1, tm.FrameCount);

        tm.Update(0.016);
        Assert.Equal(2, tm.FrameCount);
    }

    [Fact]
    public void Update_AccumulatesTotalTime()
    {
        var tm = new TimeManager();
        tm.SetSpeed(1);

        tm.Update(1.0);
        Assert.Equal(1.0, tm.TotalTime, precision: 5);

        tm.Update(0.5);
        Assert.Equal(1.5, tm.TotalTime, precision: 5);
    }

    [Fact]
    public void Update_TotalTimeScaledBySpeed()
    {
        var tm = new TimeManager();
        tm.SetSpeed(2);

        tm.Update(1.0);

        // Speed 2 = 2x multiplier, so 1.0 real second = 2.0 game seconds
        Assert.Equal(2.0, tm.TotalTime, precision: 5);
    }

    [Fact]
    public void Update_PausedDoesNotAccumulateTime()
    {
        var tm = new TimeManager();
        tm.SetSpeed(0);

        tm.Update(1.0);

        Assert.Equal(0.0, tm.TotalTime);
        Assert.Equal(1, tm.FrameCount); // Frame still counts
    }

    [Fact]
    public void DeltaTime_ScaledByMultiplier()
    {
        var tm = new TimeManager();
        tm.SetSpeed(3); // 4x multiplier

        tm.Update(0.016);

        Assert.Equal(0.016, tm.UnscaledDeltaTime, precision: 5);
        Assert.Equal(0.016 * 4.0, tm.DeltaTime, precision: 5);
    }

    [Fact]
    public void Fps_CalculatedAfterHalfSecond()
    {
        var tm = new TimeManager();

        // Simulate 30 frames at ~60fps (0.0167s each)
        for (int i = 0; i < 30; i++)
        {
            tm.Update(0.0167);
        }

        // After ~0.5s of frames, FPS should be calculated
        // 30 frames / 0.5s = ~60fps
        Assert.True(tm.Fps > 50 && tm.Fps < 70,
            $"Expected FPS around 60, got {tm.Fps:F1}");
    }

    [Fact]
    public void Fps_ZeroBeforeFirstCalculation()
    {
        var tm = new TimeManager();

        // Only a couple of frames, not enough for FPS calc (< 0.5s)
        tm.Update(0.016);
        tm.Update(0.016);

        Assert.Equal(0.0, tm.Fps);
    }

    [Fact]
    public void Fps_RecalculatesEachHalfSecond()
    {
        var tm = new TimeManager();

        // First window: 60 frames at ~100fps (0.01s each = 0.6s total, crosses 0.5 threshold)
        for (int i = 0; i < 60; i++)
            tm.Update(0.01);

        double firstFps = tm.Fps;
        Assert.True(firstFps > 80, $"Expected first FPS > 80, got {firstFps:F1}");

        // Second window: 12 frames at ~20fps (0.05s each = 0.6s total, crosses 0.5 threshold)
        for (int i = 0; i < 12; i++)
            tm.Update(0.05);

        double secondFps = tm.Fps;
        Assert.True(secondFps > 0, $"Expected second FPS > 0, got {secondFps:F1}");
        // secondFps should be ~20, firstFps should be ~100
        Assert.True(secondFps < firstFps * 0.5,
            $"Expected second FPS ({secondFps:F1}) significantly less than first FPS ({firstFps:F1})");
    }

    [Fact]
    public void UnscaledDeltaTime_ReflectsLastUpdate()
    {
        var tm = new TimeManager();

        tm.Update(0.016);
        Assert.Equal(0.016, tm.UnscaledDeltaTime, precision: 5);

        tm.Update(0.033);
        Assert.Equal(0.033, tm.UnscaledDeltaTime, precision: 5);
    }
}
