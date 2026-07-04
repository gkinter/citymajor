using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Forge.Engine.Core;

/// <summary>
/// Type-safe publish/subscribe event system for cross-system communication.
///
/// Supports two modes:
/// - Publish(): immediate dispatch on the calling thread (same frame)
/// - Enqueue(): thread-safe queueing for dispatch on the next FlushQueued() call
///
/// This decouples game systems: when a building is placed, the traffic system
/// doesn't need a direct reference — it subscribes to BuildingPlacedEvent.
/// </summary>
public sealed class EventBus
{
    /// <summary>
    /// Type-erased channel interface so we can store heterogeneous channels
    /// in a single dictionary keyed by Type.
    /// </summary>
    private interface IEventChannel
    {
        int SubscriberCount { get; }
        int QueuedCount { get; }
        void FlushQueued();
    }

    /// <summary>
    /// Strongly-typed channel for a specific event struct T.
    /// Each event type gets its own channel with its own subscriber list and queue.
    /// </summary>
    private sealed class EventChannel<T> : IEventChannel where T : struct
    {
        private readonly List<Action<T>> _handlers = new();
        private readonly ConcurrentQueue<T> _queue = new();
        private readonly object _lock = new();

        // Cached handler array, rebuilt only on subscribe/unsubscribe to avoid
        // allocating a new array on every Publish call.
        private Action<T>[] _cachedHandlers = Array.Empty<Action<T>>();
        private bool _cacheStale = true;

        public int SubscriberCount
        {
            get { lock (_lock) return _handlers.Count; }
        }

        public int QueuedCount => _queue.Count;

        public void Subscribe(Action<T> handler)
        {
            lock (_lock)
            {
                _handlers.Add(handler);
                _cacheStale = true;
            }
        }

        public bool Unsubscribe(Action<T> handler)
        {
            lock (_lock)
            {
                bool removed = _handlers.Remove(handler);
                if (removed) _cacheStale = true;
                return removed;
            }
        }

        public void Publish(in T evt)
        {
            Action<T>[] snapshot;
            lock (_lock)
            {
                if (_handlers.Count == 0) return;
                if (_cacheStale)
                {
                    _cachedHandlers = _handlers.ToArray();
                    _cacheStale = false;
                }
                snapshot = _cachedHandlers;
            }

            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i](evt);
        }

        public void Enqueue(in T evt)
        {
            _queue.Enqueue(evt);
        }

        public void FlushQueued()
        {
            while (_queue.TryDequeue(out var evt))
            {
                Publish(in evt);
            }
        }
    }

    private readonly Dictionary<Type, IEventChannel> _channels = new();
    private readonly object _channelsLock = new();

    // Cached channel array for FlushQueued, rebuilt only when channels are added
    private IEventChannel[] _cachedChannels = Array.Empty<IEventChannel>();
    private bool _channelsCacheStale = true;

    /// <summary>
    /// Subscribe a handler to receive events of type T.
    /// </summary>
    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        GetOrCreateChannel<T>().Subscribe(handler);
    }

    /// <summary>
    /// Unsubscribe a previously subscribed handler.
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var channel = TryGetChannel<T>();
        channel?.Unsubscribe(handler);
    }

    /// <summary>
    /// Publish an event immediately to all current subscribers.
    /// Called on the current thread — typically the main/sim thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish<T>(in T evt) where T : struct
    {
        var channel = TryGetChannel<T>();
        channel?.Publish(in evt);
    }

    /// <summary>
    /// Queue an event for later dispatch. Thread-safe — can be called from any thread.
    /// Events are dispatched when FlushQueued() is called on the main thread.
    /// </summary>
    public void Enqueue<T>(in T evt) where T : struct
    {
        GetOrCreateChannel<T>().Enqueue(in evt);
    }

    /// <summary>
    /// Dispatch all queued events across all event types.
    /// Call once per frame on the main thread.
    /// </summary>
    public void FlushQueued()
    {
        IEventChannel[] snapshot;
        lock (_channelsLock)
        {
            if (_channels.Count == 0) return;
            if (_channelsCacheStale)
            {
                _cachedChannels = new IEventChannel[_channels.Count];
                _channels.Values.CopyTo(_cachedChannels, 0);
                _channelsCacheStale = false;
            }
            snapshot = _cachedChannels;
        }

        for (int i = 0; i < snapshot.Length; i++)
            snapshot[i].FlushQueued();
    }

    /// <summary>
    /// Get the number of subscribers for a specific event type.
    /// </summary>
    public int SubscriberCount<T>() where T : struct
    {
        var channel = TryGetChannel<T>();
        return channel?.SubscriberCount ?? 0;
    }

    /// <summary>
    /// Get the total number of queued events across all event types.
    /// </summary>
    public int QueuedCount
    {
        get
        {
            int total = 0;
            lock (_channelsLock)
            {
                foreach (var channel in _channels.Values)
                    total += channel.QueuedCount;
            }
            return total;
        }
    }

    private EventChannel<T> GetOrCreateChannel<T>() where T : struct
    {
        Type key = typeof(T);
        lock (_channelsLock)
        {
            if (_channels.TryGetValue(key, out var existing))
                return (EventChannel<T>)existing;

            var channel = new EventChannel<T>();
            _channels[key] = channel;
            _channelsCacheStale = true;
            return channel;
        }
    }

    private EventChannel<T>? TryGetChannel<T>() where T : struct
    {
        lock (_channelsLock)
        {
            if (_channels.TryGetValue(typeof(T), out var channel))
                return (EventChannel<T>)channel;
            return null;
        }
    }
}

// ── Game Events ──────────────────────────────────────────────────────────────

/// <summary>Fired when a building is successfully placed on the map.</summary>
public struct BuildingPlacedEvent
{
    public int BuildingId;
    public int TileX;
    public int TileY;
    public ushort TypeId;
}

/// <summary>Fired when a building is demolished.</summary>
public struct BuildingDemolishedEvent
{
    public int BuildingId;
    public int TileX;
    public int TileY;
}

/// <summary>Fired when a road segment is built.</summary>
public struct RoadBuiltEvent
{
    public int TileX;
    public int TileY;
    public byte RoadType;
}

/// <summary>Fired when a road segment is demolished.</summary>
public struct RoadDemolishedEvent
{
    public int TileX;
    public int TileY;
}

/// <summary>Fired when a tile's zone designation changes.</summary>
public struct ZoneChangedEvent
{
    public int TileX;
    public int TileY;
    public byte OldZone;
    public byte NewZone;
}

/// <summary>Fired when the city population changes.</summary>
public struct PopulationChangedEvent
{
    public int Delta;
    public int NewTotal;
}

/// <summary>Fired when the city budget balance changes significantly.</summary>
public struct BudgetChangedEvent
{
    public float NewBalance;
}

/// <summary>Fired when a technology is unlocked in the tech tree.</summary>
public struct TechUnlockedEvent
{
    public int TechId;
}

/// <summary>Fired when a law or policy is enacted.</summary>
public struct LawEnactedEvent
{
    public int LawId;
}

/// <summary>Fired when a disaster begins.</summary>
public struct DisasterStartedEvent
{
    public int EventId;
    public int TileX;
    public int TileY;
}

/// <summary>Fired when the season changes.</summary>
public struct SeasonChangedEvent
{
    public int NewSeason;
}

/// <summary>Fired when the historical era advances.</summary>
public struct EraChangedEvent
{
    public int NewEra;
}
