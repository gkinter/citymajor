namespace Forge.Engine.Data;

/// <summary>
/// SoA pool for household agents. Each household represents a family unit
/// with home, workplace, income, and satisfaction.
/// </summary>
public sealed class HouseholdData
{
    public int Capacity { get; }
    public int Count { get; private set; }

    // Location
    public ushort[] HomeBuildingId;
    public ushort[] WorkBuildingId;

    // Demographics
    public byte[] MemberCount;   // 1-8 family members
    public byte[] AgeGroup;      // 0=young, 1=working, 2=retired
    public byte[] Education;     // 0=none, 1=basic, 2=advanced, 3=university

    // Economy
    public int[] Income;         // Monthly income
    public int[] Savings;        // Accumulated savings
    public byte[] WealthLevel;   // 0=poor, 1=lower, 2=middle, 3=upper, 4=wealthy

    // Satisfaction (0-255, 128 = neutral)
    public byte[] Happiness;
    public byte[] HealthSatisfaction;
    public byte[] SafetySatisfaction;
    public byte[] TransportSatisfaction;
    public byte[] LeisureSatisfaction;

    // State
    public byte[] Flags;         // bit 0: active, bit 1: moving, bit 2: unemployed

    // Free list for pool allocation
    private readonly Stack<int> _freeSlots;

    public HouseholdData(int capacity)
    {
        Capacity = capacity;

        HomeBuildingId = new ushort[capacity];
        WorkBuildingId = new ushort[capacity];
        MemberCount = new byte[capacity];
        AgeGroup = new byte[capacity];
        Education = new byte[capacity];
        Income = new int[capacity];
        Savings = new int[capacity];
        WealthLevel = new byte[capacity];
        Happiness = new byte[capacity];
        HealthSatisfaction = new byte[capacity];
        SafetySatisfaction = new byte[capacity];
        TransportSatisfaction = new byte[capacity];
        LeisureSatisfaction = new byte[capacity];
        Flags = new byte[capacity];

        _freeSlots = new Stack<int>(capacity);
        for (int i = capacity - 1; i >= 0; i--)
            _freeSlots.Push(i);
    }

    /// <summary>Allocate a new household slot. Returns -1 if pool is full.</summary>
    public int Allocate()
    {
        if (_freeSlots.Count == 0) return -1;
        int slot = _freeSlots.Pop();
        Flags[slot] = 1; // Active
        Count++;
        return slot;
    }

    /// <summary>Free a household slot back to the pool.</summary>
    public void Free(int index)
    {
        if (index < 0 || index >= Capacity) return;
        Flags[index] = 0;
        HomeBuildingId[index] = 0;
        WorkBuildingId[index] = 0;
        _freeSlots.Push(index);
        Count--;
    }

    public bool IsActive(int index) => (Flags[index] & 1) != 0;
}
