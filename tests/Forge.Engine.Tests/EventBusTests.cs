using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class EventBusTests
{
    private struct TestEvent
    {
        public int Value;
    }

    private struct OtherEvent
    {
        public string? Message;
    }

    [Fact]
    public void NewBus_HasNoSubscribers()
    {
        var bus = new EventBus();
        Assert.Equal(0, bus.SubscriberCount<TestEvent>());
        Assert.Equal(0, bus.QueuedCount);
    }

    [Fact]
    public void Subscribe_Publish_HandlerCalled()
    {
        var bus = new EventBus();
        int received = -1;

        bus.Subscribe<TestEvent>(e => received = e.Value);
        bus.Publish(new TestEvent { Value = 42 });

        Assert.Equal(42, received);
    }

    [Fact]
    public void Publish_MultipleSubscribers_AllCalled()
    {
        var bus = new EventBus();
        var values = new List<int>();

        bus.Subscribe<TestEvent>(e => values.Add(e.Value * 1));
        bus.Subscribe<TestEvent>(e => values.Add(e.Value * 2));
        bus.Subscribe<TestEvent>(e => values.Add(e.Value * 3));

        bus.Publish(new TestEvent { Value = 10 });

        Assert.Equal(3, values.Count);
        Assert.Contains(10, values);
        Assert.Contains(20, values);
        Assert.Contains(30, values);
    }

    [Fact]
    public void Publish_NoSubscribers_NoError()
    {
        var bus = new EventBus();
        // Should not throw
        bus.Publish(new TestEvent { Value = 1 });
    }

    [Fact]
    public void Unsubscribe_HandlerNotCalled()
    {
        var bus = new EventBus();
        int callCount = 0;
        Action<TestEvent> handler = e => callCount++;

        bus.Subscribe(handler);
        bus.Publish(new TestEvent { Value = 1 });
        Assert.Equal(1, callCount);

        bus.Unsubscribe(handler);
        bus.Publish(new TestEvent { Value = 2 });
        Assert.Equal(1, callCount); // Not called again
    }

    [Fact]
    public void SubscriberCount_Tracks()
    {
        var bus = new EventBus();
        Action<TestEvent> h1 = e => { };
        Action<TestEvent> h2 = e => { };

        bus.Subscribe(h1);
        Assert.Equal(1, bus.SubscriberCount<TestEvent>());

        bus.Subscribe(h2);
        Assert.Equal(2, bus.SubscriberCount<TestEvent>());

        bus.Unsubscribe(h1);
        Assert.Equal(1, bus.SubscriberCount<TestEvent>());
    }

    [Fact]
    public void TypeSafety_DifferentEventTypes()
    {
        var bus = new EventBus();
        int testReceived = 0;
        string? otherReceived = null;

        bus.Subscribe<TestEvent>(e => testReceived = e.Value);
        bus.Subscribe<OtherEvent>(e => otherReceived = e.Message);

        bus.Publish(new TestEvent { Value = 7 });

        Assert.Equal(7, testReceived);
        Assert.Null(otherReceived); // OtherEvent handler not called

        bus.Publish(new OtherEvent { Message = "hello" });

        Assert.Equal(7, testReceived); // TestEvent handler not called again
        Assert.Equal("hello", otherReceived);
    }

    [Fact]
    public void Enqueue_DoesNotFireImmediately()
    {
        var bus = new EventBus();
        int received = -1;

        bus.Subscribe<TestEvent>(e => received = e.Value);
        bus.Enqueue(new TestEvent { Value = 99 });

        Assert.Equal(-1, received); // Not yet fired
        Assert.Equal(1, bus.QueuedCount);
    }

    [Fact]
    public void FlushQueued_FiresQueuedEvents()
    {
        var bus = new EventBus();
        var received = new List<int>();

        bus.Subscribe<TestEvent>(e => received.Add(e.Value));

        bus.Enqueue(new TestEvent { Value = 1 });
        bus.Enqueue(new TestEvent { Value = 2 });
        bus.Enqueue(new TestEvent { Value = 3 });

        Assert.Equal(3, bus.QueuedCount);

        bus.FlushQueued();

        Assert.Equal(3, received.Count);
        Assert.Equal(1, received[0]);
        Assert.Equal(2, received[1]);
        Assert.Equal(3, received[2]);
        Assert.Equal(0, bus.QueuedCount);
    }

    [Fact]
    public void FlushQueued_EmptyQueue_NoError()
    {
        var bus = new EventBus();
        bus.FlushQueued(); // Should not throw
    }

    [Fact]
    public void Enqueue_ThreadSafe()
    {
        var bus = new EventBus();
        int totalReceived = 0;

        bus.Subscribe<TestEvent>(e => Interlocked.Add(ref totalReceived, e.Value));

        // Enqueue from multiple threads
        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                    bus.Enqueue(new TestEvent { Value = 1 });
            });
        }

        Task.WaitAll(tasks);
        Assert.Equal(1000, bus.QueuedCount);

        bus.FlushQueued();
        Assert.Equal(1000, totalReceived);
        Assert.Equal(0, bus.QueuedCount);
    }

    [Fact]
    public void MixedPublishAndEnqueue()
    {
        var bus = new EventBus();
        var received = new List<int>();

        bus.Subscribe<TestEvent>(e => received.Add(e.Value));

        bus.Publish(new TestEvent { Value = 1 }); // Immediate
        bus.Enqueue(new TestEvent { Value = 2 }); // Queued
        bus.Publish(new TestEvent { Value = 3 }); // Immediate

        Assert.Equal(2, received.Count); // Only immediate ones
        Assert.Equal(1, received[0]);
        Assert.Equal(3, received[1]);

        bus.FlushQueued();

        Assert.Equal(3, received.Count);
        Assert.Equal(2, received[2]);
    }

    [Fact]
    public void GameEvents_CompileAndPublish()
    {
        var bus = new EventBus();
        int buildingPlaced = 0;
        int roadBuilt = 0;
        int zoneChanged = 0;

        bus.Subscribe<BuildingPlacedEvent>(e => buildingPlaced = e.BuildingId);
        bus.Subscribe<RoadBuiltEvent>(e => roadBuilt = e.TileX);
        bus.Subscribe<ZoneChangedEvent>(e => zoneChanged = e.NewZone);

        bus.Publish(new BuildingPlacedEvent { BuildingId = 42, TileX = 10, TileY = 20, TypeId = 5 });
        bus.Publish(new RoadBuiltEvent { TileX = 15, TileY = 25, RoadType = 1 });
        bus.Publish(new ZoneChangedEvent { TileX = 1, TileY = 2, OldZone = 0, NewZone = 3 });

        Assert.Equal(42, buildingPlaced);
        Assert.Equal(15, roadBuilt);
        Assert.Equal(3, zoneChanged);
    }

    [Fact]
    public void AllGameEvents_CanBePublished()
    {
        var bus = new EventBus();

        // Verify all event types compile and can be published without error
        bus.Publish(new BuildingPlacedEvent { BuildingId = 1, TileX = 0, TileY = 0, TypeId = 1 });
        bus.Publish(new BuildingDemolishedEvent { BuildingId = 1, TileX = 0, TileY = 0 });
        bus.Publish(new RoadBuiltEvent { TileX = 0, TileY = 0, RoadType = 0 });
        bus.Publish(new RoadDemolishedEvent { TileX = 0, TileY = 0 });
        bus.Publish(new ZoneChangedEvent { TileX = 0, TileY = 0, OldZone = 0, NewZone = 1 });
        bus.Publish(new PopulationChangedEvent { Delta = 100, NewTotal = 1000 });
        bus.Publish(new BudgetChangedEvent { NewBalance = 50000f });
        bus.Publish(new TechUnlockedEvent { TechId = 1 });
        bus.Publish(new LawEnactedEvent { LawId = 1 });
        bus.Publish(new DisasterStartedEvent { EventId = 1, TileX = 50, TileY = 50 });
        bus.Publish(new SeasonChangedEvent { NewSeason = 2 });
        bus.Publish(new EraChangedEvent { NewEra = 3 });
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<TestEvent>(null!));
    }

    [Fact]
    public void Unsubscribe_NullHandler_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Unsubscribe<TestEvent>(null!));
    }

    [Fact]
    public void Unsubscribe_NotSubscribed_NoError()
    {
        var bus = new EventBus();
        Action<TestEvent> handler = e => { };
        // Should not throw even though handler was never subscribed
        bus.Unsubscribe(handler);
    }
}
