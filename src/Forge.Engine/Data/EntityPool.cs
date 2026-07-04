using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Forge.Engine.Data;

/// <summary>
/// Delegate for iterating entity pool slots by ref. Cannot use Action because
/// Action does not support ref parameters.
/// </summary>
public delegate void EntityAction<T>(int index, ref T data) where T : struct;

/// <summary>
/// A generation-indexed handle to an entity slot. The generation counter
/// detects use-after-free: if a slot is freed and reallocated, old handles
/// with stale generations will be rejected.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct EntityHandle : IEquatable<EntityHandle>
{
    public readonly int Index;
    public readonly ushort Generation;

    public static readonly EntityHandle Invalid = new(-1, 0);

    public EntityHandle(int index, ushort generation)
    {
        Index = index;
        Generation = generation;
    }

    public bool IsValid => Index >= 0;

    public bool Equals(EntityHandle other) =>
        Index == other.Index && Generation == other.Generation;

    public override bool Equals(object? obj) =>
        obj is EntityHandle h && Equals(h);

    public override int GetHashCode() =>
        HashCode.Combine(Index, Generation);

    public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);
    public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);

    public override string ToString() => $"EntityHandle({Index}:{Generation})";
}

/// <summary>
/// Generic, generation-indexed entity pool for managing 100K+ entities with
/// O(1) allocation/deallocation and zero GC pressure after construction.
///
/// Uses a flat array for entity data, a parallel generation array for
/// use-after-free detection, and a stack-based free list for O(1) alloc/dealloc.
/// </summary>
public sealed class EntityPool<T> where T : struct
{
    private T[] _data;
    private ushort[] _generations;
    private bool[] _alive;
    private int[] _freeStack;
    private int _freeTop;
    private int _aliveCount;
    private int _capacity;

    /// <summary>Number of currently alive entities.</summary>
    public int AliveCount => _aliveCount;

    /// <summary>Total slot capacity. May grow via Grow().</summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Create an entity pool with the specified initial capacity.
    /// All slots start free with generation 0.
    /// </summary>
    public EntityPool(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _data = new T[capacity];
        _generations = new ushort[capacity];
        _alive = new bool[capacity];
        _freeStack = new int[capacity];
        _freeTop = capacity;
        _aliveCount = 0;

        // Push slots in reverse order so that slot 0 is popped first
        for (int i = 0; i < capacity; i++)
            _freeStack[i] = capacity - 1 - i;
    }

    /// <summary>
    /// Allocate a slot, store the given data, and return a generational handle.
    /// O(1). If the pool is full, it automatically doubles capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityHandle Allocate(in T data)
    {
        if (_freeTop == 0)
            Grow(_capacity * 2);

        int index = _freeStack[--_freeTop];
        _data[index] = data;
        _alive[index] = true;
        _aliveCount++;

        return new EntityHandle(index, _generations[index]);
    }

    /// <summary>
    /// Free a slot by handle. O(1). The generation is bumped so stale handles
    /// are automatically invalidated. Throws if the handle is stale.
    /// </summary>
    public void Free(EntityHandle handle)
    {
        ValidateHandle(handle);

        int index = handle.Index;
        _alive[index] = false;
        _data[index] = default;

        // Bump generation (wraps at ushort.MaxValue, which is fine —
        // the probability of a collision after 65535 reuses is negligible)
        unchecked { _generations[index]++; }

        _freeStack[_freeTop++] = index;
        _aliveCount--;
    }

    /// <summary>
    /// Get a ref to the entity data at the given handle. O(1).
    /// Throws if the handle is stale or the slot is dead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(EntityHandle handle)
    {
        ValidateHandle(handle);
        return ref _data[handle.Index];
    }

    /// <summary>
    /// Check whether the handle still refers to a living entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(EntityHandle handle)
    {
        if (handle.Index < 0 || handle.Index >= _capacity) return false;
        return _alive[handle.Index] && _generations[handle.Index] == handle.Generation;
    }

    /// <summary>
    /// Iterate all alive entities. The action receives (slotIndex, ref T data).
    /// Dead slots are skipped.
    /// </summary>
    public void ForEach(EntityAction<T> action)
    {
        int found = 0;
        for (int i = 0; i < _capacity && found < _aliveCount; i++)
        {
            if (_alive[i])
            {
                action(i, ref _data[i]);
                found++;
            }
        }
    }

    /// <summary>
    /// Parallel iteration over alive entities using Parallel.For with batching.
    /// Each batch processes a contiguous range of the backing array, skipping dead slots.
    /// The action receives (slotIndex, ref T data).
    /// </summary>
    public void ParallelForEach(EntityAction<T> action, int batchSize = 256)
    {
        if (_aliveCount == 0) return;
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        int cap = _capacity;
        int batchCount = (cap + batchSize - 1) / batchSize;

        // Capture locals for the closure
        T[] data = _data;
        bool[] alive = _alive;

        Parallel.For(0, batchCount, batchIndex =>
        {
            int start = batchIndex * batchSize;
            int end = System.Math.Min(start + batchSize, cap);

            for (int i = start; i < end; i++)
            {
                if (alive[i])
                {
                    action(i, ref data[i]);
                }
            }
        });
    }

    /// <summary>
    /// Get a Span over the raw data array for SIMD or bulk processing.
    /// Dead slots contain default(T). Use IsAliveAt(index) to check liveness.
    /// </summary>
    public Span<T> AsSpan() => _data.AsSpan(0, _capacity);

    /// <summary>
    /// Check whether a given raw index is alive. For use with AsSpan().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAliveAt(int index)
    {
        if ((uint)index >= (uint)_capacity) return false;
        return _alive[index];
    }

    /// <summary>
    /// Get the current generation for a given raw slot index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetGeneration(int index)
    {
        if ((uint)index >= (uint)_capacity)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _generations[index];
    }

    /// <summary>
    /// Double (or grow to specified) capacity. Existing handles remain valid.
    /// </summary>
    private void Grow(int newCapacity)
    {
        if (newCapacity <= _capacity)
            throw new ArgumentException("New capacity must exceed current capacity.");

        int oldCapacity = _capacity;

        Array.Resize(ref _data, newCapacity);
        Array.Resize(ref _generations, newCapacity);
        Array.Resize(ref _alive, newCapacity);

        // Expand free stack
        int[] newFreeStack = new int[newCapacity];
        // Copy existing free entries
        Array.Copy(_freeStack, newFreeStack, _freeTop);

        // Push new slots onto the free stack (in reverse so lower indices come first)
        int newSlots = newCapacity - oldCapacity;
        for (int i = 0; i < newSlots; i++)
            newFreeStack[_freeTop + i] = newCapacity - 1 - i;

        _freeTop += newSlots;
        _freeStack = newFreeStack;
        _capacity = newCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateHandle(EntityHandle handle)
    {
        if (handle.Index < 0 || handle.Index >= _capacity)
            throw new ArgumentException($"Handle index {handle.Index} is out of range [0, {_capacity}).");

        if (_generations[handle.Index] != handle.Generation)
            throw new InvalidOperationException(
                $"Stale handle: slot {handle.Index} is at generation {_generations[handle.Index]}, " +
                $"but handle has generation {handle.Generation}.");

        if (!_alive[handle.Index])
            throw new InvalidOperationException($"Slot {handle.Index} is not alive.");
    }
}
