namespace Forge.Engine.Audio;

/// <summary>
/// SFX pooling with priority-based channel allocation and per-sound concurrency limits.
/// Wraps AudioManager to prevent the same sound from monopolizing all channels.
/// </summary>
public sealed class SoundPool : IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly int _maxConcurrentPerSound;

    // Registered sounds: id -> loaded handle
    private readonly Dictionary<string, SoundEntry> _sounds = new();

    // Active playback tracking per sound id
    private readonly Dictionary<string, List<ActivePlay>> _activePlays = new();

    public SoundPool(AudioManager audioManager, int maxConcurrentPerSound = 3)
    {
        _audioManager = audioManager;
        _maxConcurrentPerSound = maxConcurrentPerSound;
    }

    /// <summary>
    /// Load a sound effect from a file and register it with a string ID.
    /// </summary>
    public void Load(string id, string filePath)
    {
        var handle = _audioManager.LoadSound(filePath);
        if (!handle.IsValid)
        {
            Console.WriteLine($"[SoundPool] Failed to load '{id}' from {filePath}");
            return;
        }

        _sounds[id] = new SoundEntry(handle, filePath);
        _activePlays[id] = new List<ActivePlay>();
    }

    /// <summary>
    /// Play a sound effect by ID with priority-based channel stealing.
    /// Higher priority sounds can evict lower priority ones when the per-sound
    /// concurrency limit is reached. Returns the channel ID or -1 if not played.
    /// </summary>
    public int Play(string id, int priority = 0, float volume = 1.0f)
    {
        if (!_sounds.TryGetValue(id, out var entry))
            return -1;

        if (!_activePlays.TryGetValue(id, out var plays))
            return -1;

        // Check concurrency limit for this sound
        if (plays.Count >= _maxConcurrentPerSound)
        {
            // Find the lowest priority active play for this sound
            int lowestIdx = -1;
            int lowestPriority = int.MaxValue;
            for (int i = 0; i < plays.Count; i++)
            {
                if (plays[i].Priority < lowestPriority)
                {
                    lowestPriority = plays[i].Priority;
                    lowestIdx = i;
                }
            }

            // Only steal if the new play has higher priority
            if (lowestIdx >= 0 && priority > lowestPriority)
            {
                _audioManager.StopSound(plays[lowestIdx].ChannelId);
                plays.RemoveAt(lowestIdx);
            }
            else
            {
                // At concurrency limit and no lower-priority sound to steal
                return -1;
            }
        }

        int channelId = _audioManager.PlaySound(entry.Handle, volume);
        if (channelId < 0)
            return -1;

        plays.Add(new ActivePlay(channelId, priority, Environment.TickCount64));
        return channelId;
    }

    /// <summary>
    /// Play a sound at a world position with spatial attenuation.
    /// Subject to the same per-sound concurrency limits.
    /// </summary>
    public void PlayAt(string id, float worldX, float worldY,
                       float listenerX, float listenerY, float maxDistance,
                       int priority = 0)
    {
        if (!_sounds.TryGetValue(id, out var entry))
            return;

        if (!_activePlays.TryGetValue(id, out var plays))
            return;

        // Check concurrency limit
        if (plays.Count >= _maxConcurrentPerSound)
        {
            int lowestIdx = -1;
            int lowestPriority = int.MaxValue;
            for (int i = 0; i < plays.Count; i++)
            {
                if (plays[i].Priority < lowestPriority)
                {
                    lowestPriority = plays[i].Priority;
                    lowestIdx = i;
                }
            }

            if (lowestIdx >= 0 && priority > lowestPriority)
            {
                _audioManager.StopSound(plays[lowestIdx].ChannelId);
                plays.RemoveAt(lowestIdx);
            }
            else
            {
                return;
            }
        }

        _audioManager.PlaySoundAt(entry.Handle, worldX, worldY, listenerX, listenerY, maxDistance);
    }

    /// <summary>
    /// Update active play tracking. Call once per frame to clean up finished sounds.
    /// Since the AudioManager handles playback internally, we use a time-based heuristic
    /// to prune entries that have likely finished playing.
    /// </summary>
    public void Update()
    {
        // Prune active plays that are likely finished.
        // We use a conservative 30-second timeout since we don't get callbacks
        // from the audio thread about channel completion.
        long now = Environment.TickCount64;
        const long MaxSoundDurationMs = 30_000; // 30 seconds max assumed sound length

        foreach (var kvp in _activePlays)
        {
            var plays = kvp.Value;
            for (int i = plays.Count - 1; i >= 0; i--)
            {
                if (now - plays[i].StartTime > MaxSoundDurationMs)
                {
                    plays.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Stop all instances of a specific sound.
    /// </summary>
    public void StopAll(string id)
    {
        if (!_activePlays.TryGetValue(id, out var plays))
            return;

        foreach (var play in plays)
        {
            _audioManager.StopSound(play.ChannelId);
        }
        plays.Clear();
    }

    /// <summary>
    /// Stop all currently playing sounds across all IDs.
    /// </summary>
    public void StopAllSounds()
    {
        foreach (var kvp in _activePlays)
        {
            foreach (var play in kvp.Value)
            {
                _audioManager.StopSound(play.ChannelId);
            }
            kvp.Value.Clear();
        }
    }

    /// <summary>
    /// Check if a sound ID has been loaded.
    /// </summary>
    public bool IsLoaded(string id) => _sounds.ContainsKey(id);

    /// <summary>
    /// Get the number of currently tracked active plays for a sound.
    /// </summary>
    public int ActiveCount(string id) =>
        _activePlays.TryGetValue(id, out var plays) ? plays.Count : 0;

    public void Dispose()
    {
        StopAllSounds();
        _sounds.Clear();
        _activePlays.Clear();
    }

    private readonly struct SoundEntry
    {
        public readonly SoundHandle Handle;
        public readonly string FilePath;

        public SoundEntry(SoundHandle handle, string filePath)
        {
            Handle = handle;
            FilePath = filePath;
        }
    }

    private readonly struct ActivePlay
    {
        public readonly int ChannelId;
        public readonly int Priority;
        public readonly long StartTime;

        public ActivePlay(int channelId, int priority, long startTime)
        {
            ChannelId = channelId;
            Priority = priority;
            StartTime = startTime;
        }
    }
}
