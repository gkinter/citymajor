using System.Threading;

namespace Forge.Engine.Simulation;

/// <summary>
/// Lock-free single-producer single-consumer (SPSC) ring buffer for player commands.
/// The main thread (producer) enqueues commands; the simulation thread (consumer) dequeues.
/// Uses power-of-2 capacity with modular arithmetic for branchless wrap-around.
/// </summary>
public sealed class CommandQueue
{
    /// <summary>
    /// Discriminated union of all possible player commands.
    /// </summary>
    public enum CommandType : byte
    {
        PlaceRoad,
        PlaceBuilding,
        PlaceZone,
        Bulldoze,
        SetTaxRate,
        SetBudget,
        SetPolicy,
        TakeLoan,
        RepayLoan,
        SetResearch,
        ToggleOrdinance,
        Terraform,
        SetGameSpeed,
        ProposeLaw,
        RepealLaw,
    }

    public struct Command
    {
        public CommandType Type;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public ushort DataId;    // building type, zone type, policy id, etc.
        public float DataValue;  // tax rate, budget percentage, loan amount, etc.
        public byte DataByte;    // target elevation for terraform, etc.
    }

    private readonly Command[] _buffer;
    private readonly int _mask;

    // Padded to avoid false sharing between producer and consumer cache lines
    private volatile int _head; // Written by producer (main thread)
    private volatile int _tail; // Written by consumer (sim thread)

    /// <summary>
    /// Create a SPSC queue with the given capacity (rounded up to next power of 2).
    /// </summary>
    public CommandQueue(int capacity = 1024)
    {
        // Round up to power of 2
        int size = 1;
        while (size < capacity) size <<= 1;

        _buffer = new Command[size];
        _mask = size - 1;
        _head = 0;
        _tail = 0;
    }

    /// <summary>Number of commands currently in the queue.</summary>
    public int Count
    {
        get
        {
            int h = _head;
            int t = _tail;
            return (h - t) & _mask;
        }
    }

    public bool IsEmpty => _head == _tail;

    /// <summary>
    /// Enqueue a command. Called by the main thread (producer).
    /// Returns false if the queue is full.
    /// </summary>
    public bool TryEnqueue(Command cmd)
    {
        int h = _head;
        int next = (h + 1) & _mask;

        // Full check: next write position would overlap the read position
        if (next == Volatile.Read(ref _tail))
            return false;

        _buffer[h] = cmd;

        // Memory fence: ensure the command data is visible before advancing head
        Volatile.Write(ref _head, next);
        return true;
    }

    /// <summary>
    /// Dequeue a command. Called by the simulation thread (consumer).
    /// Returns false if the queue is empty.
    /// </summary>
    public bool TryDequeue(out Command cmd)
    {
        int t = _tail;

        if (t == Volatile.Read(ref _head))
        {
            cmd = default;
            return false;
        }

        cmd = _buffer[t];
        int next = (t + 1) & _mask;

        // Memory fence: ensure we read the command before advancing tail
        Volatile.Write(ref _tail, next);
        return true;
    }

    /// <summary>
    /// Drain all pending commands into the provided list. Called by the simulation thread.
    /// </summary>
    public int DrainTo(List<Command> output, int maxCount = int.MaxValue)
    {
        int drained = 0;
        while (drained < maxCount && TryDequeue(out var cmd))
        {
            output.Add(cmd);
            drained++;
        }
        return drained;
    }

    // Convenience enqueue methods for common commands

    public bool EnqueuePlaceRoad(int x, int y, int endX, int endY) =>
        TryEnqueue(new Command
        {
            Type = CommandType.PlaceRoad,
            X = x, Y = y,
            Width = endX, Height = endY,
        });

    public bool EnqueuePlaceBuilding(int x, int y, ushort buildingType) =>
        TryEnqueue(new Command
        {
            Type = CommandType.PlaceBuilding,
            X = x, Y = y,
            DataId = buildingType,
        });

    public bool EnqueuePlaceZone(int x, int y, int w, int h, ushort zoneType) =>
        TryEnqueue(new Command
        {
            Type = CommandType.PlaceZone,
            X = x, Y = y,
            Width = w, Height = h,
            DataId = zoneType,
        });

    public bool EnqueueBulldoze(int x, int y, int w, int h) =>
        TryEnqueue(new Command
        {
            Type = CommandType.Bulldoze,
            X = x, Y = y,
            Width = w, Height = h,
        });

    public bool EnqueueSetTaxRate(ushort taxCategory, float rate) =>
        TryEnqueue(new Command
        {
            Type = CommandType.SetTaxRate,
            DataId = taxCategory,
            DataValue = rate,
        });

    public bool EnqueueTakeLoan(float amount) =>
        TryEnqueue(new Command
        {
            Type = CommandType.TakeLoan,
            DataValue = amount,
        });

    public bool EnqueueRepayLoan(float amount) =>
        TryEnqueue(new Command
        {
            Type = CommandType.RepayLoan,
            DataValue = amount,
        });

    public bool EnqueueSetResearch(int techId) =>
        TryEnqueue(new Command
        {
            Type = CommandType.SetResearch,
            DataId = (ushort)techId,
        });

    public bool EnqueueToggleOrdinance(int ordinanceId) =>
        TryEnqueue(new Command
        {
            Type = CommandType.ToggleOrdinance,
            DataId = (ushort)ordinanceId,
        });

    public bool EnqueueTerraform(int x, int y, byte targetElevation) =>
        TryEnqueue(new Command
        {
            Type = CommandType.Terraform,
            X = x,
            Y = y,
            DataByte = targetElevation,
        });

    public bool EnqueueSetGameSpeed(int speed) =>
        TryEnqueue(new Command
        {
            Type = CommandType.SetGameSpeed,
            DataId = (ushort)speed,
        });

    public bool EnqueueProposeLaw(int lawId) =>
        TryEnqueue(new Command
        {
            Type = CommandType.ProposeLaw,
            DataId = (ushort)lawId,
        });

    public bool EnqueueRepealLaw(int lawId) =>
        TryEnqueue(new Command
        {
            Type = CommandType.RepealLaw,
            DataId = (ushort)lawId,
        });
}
