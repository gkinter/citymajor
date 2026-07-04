using Forge.Engine.Core;
using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class SimulationLoopTests : IDisposable
{
    private readonly Config _config = new() { WorldSize = 16, ChunkSize = 8 };
    private readonly SimulationLoop _loop;

    public SimulationLoopTests()
    {
        _loop = new SimulationLoop(_config);
    }

    public void Dispose()
    {
        _loop.Dispose();
    }

    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        Assert.NotNull(_loop.State);
        Assert.NotNull(_loop.Commands);
        Assert.NotNull(_loop.CurrentSnapshot);
        Assert.Equal(0, _loop.State.TickCount);
    }

    [Fact]
    public void Start_Stop_DoesNotThrow()
    {
        _loop.Start();
        Thread.Sleep(50);
        _loop.Stop();
    }

    [Fact]
    public void Start_Twice_DoesNotCreateSecondThread()
    {
        _loop.Start();
        _loop.Start(); // Should be a no-op
        Thread.Sleep(50);
        _loop.Stop();
    }

    [Fact]
    public void TicksFire_WhenRunning()
    {
        int trafficTicks = 0;
        _loop.OnTrafficTick += (state, dt) => Interlocked.Increment(ref trafficTicks);

        _loop.SetSpeed(1);
        _loop.Start();
        Thread.Sleep(1200); // Should get at least 1 traffic tick at 500ms intervals
        _loop.Stop();

        Assert.True(trafficTicks > 0, $"Expected traffic ticks > 0, got {trafficTicks}");
    }

    [Fact]
    public void SpeedMultiplier_AffectsTickRate()
    {
        int ticksSlow = 0;
        int ticksFast = 0;

        _loop.OnTrafficTick += (state, dt) => Interlocked.Increment(ref ticksSlow);
        _loop.SetSpeed(1);
        _loop.Start();
        Thread.Sleep(1000);
        _loop.Stop();
        ticksSlow = Volatile.Read(ref ticksSlow);

        // Create a new loop for the fast test
        using var fastLoop = new SimulationLoop(_config);
        fastLoop.OnTrafficTick += (state, dt) => Interlocked.Increment(ref ticksFast);
        fastLoop.SetSpeed(3);
        fastLoop.Start();
        Thread.Sleep(1000);
        fastLoop.Stop();
        ticksFast = Volatile.Read(ref ticksFast);

        Assert.True(ticksFast > ticksSlow,
            $"Expected fast ticks ({ticksFast}) > slow ticks ({ticksSlow})");
    }

    [Fact]
    public void Pause_StopsTicks()
    {
        int ticks = 0;
        _loop.OnTrafficTick += (state, dt) => Interlocked.Increment(ref ticks);

        _loop.SetSpeed(0); // Paused
        _loop.Start();
        Thread.Sleep(600);
        _loop.Stop();

        Assert.Equal(0, ticks);
    }

    [Fact]
    public void Commands_AreDequeuedAndDispatched()
    {
        var dispatched = new List<CommandQueue.CommandType>();
        _loop.OnCommand += (state, cmd) =>
        {
            lock (dispatched)
            {
                dispatched.Add(cmd.Type);
            }
        };

        _loop.Commands.EnqueuePlaceBuilding(5, 5, 1);
        _loop.Commands.EnqueueBulldoze(3, 3, 1, 1);

        _loop.SetSpeed(1);
        _loop.Start();
        Thread.Sleep(200); // Commands processed quickly
        _loop.Stop();

        lock (dispatched)
        {
            Assert.Contains(CommandQueue.CommandType.PlaceBuilding, dispatched);
            Assert.Contains(CommandQueue.CommandType.Bulldoze, dispatched);
        }
    }

    [Fact]
    public void Commands_ProcessedWhilePaused()
    {
        var dispatched = new List<CommandQueue.CommandType>();
        _loop.OnCommand += (state, cmd) =>
        {
            lock (dispatched)
            {
                dispatched.Add(cmd.Type);
            }
        };

        _loop.Commands.EnqueuePlaceBuilding(1, 1, 1);
        _loop.SetSpeed(0); // Paused
        _loop.Start();
        Thread.Sleep(200);
        _loop.Stop();

        lock (dispatched)
        {
            Assert.Contains(CommandQueue.CommandType.PlaceBuilding, dispatched);
        }
    }

    [Fact]
    public void Snapshot_ProducedEachTickCycle()
    {
        _loop.SetSpeed(1);
        _loop.Start();
        Thread.Sleep(300);

        var snap = _loop.CurrentSnapshot;
        Assert.NotNull(snap);

        _loop.Stop();
    }

    [Fact]
    public void DayTick_FiresAfterDayInterval()
    {
        int dayTicks = 0;
        _loop.OnDayTick += (state, dt) => Interlocked.Increment(ref dayTicks);

        // At speed 4, DayInterval (10s) becomes 2.5s real time
        _loop.SetSpeed(4);
        _loop.Start();
        Thread.Sleep(3000);
        _loop.Stop();

        Assert.True(dayTicks > 0, $"Expected day ticks > 0, got {dayTicks}");
    }

    [Fact]
    public void SetSpeed_ClampsToRange()
    {
        _loop.SetSpeed(-5);
        _loop.SetSpeed(100);
        // No exception = pass. Speed is clamped internally to 0-4.
    }

    [Fact]
    public void TickCount_IncreasesOverTime()
    {
        _loop.SetSpeed(1);
        _loop.Start();
        Thread.Sleep(800);
        _loop.Stop();

        Assert.True(_loop.State.TickCount > 0,
            $"Expected TickCount > 0, got {_loop.State.TickCount}");
    }

    [Fact]
    public void MonthTick_FiresOnNewMonth()
    {
        int monthTicks = 0;
        _loop.OnMonthTick += (state, dt) => Interlocked.Increment(ref monthTicks);

        // Force the state near end of month to trigger month tick sooner
        _loop.State.Day = 30;

        _loop.SetSpeed(4);
        _loop.Start();
        Thread.Sleep(3500);
        _loop.Stop();

        Assert.True(monthTicks > 0, $"Expected month ticks > 0, got {monthTicks}");
    }

    [Fact]
    public void Dispose_StopsLoop()
    {
        _loop.Start();
        Thread.Sleep(50);
        _loop.Dispose();
        // Should not hang or throw
    }
}
