using System.Collections.Concurrent;

namespace Forge.Engine.Simulation;

/// <summary>
/// Fork-join parallel job execution system. Distributes work across a thread pool
/// for data-parallel simulation tasks (pathfinding, building updates, etc.).
/// </summary>
public sealed class JobSystem : IDisposable
{
    private readonly int _workerCount;
    private readonly Thread[] _workers;
    private readonly BlockingCollection<JobBatch> _jobQueue;
    private volatile bool _running;

    public int WorkerCount => _workerCount;

    public JobSystem(int? workerCount = null)
    {
        _workerCount = workerCount ?? System.Math.Max(1, Environment.ProcessorCount - 2);
        _workers = new Thread[_workerCount];
        _jobQueue = new BlockingCollection<JobBatch>(boundedCapacity: 256);
        _running = true;

        for (int i = 0; i < _workerCount; i++)
        {
            int id = i;
            _workers[i] = new Thread(() => WorkerLoop(id))
            {
                Name = $"JobWorker-{id}",
                IsBackground = true,
            };
            _workers[i].Start();
        }

        Console.WriteLine($"[JobSystem] Started with {_workerCount} workers.");
    }

    /// <summary>
    /// A batch of work to execute in parallel. Each item in the range [start, end)
    /// is processed by the action delegate.
    /// </summary>
    private sealed class JobBatch
    {
        public required Action<int> Action;
        public required int Start;
        public required int End;
        public required CountdownEvent Completion;
    }

    /// <summary>
    /// Execute an action for each index in [0, count) across worker threads.
    /// Blocks until all work is complete (fork-join).
    /// </summary>
    public void ParallelFor(int count, Action<int> action)
    {
        if (count <= 0) return;

        // For small work, just run inline
        if (count <= _workerCount * 2)
        {
            for (int i = 0; i < count; i++)
                action(i);
            return;
        }

        // Split into batches, one per worker
        int batchSize = (count + _workerCount - 1) / _workerCount;
        int batchCount = (count + batchSize - 1) / batchSize;

        using var completion = new CountdownEvent(batchCount);

        for (int b = 0; b < batchCount; b++)
        {
            int start = b * batchSize;
            int end = System.Math.Min(start + batchSize, count);

            _jobQueue.Add(new JobBatch
            {
                Action = action,
                Start = start,
                End = end,
                Completion = completion,
            });
        }

        // Wait for all batches to complete (join)
        completion.Wait();
    }

    /// <summary>
    /// Execute multiple independent actions in parallel. Blocks until all complete.
    /// </summary>
    public void ParallelInvoke(params Action[] actions)
    {
        if (actions.Length == 0) return;
        if (actions.Length == 1)
        {
            actions[0]();
            return;
        }

        using var completion = new CountdownEvent(actions.Length);

        for (int i = 0; i < actions.Length; i++)
        {
            int idx = i;
            _jobQueue.Add(new JobBatch
            {
                Action = _ => actions[idx](),
                Start = 0,
                End = 1,
                Completion = completion,
            });
        }

        completion.Wait();
    }

    private void WorkerLoop(int id)
    {
        while (_running)
        {
            try
            {
                if (!_jobQueue.TryTake(out var batch, millisecondsTimeout: 100))
                    continue;

                for (int i = batch.Start; i < batch.End; i++)
                {
                    batch.Action(i);
                }

                batch.Completion.Signal();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JobSystem] Worker {id} error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _jobQueue.CompleteAdding();

        foreach (var worker in _workers)
        {
            worker.Join(timeout: TimeSpan.FromSeconds(1));
        }

        _jobQueue.Dispose();
    }
}
