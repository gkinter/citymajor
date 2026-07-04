namespace Forge.Engine.Data;

/// <summary>
/// SoA pool for vehicles (cars, trucks, buses, emergency).
/// Vehicles move along the road graph and are interpolated for rendering.
/// </summary>
public sealed class VehicleData
{
    public int Capacity { get; }
    public int Count { get; private set; }

    // World-space position (interpolated for rendering)
    public float[] WorldX;
    public float[] WorldY;

    // Previous position (for render interpolation)
    public float[] PrevWorldX;
    public float[] PrevWorldY;

    // Movement
    public float[] Speed;        // Current speed in tiles/sec
    public float[] MaxSpeed;     // Type-dependent max speed
    public float[] Heading;      // Direction in radians

    // Route
    public int[] CurrentRoadNode;
    public int[] TargetRoadNode;
    public float[] RouteProgress; // 0.0 to 1.0 along current edge

    // Type
    public ushort[] TypeId;      // Vehicle type (car, bus, truck, emergency)
    public byte[] Flags;         // bit 0: active, bit 1: emergency, bit 2: stuck

    private readonly Stack<int> _freeSlots;

    public VehicleData(int capacity)
    {
        Capacity = capacity;

        WorldX = new float[capacity];
        WorldY = new float[capacity];
        PrevWorldX = new float[capacity];
        PrevWorldY = new float[capacity];
        Speed = new float[capacity];
        MaxSpeed = new float[capacity];
        Heading = new float[capacity];
        CurrentRoadNode = new int[capacity];
        TargetRoadNode = new int[capacity];
        RouteProgress = new float[capacity];
        TypeId = new ushort[capacity];
        Flags = new byte[capacity];

        _freeSlots = new Stack<int>(capacity);
        for (int i = capacity - 1; i >= 0; i--)
            _freeSlots.Push(i);
    }

    public int Allocate()
    {
        if (_freeSlots.Count == 0) return -1;
        int slot = _freeSlots.Pop();
        Flags[slot] = 1;
        Count++;
        return slot;
    }

    public void Free(int index)
    {
        if (index < 0 || index >= Capacity) return;
        Flags[index] = 0;
        _freeSlots.Push(index);
        Count--;
    }

    public bool IsActive(int index) => (Flags[index] & 1) != 0;
}
