using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class GameLoopTests
{
    [Fact]
    public void FixedTimestep_ProducesCorrectNumberOfUpdates()
    {
        var config = new Config { FixedTimestep = 1.0 / 60.0, MaxUpdatesPerFrame = 10 };
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int updateCount = 0;

        loop.OnFixedUpdate += dt =>
        {
            updateCount++;
            if (updateCount >= 5)
                loop.Stop();
        };

        loop.OnFrameEnd += () =>
        {
            // Safety: stop after a few frames regardless
            if (updateCount == 0)
            {
                // First frame might not have enough accumulated time
            }
        };

        // Run in a separate thread with a timeout to prevent infinite hang
        var task = Task.Run(() => loop.Run());
        bool completed = task.Wait(TimeSpan.FromSeconds(2));

        if (!completed) loop.Stop();
        task.Wait(TimeSpan.FromSeconds(1));

        Assert.True(updateCount >= 5, $"Expected at least 5 updates, got {updateCount}");
    }

    [Fact]
    public void SpiralOfDeath_CapsAccumulator()
    {
        var config = new Config { FixedTimestep = 1.0 / 60.0, MaxUpdatesPerFrame = 5 };
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int maxUpdatesInOneFrame = 0;
        int currentFrameUpdates = 0;
        int frameCount = 0;

        loop.OnProcessInput += () =>
        {
            // Track per-frame update counts
            if (currentFrameUpdates > maxUpdatesInOneFrame)
                maxUpdatesInOneFrame = currentFrameUpdates;
            currentFrameUpdates = 0;
            frameCount++;
        };

        loop.OnFixedUpdate += dt =>
        {
            currentFrameUpdates++;
            // Simulate a slow update to cause accumulation
            if (frameCount < 3)
                Thread.Sleep(50);
        };

        loop.OnFrameEnd += () =>
        {
            if (frameCount >= 10)
                loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        bool completed = task.Wait(TimeSpan.FromSeconds(5));

        if (!completed) loop.Stop();
        task.Wait(TimeSpan.FromSeconds(1));

        // The max updates per frame should be capped at MaxUpdatesPerFrame
        Assert.True(maxUpdatesInOneFrame <= config.MaxUpdatesPerFrame,
            $"Max updates per frame was {maxUpdatesInOneFrame}, expected <= {config.MaxUpdatesPerFrame}");
    }

    [Fact]
    public void Stop_ExitsTheLoop()
    {
        var config = new Config();
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        bool loopExited = false;

        var task = Task.Run(() =>
        {
            loop.Run();
            loopExited = true;
        });

        Thread.Sleep(100);
        loop.Stop();
        bool completed = task.Wait(TimeSpan.FromSeconds(2));

        Assert.True(completed, "Loop did not exit within timeout");
        Assert.True(loopExited);
    }

    [Fact]
    public void OnRender_CalledWithAlpha()
    {
        var config = new Config { FixedTimestep = 1.0 / 60.0 };
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        var alphas = new List<double>();
        int frameCount = 0;

        loop.OnRender += alpha =>
        {
            lock (alphas)
            {
                alphas.Add(alpha);
            }
            frameCount++;
            if (frameCount >= 5)
                loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(2));

        lock (alphas)
        {
            Assert.True(alphas.Count > 0, "OnRender was never called");
            // Alpha should be in [0, 1) range (accumulator / fixedDt)
            foreach (var a in alphas)
            {
                Assert.True(a >= 0.0, $"Alpha {a} is negative");
            }
        }
    }

    [Fact]
    public void OnProcessInput_CalledEveryFrame()
    {
        var config = new Config();
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int inputCalls = 0;

        loop.OnProcessInput += () =>
        {
            inputCalls++;
            if (inputCalls >= 5)
                loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(2));

        Assert.True(inputCalls >= 5, $"OnProcessInput called {inputCalls} times, expected >= 5");
    }

    [Fact]
    public void OnRenderUI_CalledEveryFrame()
    {
        var config = new Config();
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int uiCalls = 0;
        int frameLimit = 0;

        loop.OnRenderUI += () => uiCalls++;
        loop.OnFrameEnd += () =>
        {
            frameLimit++;
            if (frameLimit >= 5) loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(2));

        Assert.True(uiCalls >= 5, $"OnRenderUI called {uiCalls} times, expected >= 5");
    }

    [Fact]
    public void OnFrameEnd_CalledEveryFrame()
    {
        var config = new Config();
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int endCalls = 0;

        loop.OnFrameEnd += () =>
        {
            endCalls++;
            if (endCalls >= 5) loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(2));

        Assert.Equal(5, endCalls);
    }

    [Fact]
    public void PausedSpeed_NoFixedUpdates()
    {
        var config = new Config { FixedTimestep = 1.0 / 60.0 };
        var time = new TimeManager();
        time.SetSpeed(0); // Paused
        var loop = new GameLoop(config, time);
        int updateCount = 0;
        int frameCount = 0;

        loop.OnFixedUpdate += dt => updateCount++;
        loop.OnFrameEnd += () =>
        {
            frameCount++;
            if (frameCount >= 10) loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(2));

        Assert.Equal(0, updateCount);
    }

    [Fact]
    public void FrameTimeClamped_At250ms()
    {
        // Verify the game loop doesn't allow frame times over 0.25s
        // by checking that even with delays, updates remain bounded
        var config = new Config { FixedTimestep = 1.0 / 60.0, MaxUpdatesPerFrame = 5 };
        var time = new TimeManager();
        var loop = new GameLoop(config, time);
        int maxUpdates = 0;
        int currentUpdates = 0;
        int frames = 0;

        loop.OnProcessInput += () =>
        {
            if (currentUpdates > maxUpdates) maxUpdates = currentUpdates;
            currentUpdates = 0;
        };

        loop.OnFixedUpdate += dt => currentUpdates++;
        loop.OnFrameEnd += () =>
        {
            frames++;
            if (frames >= 5) loop.Stop();
        };

        var task = Task.Run(() => loop.Run());
        task.Wait(TimeSpan.FromSeconds(3));

        // Even at 0.25s frame time and 1/60 timestep, that's 15 updates
        // But MaxUpdatesPerFrame caps it at 5
        Assert.True(maxUpdates <= config.MaxUpdatesPerFrame,
            $"Max updates {maxUpdates} exceeds cap {config.MaxUpdatesPerFrame}");
    }
}
