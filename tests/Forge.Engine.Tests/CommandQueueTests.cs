using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class CommandQueueTests
{
    [Fact]
    public void EmptyQueue_IsEmpty()
    {
        var queue = new CommandQueue();
        Assert.True(queue.IsEmpty);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_Dequeue_RoundTrip()
    {
        var queue = new CommandQueue();

        var cmd = new CommandQueue.Command
        {
            Type = CommandQueue.CommandType.PlaceBuilding,
            X = 10,
            Y = 20,
            DataId = 42,
        };

        Assert.True(queue.TryEnqueue(cmd));
        Assert.False(queue.IsEmpty);
        Assert.Equal(1, queue.Count);

        Assert.True(queue.TryDequeue(out var result));
        Assert.Equal(CommandQueue.CommandType.PlaceBuilding, result.Type);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(42, result.DataId);

        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new CommandQueue();
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void DrainTo_DrainAllCommands()
    {
        var queue = new CommandQueue();
        for (int i = 0; i < 5; i++)
        {
            queue.TryEnqueue(new CommandQueue.Command { X = i });
        }

        var output = new List<CommandQueue.Command>();
        int drained = queue.DrainTo(output);

        Assert.Equal(5, drained);
        Assert.Equal(5, output.Count);
        Assert.True(queue.IsEmpty);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i, output[i].X);
        }
    }

    [Fact]
    public void DrainTo_RespectMaxCount()
    {
        var queue = new CommandQueue();
        for (int i = 0; i < 10; i++)
        {
            queue.TryEnqueue(new CommandQueue.Command { X = i });
        }

        var output = new List<CommandQueue.Command>();
        int drained = queue.DrainTo(output, maxCount: 3);

        Assert.Equal(3, drained);
        Assert.Equal(3, output.Count);
        Assert.False(queue.IsEmpty);
    }

    [Fact]
    public void ConvenienceMethods_EnqueueCorrectly()
    {
        var queue = new CommandQueue();

        Assert.True(queue.EnqueuePlaceRoad(1, 2, 3, 4));
        Assert.True(queue.EnqueuePlaceBuilding(5, 6, 100));
        Assert.True(queue.EnqueueBulldoze(7, 8, 2, 3));

        Assert.True(queue.TryDequeue(out var road));
        Assert.Equal(CommandQueue.CommandType.PlaceRoad, road.Type);
        Assert.Equal(1, road.X);

        Assert.True(queue.TryDequeue(out var building));
        Assert.Equal(CommandQueue.CommandType.PlaceBuilding, building.Type);
        Assert.Equal((ushort)100, building.DataId);

        Assert.True(queue.TryDequeue(out var bulldoze));
        Assert.Equal(CommandQueue.CommandType.Bulldoze, bulldoze.Type);
    }
}
