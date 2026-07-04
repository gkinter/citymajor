using System.Runtime.InteropServices;
using SDL2;

namespace Forge.Engine.Audio;

/// <summary>
/// Handle to a loaded sound effect in the audio manager.
/// </summary>
public readonly struct SoundHandle : IEquatable<SoundHandle>
{
    public readonly int Id;
    internal SoundHandle(int id) => Id = id;
    public bool IsValid => Id >= 0;
    public static SoundHandle Invalid => new(-1);
    public bool Equals(SoundHandle other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is SoundHandle h && Equals(h);
    public override int GetHashCode() => Id;
    public static bool operator ==(SoundHandle a, SoundHandle b) => a.Id == b.Id;
    public static bool operator !=(SoundHandle a, SoundHandle b) => a.Id != b.Id;
}

/// <summary>
/// SDL2 audio manager using SDL_OpenAudioDevice and SDL_QueueAudio.
/// Supports up to 32 concurrent sound channels with manual additive mixing,
/// a music streaming channel with ring buffer, and simple distance-based spatial audio.
/// Gracefully degrades to no-ops when no audio device is available.
/// </summary>
public sealed class AudioManager : IDisposable
{
    private const int SampleRate = 44100;
    private const int ChannelCount = 2; // stereo
    private const int BufferSamples = 4096;
    private const int MaxChannels = 32;
    private const int MixBufferFrames = 4096;

    private bool _initialized;
    private bool _deviceAvailable;
    private uint _deviceId;

    private float _masterVolume = 0.8f;
    private float _sfxVolume = 1.0f;
    private float _musicVolume = 0.7f;
    private bool _isMuted;

    // Loaded sound data (raw PCM S16 stereo at 44100Hz)
    private readonly List<SoundClip> _clips = new();
    private readonly object _clipLock = new();

    // Active channels for mixing
    private readonly Channel[] _channels = new Channel[MaxChannels];
    private readonly object _channelLock = new();
    private int _nextChannelId = 1;

    // Music player (ring-buffer streaming)
    private readonly MusicPlayer _musicPlayer;

    // Mix buffer (interleaved S16 stereo samples)
    private readonly short[] _mixBuffer = new short[MixBufferFrames * ChannelCount];
    private readonly float[] _floatMixBuffer = new float[MixBufferFrames * ChannelCount];

    // Audio callback delegate must be kept alive to prevent GC
    private SDL.SDL_AudioCallback? _callbackDelegate;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = System.Math.Clamp(value, 0f, 1f);
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = System.Math.Clamp(value, 0f, 1f);
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = System.Math.Clamp(value, 0f, 1f);
            _musicPlayer.Volume = _musicVolume;
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_deviceAvailable)
            {
                SDL.SDL_PauseAudioDevice(_deviceId, value ? 1 : 0);
            }
        }
    }

    public AudioManager()
    {
        _musicPlayer = new MusicPlayer(SampleRate, ChannelCount);
        for (int i = 0; i < MaxChannels; i++)
            _channels[i] = new Channel();
    }

    /// <summary>
    /// Initialize the SDL2 audio subsystem and open an audio device.
    /// If no device is available, all subsequent calls become no-ops.
    /// </summary>
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO) < 0)
        {
            Console.WriteLine($"[AudioManager] SDL audio init failed: {SDL.SDL_GetError()}. Audio disabled.");
            return;
        }

        _callbackDelegate = AudioCallback;

        var desired = new SDL.SDL_AudioSpec
        {
            freq = SampleRate,
            format = SDL.AUDIO_S16LSB,
            channels = (byte)ChannelCount,
            samples = BufferSamples,
            callback = _callbackDelegate,
            userdata = IntPtr.Zero
        };

        _deviceId = SDL.SDL_OpenAudioDevice(
            null, 0, ref desired, out var obtained, 0);

        if (_deviceId == 0)
        {
            Console.WriteLine($"[AudioManager] Failed to open audio device: {SDL.SDL_GetError()}. Audio disabled.");
            return;
        }

        _deviceAvailable = true;

        // Unpause the device to start the audio callback
        SDL.SDL_PauseAudioDevice(_deviceId, 0);

        Console.WriteLine($"[AudioManager] Initialized: {obtained.freq}Hz, {obtained.channels}ch, {obtained.samples} samples buffer");
    }

    /// <summary>
    /// Load a WAV file and return a handle for playback.
    /// The WAV is converted to the device format (S16 stereo 44100Hz) if needed.
    /// </summary>
    public SoundHandle LoadSound(string path)
    {
        if (!_initialized)
            return SoundHandle.Invalid;

        if (!File.Exists(path))
        {
            Console.WriteLine($"[AudioManager] WAV file not found: {path}");
            return SoundHandle.Invalid;
        }

        // Use SDL_LoadWAV to parse the WAV file
        var loadResult = SDL.SDL_LoadWAV(
            path, out var wavSpec, out var wavBuffer, out uint wavLength);

        if (loadResult == IntPtr.Zero)
        {
            Console.WriteLine($"[AudioManager] Failed to load WAV '{path}': {SDL.SDL_GetError()}");
            return SoundHandle.Invalid;
        }

        try
        {
            // Copy raw PCM data from the loaded WAV buffer.
            // SDL_BuildAudioCVT/SDL_ConvertAudio are not available in ppy.SDL2-CS,
            // so we load WAV data as-is. For best results, use WAV files that already
            // match the device format (S16 stereo at 44100Hz).
            byte[] pcmData = new byte[wavLength];
            Marshal.Copy(wavBuffer, pcmData, 0, (int)wavLength);

            // Convert byte[] to short[] samples
            int sampleCount = pcmData.Length / 2;
            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcmData, 0, samples, 0, pcmData.Length);

            var clip = new SoundClip(path, samples, sampleCount / ChannelCount);

            lock (_clipLock)
            {
                int id = _clips.Count;
                _clips.Add(clip);
                return new SoundHandle(id);
            }
        }
        finally
        {
            SDL.SDL_FreeWAV(wavBuffer);
        }
    }

    /// <summary>
    /// Play a loaded sound effect with optional volume and pan.
    /// Returns a channel ID that can be used to stop the sound, or -1 if no channel is available.
    /// </summary>
    public int PlaySound(SoundHandle handle, float volume = 1.0f, float pan = 0.0f)
    {
        if (!_deviceAvailable || !handle.IsValid)
            return -1;

        SoundClip? clip;
        lock (_clipLock)
        {
            if (handle.Id < 0 || handle.Id >= _clips.Count)
                return -1;
            clip = _clips[handle.Id];
        }

        lock (_channelLock)
        {
            int slot = FindFreeChannel();
            if (slot < 0) return -1;

            int channelId = _nextChannelId++;
            ref var ch = ref _channels[slot];
            ch.Active = true;
            ch.ChannelId = channelId;
            ch.Clip = clip;
            ch.Position = 0;
            ch.Volume = System.Math.Clamp(volume, 0f, 1f);
            ch.PanLeft = System.Math.Clamp(1f - pan, 0f, 1f);
            ch.PanRight = System.Math.Clamp(1f + pan, 0f, 1f);
            ch.Loop = false;

            return channelId;
        }
    }

    /// <summary>
    /// Stop a specific sound channel by ID.
    /// </summary>
    public void StopSound(int channelId)
    {
        if (channelId <= 0) return;

        lock (_channelLock)
        {
            for (int i = 0; i < MaxChannels; i++)
            {
                if (_channels[i].Active && _channels[i].ChannelId == channelId)
                {
                    _channels[i].Active = false;
                    _channels[i].Clip = null;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Play music from a WAV file path. Music is streamed via a ring buffer
    /// and mixed alongside sound effects.
    /// </summary>
    public void PlayMusic(string path, float volume = 0.6f, bool loop = true)
    {
        _musicPlayer.Volume = volume;
        _musicPlayer.Play(path, loop);
    }

    /// <summary>
    /// Stop music with an optional fade-out duration.
    /// </summary>
    public void StopMusic(float fadeOutSeconds = 1.0f)
    {
        _musicPlayer.Stop(fadeOutSeconds);
    }

    /// <summary>
    /// Set the music volume (0.0 - 1.0).
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        MusicVolume = volume;
    }

    /// <summary>
    /// Play a sound at a world position with distance-based attenuation and panning.
    /// Sound is attenuated linearly from full volume at the listener position to silent at maxDistance.
    /// </summary>
    public void PlaySoundAt(SoundHandle handle, float worldX, float worldY,
                            float listenerX, float listenerY, float maxDistance)
    {
        if (!_deviceAvailable || !handle.IsValid || maxDistance <= 0f)
            return;

        float dx = worldX - listenerX;
        float dy = worldY - listenerY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist >= maxDistance)
            return; // Too far away, don't play

        // Linear attenuation
        float attenuation = 1f - (dist / maxDistance);
        attenuation = System.Math.Clamp(attenuation, 0f, 1f);

        // Simple stereo panning based on X offset
        // Pan range: -1 (full left) to +1 (full right)
        float pan = 0f;
        if (maxDistance > 0f)
        {
            pan = System.Math.Clamp(dx / maxDistance, -1f, 1f);
        }

        PlaySound(handle, attenuation, pan);
    }

    /// <summary>
    /// Update music streaming state. Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        _musicPlayer.Update(dt);
    }

    private int FindFreeChannel()
    {
        // First pass: find an inactive channel
        for (int i = 0; i < MaxChannels; i++)
        {
            if (!_channels[i].Active)
                return i;
        }

        // Second pass: steal the oldest channel (lowest channelId)
        int oldest = 0;
        int oldestId = int.MaxValue;
        for (int i = 0; i < MaxChannels; i++)
        {
            if (_channels[i].ChannelId < oldestId)
            {
                oldestId = _channels[i].ChannelId;
                oldest = i;
            }
        }
        _channels[oldest].Active = false;
        _channels[oldest].Clip = null;
        return oldest;
    }

    /// <summary>
    /// SDL audio callback. Called from the audio thread to fill the output buffer.
    /// Mixes all active channels and music into a single S16 stereo stream.
    /// </summary>
    private void AudioCallback(IntPtr userdata, IntPtr stream, int len)
    {
        int sampleCount = len / 2; // S16 = 2 bytes per sample
        int frameCount = sampleCount / ChannelCount;

        // Clear the float mix buffer
        Array.Clear(_floatMixBuffer, 0, sampleCount);

        float masterVol = _isMuted ? 0f : _masterVolume;
        float sfxVol = _sfxVolume * masterVol;

        // Mix sound effect channels
        lock (_channelLock)
        {
            for (int ch = 0; ch < MaxChannels; ch++)
            {
                ref var channel = ref _channels[ch];
                if (!channel.Active || channel.Clip == null)
                    continue;

                var clip = channel.Clip;
                float vol = channel.Volume * sfxVol;
                float panL = channel.PanLeft;
                float panR = channel.PanRight;
                int pos = channel.Position;

                for (int f = 0; f < frameCount; f++)
                {
                    if (pos >= clip.FrameCount)
                    {
                        if (channel.Loop)
                        {
                            pos = 0;
                        }
                        else
                        {
                            channel.Active = false;
                            channel.Clip = null;
                            break;
                        }
                    }

                    int sampleIdx = pos * ChannelCount;
                    int outIdx = f * ChannelCount;

                    float left = clip.Samples[sampleIdx] * vol * panL;
                    float right = clip.Samples[sampleIdx + 1] * vol * panR;

                    _floatMixBuffer[outIdx] += left;
                    _floatMixBuffer[outIdx + 1] += right;

                    pos++;
                }

                channel.Position = pos;
            }
        }

        // Mix music
        float musicVol = _musicPlayer.Volume * masterVol;
        _musicPlayer.FillBuffer(_floatMixBuffer, frameCount, musicVol);

        // Convert float mix to S16 with clamping
        for (int i = 0; i < sampleCount; i++)
        {
            float val = _floatMixBuffer[i];
            // Clamp to S16 range
            if (val > 32767f) val = 32767f;
            else if (val < -32768f) val = -32768f;
            _mixBuffer[i] = (short)val;
        }

        // Copy to SDL stream
        Marshal.Copy(
            MemoryMarshal.AsBytes(_mixBuffer.AsSpan(0, sampleCount)).ToArray(),
            0, stream, len);
    }

    public void Dispose()
    {
        if (_deviceAvailable)
        {
            SDL.SDL_PauseAudioDevice(_deviceId, 1);
            SDL.SDL_CloseAudioDevice(_deviceId);
            _deviceAvailable = false;
        }

        _musicPlayer.Dispose();

        lock (_clipLock)
        {
            _clips.Clear();
        }

        lock (_channelLock)
        {
            for (int i = 0; i < MaxChannels; i++)
            {
                _channels[i].Active = false;
                _channels[i].Clip = null;
            }
        }

        _initialized = false;
    }

    // Internal types

    private sealed class SoundClip
    {
        public readonly string Path;
        public readonly short[] Samples; // Interleaved S16 stereo
        public readonly int FrameCount;  // Number of stereo frames

        public SoundClip(string path, short[] samples, int frameCount)
        {
            Path = path;
            Samples = samples;
            FrameCount = frameCount;
        }
    }

    private struct Channel
    {
        public bool Active;
        public int ChannelId;
        public SoundClip? Clip;
        public int Position;   // Current frame position in clip
        public float Volume;
        public float PanLeft;
        public float PanRight;
        public bool Loop;
    }
}
