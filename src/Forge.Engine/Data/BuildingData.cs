namespace Forge.Engine.Data;

/// <summary>
/// SoA pool for buildings. Each building occupies one or more tiles
/// and has a type, level, and operational state.
/// </summary>
public sealed class BuildingData
{
    public int Capacity { get; }
    public int Count { get; private set; }

    // Grid position (top-left corner of the building footprint)
    public int[] GridX;
    public int[] GridY;

    // Dimensions
    public byte[] Width;
    public byte[] Height;

    // Type and state
    public ushort[] TypeId;      // References building definition data
    public byte[] Level;         // Upgrade level (1-5)
    public byte[] State;         // 0=constructing, 1=operational, 2=abandoned, 3=demolishing

    // Simulation values
    public ushort[] Occupants;   // Current occupant count
    public ushort[] MaxOccupants;
    public int[] Revenue;        // Monthly revenue (or negative for cost)
    public byte[] Condition;     // 0-255, degrades over time without maintenance
    public byte[] FireRisk;
    public byte[] CrimeAttraction;

    // Services provided (bitmask)
    public uint[] ServiceFlags;  // bit 0: power, 1: water, 2: police, 3: fire, 4: health, 5: education, etc.

    public byte[] Flags;         // bit 0: active

    private readonly Stack<int> _freeSlots;

    public BuildingData(int capacity)
    {
        Capacity = capacity;

        GridX = new int[capacity];
        GridY = new int[capacity];
        Width = new byte[capacity];
        Height = new byte[capacity];
        TypeId = new ushort[capacity];
        Level = new byte[capacity];
        State = new byte[capacity];
        Occupants = new ushort[capacity];
        MaxOccupants = new ushort[capacity];
        Revenue = new int[capacity];
        Condition = new byte[capacity];
        FireRisk = new byte[capacity];
        CrimeAttraction = new byte[capacity];
        ServiceFlags = new uint[capacity];
        Flags = new byte[capacity];

        _freeSlots = new Stack<int>(capacity);
        // Slot 0 is reserved: household HomeBuildingId/WorkBuildingId use 0 as "none".
        for (int i = capacity - 1; i >= 1; i--)
            _freeSlots.Push(i);
    }

    public int Allocate()
    {
        if (_freeSlots.Count == 0) return -1;
        int slot = _freeSlots.Pop();
        Flags[slot] = 1;
        Level[slot] = 1;
        Condition[slot] = 255;
        Count++;
        return slot;
    }

    /// <summary>
    /// Activate a specific pool slot for save/load (preserves household home/work ids).
    /// Falls back to <see cref="Allocate"/> when the preferred slot is unavailable.
    /// </summary>
    public int AllocateAt(int preferred)
    {
        if (preferred <= 0 || preferred >= Capacity || IsActive(preferred))
            return Allocate();

        // Rebuild free stack without the preferred slot.
        if (_freeSlots.Count > 0)
        {
            var tmp = new int[_freeSlots.Count];
            int n = 0;
            while (_freeSlots.Count > 0)
            {
                int s = _freeSlots.Pop();
                if (s != preferred)
                    tmp[n++] = s;
            }

            for (int i = n - 1; i >= 0; i--)
                _freeSlots.Push(tmp[i]);
        }

        Flags[preferred] = 1;
        Level[preferred] = 1;
        Condition[preferred] = 255;
        Count++;
        return preferred;
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
