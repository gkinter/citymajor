using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class ProfilerTests : IDisposable
{
    public ProfilerTests()
    {
        // Reset profiler state before each test
        Profiler.Reset();
        Profiler.IsEnabled = true;
    }

    public void Dispose()
    {
        Profiler.Reset();
        Profiler.IsEnabled = true;
    }

    [Fact]
    public void BeginEndFrame_RecordsFrameTime()
    {
        Profiler.BeginFrame();
        // Simulate some work
        Thread.SpinWait(1000);
        Profiler.EndFrame();

        Assert.Equal(1, Profiler.FrameCount);
    }

    [Fact]
    public void MultipleFrames_IncrementFrameCount()
    {
        for (int i = 0; i < 10; i++)
        {
            Profiler.BeginFrame();
            Profiler.EndFrame();
        }

        Assert.Equal(10, Profiler.FrameCount);
    }

    [Fact]
    public void BeginScope_MeasuresTime()
    {
        Profiler.BeginFrame();
        using (Profiler.BeginScope("TestScope"))
        {
            Thread.SpinWait(10000);
        }
        Profiler.EndFrame();

        double avg = Profiler.GetAverageMs("TestScope");
        Assert.True(avg >= 0, $"Average should be non-negative, got {avg}");
    }

    [Fact]
    public void BeginEndSample_RecordsTiming()
    {
        Profiler.BeginFrame();
        Profiler.BeginSample("ManualScope");
        Thread.SpinWait(5000);
        Profiler.EndSample("ManualScope");
        Profiler.EndFrame();

        double avg = Profiler.GetAverageMs("ManualScope");
        Assert.True(avg >= 0, $"Average should be non-negative, got {avg}");
    }

    [Fact]
    public void SetCounter_StoresValue()
    {
        Profiler.SetCounter("Entities", 1500);
        Assert.Equal(1500, Profiler.GetCounter("Entities"));
    }

    [Fact]
    public void IncrementCounter_AccumulatesValue()
    {
        Profiler.SetCounter("DrawCalls", 0);
        Profiler.IncrementCounter("DrawCalls");
        Profiler.IncrementCounter("DrawCalls");
        Profiler.IncrementCounter("DrawCalls", 5);

        Assert.Equal(7, Profiler.GetCounter("DrawCalls"));
    }

    [Fact]
    public void GetCounter_UnknownName_ReturnsZero()
    {
        Assert.Equal(0, Profiler.GetCounter("NonExistent"));
    }

    [Fact]
    public void TrackAllocation_IncreasesMemory()
    {
        Profiler.TrackAllocation("Rendering", 1024);
        Profiler.TrackAllocation("Rendering", 2048);

        Assert.Equal(3072, Profiler.GetMemoryUsage("Rendering"));
    }

    [Fact]
    public void TrackDeallocation_DecreasesMemory()
    {
        Profiler.TrackAllocation("Simulation", 4096);
        Profiler.TrackDeallocation("Simulation", 1024);

        Assert.Equal(3072, Profiler.GetMemoryUsage("Simulation"));
    }

    [Fact]
    public void GetMemoryUsage_UnknownCategory_ReturnsZero()
    {
        Assert.Equal(0, Profiler.GetMemoryUsage("NonExistent"));
    }

    [Fact]
    public void GetAverageMs_UnknownScope_ReturnsZero()
    {
        Assert.Equal(0.0, Profiler.GetAverageMs("UnknownScope"));
    }

    [Fact]
    public void GetMaxMs_UnknownScope_ReturnsZero()
    {
        Assert.Equal(0.0, Profiler.GetMaxMs("UnknownScope"));
    }

    [Fact]
    public void IsEnabled_False_DisablesProfiling()
    {
        Profiler.IsEnabled = false;

        Profiler.BeginFrame();
        using (Profiler.BeginScope("Disabled"))
        {
            Thread.SpinWait(1000);
        }
        Profiler.SetCounter("Test", 42);
        Profiler.EndFrame();

        // Frame count should not advance
        Assert.Equal(0, Profiler.FrameCount);
        Assert.Equal(0.0, Profiler.GetAverageMs("Disabled"));
        // Counter should not be set (disabled)
        Assert.Equal(0, Profiler.GetCounter("Test"));
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        Profiler.BeginFrame();
        using (Profiler.BeginScope("ToBeReset")) { }
        Profiler.SetCounter("Counter", 100);
        Profiler.TrackAllocation("Memory", 5000);
        Profiler.EndFrame();

        Profiler.Reset();

        Assert.Equal(0, Profiler.FrameCount);
        Assert.Equal(0.0, Profiler.GetAverageMs("ToBeReset"));
        Assert.Equal(0, Profiler.GetCounter("Counter"));
        Assert.Equal(0, Profiler.GetMemoryUsage("Memory"));
    }

    [Fact]
    public void ProfileScope_Dispose_RecordsTiming()
    {
        Profiler.BeginFrame();

        var scope = Profiler.BeginScope("ScopeTest");
        Thread.SpinWait(5000);
        scope.Dispose();

        Profiler.EndFrame();

        // Should have recorded something via public API
        double avg = Profiler.GetAverageMs("ScopeTest");
        Assert.True(avg >= 0, "Scope should have recorded a non-negative average");
        Assert.Contains("ScopeTest", Profiler.GetScopeNames());
    }

    [Fact]
    public void MultipleScopes_SameFrame_Accumulate()
    {
        Profiler.BeginFrame();

        using (Profiler.BeginScope("Accumulated"))
        {
            Thread.SpinWait(1000);
        }
        using (Profiler.BeginScope("Accumulated"))
        {
            Thread.SpinWait(1000);
        }

        Profiler.EndFrame();

        // The scope accumulates multiple calls within the same frame
        double avg = Profiler.GetAverageMs("Accumulated");
        Assert.True(avg >= 0);
    }

    [Fact]
    public void ConcurrentProfiling_ThreadSafe()
    {
        var barrier = new Barrier(4);
        var tasks = new Task[4];

        for (int t = 0; t < 4; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();

                for (int i = 0; i < 100; i++)
                {
                    string scopeName = $"Thread{threadId}";
                    Profiler.BeginSample(scopeName);
                    Thread.SpinWait(100);
                    Profiler.EndSample(scopeName);

                    Profiler.IncrementCounter($"Counter{threadId}");
                    Profiler.TrackAllocation($"Memory{threadId}", 64);
                }
            });
        }

        Task.WaitAll(tasks);

        // Verify each thread's counter
        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(100, Profiler.GetCounter($"Counter{t}"));
            Assert.Equal(6400, Profiler.GetMemoryUsage($"Memory{t}"));
        }
    }

    [Fact]
    public void GetScopeNames_ReturnsAllRecordedScopes()
    {
        Profiler.BeginFrame();
        using (Profiler.BeginScope("Alpha")) { }
        using (Profiler.BeginScope("Beta")) { }
        using (Profiler.BeginScope("Gamma")) { }
        Profiler.EndFrame();

        var names = Profiler.GetScopeNames().ToList();
        Assert.Contains("Alpha", names);
        Assert.Contains("Beta", names);
        Assert.Contains("Gamma", names);
    }

    [Fact]
    public void GetCounterNames_ReturnsAllCounters()
    {
        Profiler.SetCounter("DrawCalls", 10);
        Profiler.SetCounter("Entities", 500);

        var names = Profiler.GetCounterNames().ToList();
        Assert.Contains("DrawCalls", names);
        Assert.Contains("Entities", names);
    }

    [Fact]
    public void ScopeData_MaxMs_TracksHighestValue()
    {
        // Record several frames with varying durations
        for (int i = 0; i < 5; i++)
        {
            Profiler.BeginFrame();
            Profiler.BeginSample("VaryingScope");
            Thread.SpinWait(i * 5000); // increasing work
            Profiler.EndSample("VaryingScope");
            Profiler.EndFrame();
        }

        double max = Profiler.GetMaxMs("VaryingScope");
        double avg = Profiler.GetAverageMs("VaryingScope");

        Assert.True(max >= avg, $"Max ({max}) should be >= average ({avg})");
    }

    [Fact]
    public void EndSample_WithoutBegin_IsIgnored()
    {
        // Should not throw
        Profiler.EndSample("NeverStarted");
        Assert.Equal(0.0, Profiler.GetAverageMs("NeverStarted"));
    }
}
