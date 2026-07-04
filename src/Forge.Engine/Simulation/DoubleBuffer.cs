namespace Forge.Engine.Simulation;

/// <summary>
/// Lock-free double buffer for passing simulation snapshots to the render thread.
/// The simulation thread publishes new values via SwapIn(), which atomically replaces
/// the front reference. The render thread reads from Front without locking.
///
/// This is truly lock-free: a single Interlocked.Exchange on the front reference
/// guarantees the render thread always sees a complete, consistent snapshot.
/// </summary>
public sealed class DoubleBuffer<T> where T : class
{
    private T _front;

    public DoubleBuffer(T initial1, T _unused)
    {
        _front = initial1;
    }

    /// <summary>
    /// Get the current front buffer for reading (render thread).
    /// This never blocks. The returned reference is always a complete snapshot.
    /// </summary>
    public T Front => Volatile.Read(ref _front);

    /// <summary>
    /// Atomically publish a new value as the front buffer. Called by the simulation
    /// thread after producing a complete snapshot. Returns the previous front value
    /// (which can be reused or discarded).
    /// </summary>
    public T SwapIn(T newValue)
    {
        return Interlocked.Exchange(ref _front, newValue);
    }
}
