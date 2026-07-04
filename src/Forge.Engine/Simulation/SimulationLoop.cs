using System.Diagnostics;
using Forge.Engine.Core;

namespace Forge.Engine.Simulation;

/// <summary>
/// Dedicated simulation thread with tiered tick rates:
/// - Traffic: 2 ticks/sec (500ms)
/// - Economy: 1 tick/day (game day)
/// - Population: 1 tick/month (game month)
///
/// The simulation produces snapshots via double-buffering for the render thread.
/// Player commands are consumed from a lock-free SPSC queue.
/// </summary>
public sealed class SimulationLoop : IDisposable
{
    private readonly Config _config;
    private readonly WorldState _state;
    private readonly CommandQueue _commandQueue;
    private readonly DoubleBuffer<SimSnapshot> _snapshotBuffer;

    private Thread? _thread;
    private volatile bool _running;
    private volatile int _speedMultiplier = 1; // 0 = paused

    // Tiered tick accumulators (in simulation seconds)
    private double _trafficAccumulator;
    private double _dayAccumulator;

    // Reusable list for draining commands (avoids GC pressure at 30Hz)
    private readonly List<CommandQueue.Command> _commandDrainList = new(64);

    private const double TrafficInterval = 0.5;  // 2 ticks per second
    private const double DayInterval = 10.0;     // 1 game day = 10 real seconds at 1x speed

    /// <summary>Delegate for game-specific simulation systems.</summary>
    public delegate void TickHandler(WorldState state, double dt);

    /// <summary>Called every traffic tick (2/sec).</summary>
    public event TickHandler? OnTrafficTick;

    /// <summary>Called every game day.</summary>
    public event TickHandler? OnDayTick;

    /// <summary>Called every game month.</summary>
    public event TickHandler? OnMonthTick;

    /// <summary>Called for each player command.</summary>
    public event Action<WorldState, CommandQueue.Command>? OnCommand;

    public WorldState State => _state;
    public CommandQueue Commands => _commandQueue;

    /// <summary>Read the latest snapshot on the render thread.</summary>
    public SimSnapshot CurrentSnapshot => _snapshotBuffer.Front;

    public SimulationLoop(Config config)
    {
        _config = config;
        _state = new WorldState(config.WorldSize);
        _commandQueue = new CommandQueue();

        var snap1 = SimSnapshot.CaptureFrom(_state);
        var snap2 = SimSnapshot.CaptureFrom(_state);
        _snapshotBuffer = new DoubleBuffer<SimSnapshot>(snap1, snap2);
    }

    /// <summary>
    /// Start the simulation on a dedicated background thread.
    /// </summary>
    public void Start()
    {
        if (_thread != null) return;

        _running = true;
        _thread = new Thread(RunLoop)
        {
            Name = "SimulationThread",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        Console.WriteLine("[SimulationLoop] Started on dedicated thread.");
    }

    /// <summary>
    /// Stop the simulation thread and wait for it to finish.
    /// </summary>
    public void Stop()
    {
        _running = false;
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
        _thread = null;
        Console.WriteLine("[SimulationLoop] Stopped.");
    }

    public void SetSpeed(int multiplier)
    {
        _speedMultiplier = System.Math.Clamp(multiplier, 0, 4);
    }

    private void RunLoop()
    {
        var stopwatch = Stopwatch.StartNew();
        double previousTime = stopwatch.Elapsed.TotalSeconds;
        const double fixedStep = 1.0 / 30.0; // Sim runs at 30 Hz internally

        while (_running)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double elapsed = currentTime - previousTime;
            previousTime = currentTime;

            // Clamp to prevent spiral
            if (elapsed > 0.1) elapsed = 0.1;

            int speed = _speedMultiplier;
            if (speed == 0)
            {
                // Paused: still process commands but don't advance simulation
                ProcessCommands();
                Thread.Sleep(16); // Don't burn CPU while paused
                continue;
            }

            double scaledElapsed = elapsed * speed;

            // Process player commands
            ProcessCommands();

            // Traffic ticks (2 per sim-second)
            _trafficAccumulator += scaledElapsed;
            while (_trafficAccumulator >= TrafficInterval)
            {
                OnTrafficTick?.Invoke(_state, TrafficInterval);
                _trafficAccumulator -= TrafficInterval;
                _state.TickCount++;
            }

            // Day ticks
            _dayAccumulator += scaledElapsed;
            while (_dayAccumulator >= DayInterval)
            {
                OnDayTick?.Invoke(_state, DayInterval);
                bool newMonth = _state.AdvanceDay();

                if (newMonth)
                {
                    OnMonthTick?.Invoke(_state, DayInterval * 30);
                }

                _dayAccumulator -= DayInterval;
            }

            // Produce snapshot for render thread and atomically publish it
            var snapshot = SimSnapshot.CaptureFrom(_state);
            _snapshotBuffer.SwapIn(snapshot);

            // Sleep to target ~30 Hz sim rate
            double frameTime = stopwatch.Elapsed.TotalSeconds - currentTime;
            double sleepTime = fixedStep - frameTime;
            if (sleepTime > 0.001)
            {
                Thread.Sleep((int)(sleepTime * 1000));
            }
        }
    }

    private void ProcessCommands()
    {
        _commandDrainList.Clear();
        _commandQueue.DrainTo(_commandDrainList, maxCount: 64);

        foreach (var cmd in _commandDrainList)
        {
            OnCommand?.Invoke(_state, cmd);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
