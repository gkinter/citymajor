using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class DoubleBufferTests
{
    private sealed class Payload
    {
        public int Value { get; init; }
    }

    [Fact]
    public void Front_ReturnsInitialValue()
    {
        var initial = new Payload { Value = 1 };
        var buffer = new DoubleBuffer<Payload>(initial, new Payload { Value = 2 });

        Assert.Same(initial, buffer.Front);
        Assert.Equal(1, buffer.Front.Value);
    }

    [Fact]
    public void SwapIn_ReplacesFront()
    {
        var a = new Payload { Value = 1 };
        var b = new Payload { Value = 2 };
        var buffer = new DoubleBuffer<Payload>(a, new Payload());

        buffer.SwapIn(b);

        Assert.Same(b, buffer.Front);
        Assert.Equal(2, buffer.Front.Value);
    }

    [Fact]
    public void SwapIn_ReturnsPreviousFront()
    {
        var a = new Payload { Value = 1 };
        var b = new Payload { Value = 2 };
        var buffer = new DoubleBuffer<Payload>(a, new Payload());

        var previous = buffer.SwapIn(b);

        Assert.Same(a, previous);
        Assert.Equal(1, previous.Value);
    }

    [Fact]
    public void MultipleRapidSwaps_FrontAlwaysCurrent()
    {
        var buffer = new DoubleBuffer<Payload>(new Payload { Value = 0 }, new Payload());

        for (int i = 1; i <= 100; i++)
        {
            buffer.SwapIn(new Payload { Value = i });
            Assert.Equal(i, buffer.Front.Value);
        }
    }

    [Fact]
    public void ThreadSafety_ConcurrentReadWrite()
    {
        var buffer = new DoubleBuffer<Payload>(new Payload { Value = 0 }, new Payload());
        const int iterations = 10_000;
        bool readError = false;

        // Writer thread: continuously swap in new values
        var writerTask = Task.Run(() =>
        {
            for (int i = 1; i <= iterations; i++)
            {
                buffer.SwapIn(new Payload { Value = i });
            }
        });

        // Reader thread: continuously read front, verify it's a valid Payload
        var readerTask = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var snap = buffer.Front;
                if (snap == null || snap.Value < 0)
                {
                    readError = true;
                    break;
                }
            }
        });

        Task.WaitAll(writerTask, readerTask);

        Assert.False(readError, "Reader saw null or invalid value during concurrent access");
    }

    [Fact]
    public void ThreadSafety_FrontNeverNull()
    {
        var buffer = new DoubleBuffer<Payload>(new Payload { Value = 0 }, new Payload());
        bool sawNull = false;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            int i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                buffer.SwapIn(new Payload { Value = ++i });
            }
        });

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (buffer.Front == null)
                {
                    sawNull = true;
                    break;
                }
            }
        });

        Task.WaitAll(writer, reader);

        Assert.False(sawNull, "Front returned null during concurrent access");
    }

    [Fact]
    public void SwapIn_ChainReturnsPreviousValues()
    {
        var a = new Payload { Value = 1 };
        var b = new Payload { Value = 2 };
        var c = new Payload { Value = 3 };
        var buffer = new DoubleBuffer<Payload>(a, new Payload());

        var prev1 = buffer.SwapIn(b);
        var prev2 = buffer.SwapIn(c);

        Assert.Equal(1, prev1.Value);
        Assert.Equal(2, prev2.Value);
        Assert.Equal(3, buffer.Front.Value);
    }

    [Fact]
    public void Front_ReadMultipleTimes_ReturnsSameReference()
    {
        var a = new Payload { Value = 42 };
        var buffer = new DoubleBuffer<Payload>(a, new Payload());

        var read1 = buffer.Front;
        var read2 = buffer.Front;

        Assert.Same(read1, read2);
    }

    [Fact]
    public void SwapIn_WithSameObject_ReturnsItself()
    {
        var a = new Payload { Value = 1 };
        var buffer = new DoubleBuffer<Payload>(a, new Payload());

        var prev = buffer.SwapIn(a);

        Assert.Same(a, prev);
        Assert.Same(a, buffer.Front);
    }

    [Fact]
    public void MultipleReaders_AllSeeConsistentValue()
    {
        var buffer = new DoubleBuffer<Payload>(new Payload { Value = 42 }, new Payload());
        bool inconsistency = false;

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var snap = buffer.Front;
                if (snap == null) { inconsistency = true; return; }
            }
        })).ToArray();

        Task.WaitAll(tasks);

        Assert.False(inconsistency);
    }
}
