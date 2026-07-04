using System.Runtime.InteropServices;
using SDL2;

namespace Forge.Engine.Audio;

/// <summary>
/// Music streaming player with ring buffer for longer audio tracks.
/// Loads WAV files into memory and streams them through the audio callback
/// with support for looping and fade-out.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    public enum MusicMood { Peaceful, Building, Busy, Crisis }

    private readonly int _sampleRate;
    private readonly int _channelCount;

    private MusicMood _currentMood = MusicMood.Peaceful;
    private float _fadeTime = 2.0f;

    // Current music state
    private short[]? _musicSamples;     // Full loaded track (interleaved S16 stereo)
    private int _musicFrameCount;       // Total frames in the loaded track
    private int _musicPosition;         // Current playback frame position
    private bool _musicPlaying;
    private bool _musicLoop;
    private string? _currentPath;

    // Fade state
    private bool _fading;
    private float _fadeVolume = 1.0f;     // Current fade multiplier (1.0 = full, 0.0 = silent)
    private float _fadeRate;              // Volume decrease per second

    // Volume
    private float _volume = 0.7f;

    private readonly object _lock = new();

    public MusicMood CurrentMood => _currentMood;
    public float FadeTime { get => _fadeTime; set => _fadeTime = System.Math.Max(0.1f, value); }
    public bool IsPlaying => _musicPlaying;

    public float Volume
    {
        get => _volume;
        set => _volume = System.Math.Clamp(value, 0f, 1f);
    }

    public MusicPlayer(int sampleRate, int channelCount)
    {
        _sampleRate = sampleRate;
        _channelCount = channelCount;
    }

    /// <summary>
    /// Load and play a WAV file as music. Replaces any currently playing track.
    /// </summary>
    public void Play(string filePath, bool loop = true)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[MusicPlayer] File not found: {filePath}");
            return;
        }

        // Load the WAV
        var loadResult = SDL.SDL_LoadWAV(
            filePath, out var wavSpec, out var wavBuffer, out uint wavLength);

        if (loadResult == IntPtr.Zero)
        {
            Console.WriteLine($"[MusicPlayer] Failed to load music '{filePath}': {SDL.SDL_GetError()}");
            return;
        }

        try
        {
            // Copy raw PCM data from the loaded WAV buffer.
            // SDL_BuildAudioCVT/SDL_ConvertAudio are not available in ppy.SDL2-CS,
            // so we load WAV data as-is. For best results, use WAV files that already
            // match the device format (S16 stereo at the configured sample rate).
            byte[] pcmData = new byte[wavLength];
            Marshal.Copy(wavBuffer, pcmData, 0, (int)wavLength);

            int sampleCount = pcmData.Length / 2;
            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcmData, 0, samples, 0, pcmData.Length);

            lock (_lock)
            {
                _musicSamples = samples;
                _musicFrameCount = sampleCount / _channelCount;
                _musicPosition = 0;
                _musicPlaying = true;
                _musicLoop = loop;
                _currentPath = filePath;
                _fading = false;
                _fadeVolume = 1.0f;
            }

            Console.WriteLine($"[MusicPlayer] Playing: {filePath} ({_musicFrameCount} frames, loop={loop})");
        }
        finally
        {
            SDL.SDL_FreeWAV(wavBuffer);
        }
    }

    /// <summary>
    /// Set the mood, which can be used to select different music tracks or stem mixes.
    /// </summary>
    public void SetMood(MusicMood mood)
    {
        if (mood == _currentMood) return;
        _currentMood = mood;
    }

    /// <summary>
    /// Pause music playback.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _musicPlaying = false;
        }
    }

    /// <summary>
    /// Resume paused music playback.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (_musicSamples != null)
                _musicPlaying = true;
        }
    }

    /// <summary>
    /// Stop music with an optional fade-out. If fadeOutSeconds is 0, stops immediately.
    /// </summary>
    public void Stop(float fadeOutSeconds = 0f)
    {
        lock (_lock)
        {
            if (!_musicPlaying) return;

            if (fadeOutSeconds <= 0f)
            {
                _musicPlaying = false;
                _musicSamples = null;
                _musicPosition = 0;
                _fading = false;
                _fadeVolume = 1.0f;
            }
            else
            {
                _fading = true;
                _fadeRate = 1.0f / fadeOutSeconds;
            }
        }
    }

    /// <summary>
    /// Update fade state. Call once per frame from the main thread.
    /// </summary>
    public void Update(float dt)
    {
        lock (_lock)
        {
            if (!_fading || !_musicPlaying)
                return;

            _fadeVolume -= _fadeRate * dt;
            if (_fadeVolume <= 0f)
            {
                _fadeVolume = 0f;
                _musicPlaying = false;
                _musicSamples = null;
                _musicPosition = 0;
                _fading = false;
                _fadeVolume = 1.0f;
            }
        }
    }

    /// <summary>
    /// Fill the mix buffer with music data. Called from the audio callback thread.
    /// Adds music samples (scaled by volume and fade) to the existing contents of the buffer.
    /// </summary>
    internal void FillBuffer(float[] mixBuffer, int frameCount, float externalVolume)
    {
        lock (_lock)
        {
            if (!_musicPlaying || _musicSamples == null)
                return;

            float vol = externalVolume * _fadeVolume;
            int remaining = frameCount;
            int outOffset = 0;

            while (remaining > 0)
            {
                int framesAvailable = _musicFrameCount - _musicPosition;
                if (framesAvailable <= 0)
                {
                    if (_musicLoop)
                    {
                        _musicPosition = 0;
                        framesAvailable = _musicFrameCount;
                    }
                    else
                    {
                        _musicPlaying = false;
                        break;
                    }
                }

                int framesToCopy = System.Math.Min(remaining, framesAvailable);
                int srcIdx = _musicPosition * _channelCount;

                for (int f = 0; f < framesToCopy; f++)
                {
                    int si = srcIdx + f * _channelCount;
                    int di = outOffset + f * _channelCount;

                    for (int c = 0; c < _channelCount; c++)
                    {
                        mixBuffer[di + c] += _musicSamples[si + c] * vol;
                    }
                }

                _musicPosition += framesToCopy;
                remaining -= framesToCopy;
                outOffset += framesToCopy * _channelCount;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _musicPlaying = false;
            _musicSamples = null;
        }
    }
}
