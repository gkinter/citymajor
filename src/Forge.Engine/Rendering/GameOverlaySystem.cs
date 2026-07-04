using Forge.Engine.Data;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Manages 8 data overlay modes that render heatmap colors over the terrain.
/// Only one overlay can be active at a time (radio button behavior).
/// Provides smooth fade transitions (200ms) when toggling overlays.
/// Feeds overlay data to both the terrain shader and the minimap.
///
/// Overlays:
///   F2 = Traffic          (TileData.Traffic)
///   F3 = Land Value       (TileData.LandValue)
///   F4 = Happiness        (per-building, averaged to tile)
///   F5 = Pollution        (TileData.Pollution)
///   F6 = Crime            (TileData.Crime)
///   F7 = Fire Risk        (TileData.FireRisk)
///   F8 = Power Grid       (TileData.PowerGrid, binary 0/1 -> 0.0/1.0)
///   F9 = Water            (TileData.WaterGrid, binary 0/1 -> 0.0/1.0)
/// </summary>
public sealed class GameOverlaySystem : IDisposable
{
    /// <summary>
    /// Overlay identifiers. Values match the u_overlayType uniform in the shader.
    /// </summary>
    public enum OverlayId
    {
        None = 0,
        Traffic = 1,
        LandValue = 2,
        Happiness = 3,
        Pollution = 4,
        Crime = 5,
        FireRisk = 6,
        PowerGrid = 7,
        Water = 8,
    }

    /// <summary>Display names for each overlay (indexed by OverlayId).</summary>
    private static readonly string[] OverlayNames =
    [
        "",            // None
        "Traffic",     // 1
        "Land Value",  // 2
        "Happiness",   // 3
        "Pollution",   // 4
        "Crime",       // 5
        "Fire Risk",   // 6
        "Power Grid",  // 7
        "Water",       // 8
    ];

    private readonly GL _gl;
    private readonly OverlayRenderer _overlayRenderer;
    private readonly int _worldSize;

    // Current overlay state
    private OverlayId _activeOverlay = OverlayId.None;
    private OverlayId _targetOverlay = OverlayId.None;

    // Fade transition
    private float _fadeOpacity = 0f;
    private const float FadeDuration = 0.2f; // 200ms

    // Data buffer for the active overlay (worldSize * worldSize floats, 0.0-1.0)
    private readonly float[] _overlayBuffer;

    // Happiness buffer (computed from building data, not directly from TileData)
    private readonly float[] _happinessBuffer;

    /// <summary>The currently displayed overlay (after fade completes).</summary>
    public OverlayId ActiveOverlay => _activeOverlay;

    /// <summary>The target overlay (what we're fading toward).</summary>
    public OverlayId TargetOverlay => _targetOverlay;

    /// <summary>Current fade opacity (0.0 = invisible, 1.0 = fully visible).</summary>
    public float FadeOpacity => _fadeOpacity;

    /// <summary>Display name of the active overlay, or empty string if none.</summary>
    public string ActiveOverlayName =>
        (int)_activeOverlay < OverlayNames.Length ? OverlayNames[(int)_activeOverlay] : "";

    /// <summary>The raw overlay data buffer for external consumers (e.g., minimap).</summary>
    public float[] OverlayBuffer => _overlayBuffer;

    /// <summary>Whether any overlay is active or fading in/out.</summary>
    public bool IsActive => _activeOverlay != OverlayId.None || _fadeOpacity > 0.001f;

    public GameOverlaySystem(GL gl, int worldSize)
    {
        _gl = gl;
        _worldSize = worldSize;
        _overlayRenderer = new OverlayRenderer(gl);
        _overlayRenderer.Init(worldSize, worldSize);
        _overlayBuffer = new float[worldSize * worldSize];
        _happinessBuffer = new float[worldSize * worldSize];
    }

    /// <summary>
    /// Toggle an overlay. If the same overlay is already active, deactivate it.
    /// If a different overlay is active, switch to the new one.
    /// </summary>
    public void ToggleOverlay(OverlayId overlay)
    {
        if (_targetOverlay == overlay)
        {
            // Same overlay pressed again -> fade out
            _targetOverlay = OverlayId.None;
        }
        else
        {
            // Different overlay -> switch (if currently faded in, will crossfade)
            _targetOverlay = overlay;
            _activeOverlay = overlay;
        }
    }

    /// <summary>
    /// Update fade transition and overlay data. Call once per frame.
    /// </summary>
    /// <param name="dt">Delta time in seconds.</param>
    /// <param name="tiles">Tile data to read overlay values from.</param>
    public void Update(float dt, TileData tiles)
    {
        // Update fade
        if (_targetOverlay != OverlayId.None)
        {
            // Fade in
            _fadeOpacity = MathF.Min(_fadeOpacity + dt / FadeDuration, 1f);
            _activeOverlay = _targetOverlay;
        }
        else
        {
            // Fade out
            _fadeOpacity = MathF.Max(_fadeOpacity - dt / FadeDuration, 0f);
            if (_fadeOpacity <= 0.001f)
            {
                _activeOverlay = OverlayId.None;
                _fadeOpacity = 0f;
            }
        }

        // Update overlay data if active
        if (_activeOverlay != OverlayId.None)
        {
            FillOverlayBuffer(tiles, _activeOverlay);
            _overlayRenderer.Upload(_overlayBuffer);
        }

        // Update the base OverlayRenderer's active overlay type to match
        _overlayRenderer.ActiveOverlay = _activeOverlay switch
        {
            OverlayId.Traffic => OverlayRenderer.OverlayType.Traffic,
            OverlayId.LandValue => OverlayRenderer.OverlayType.LandValue,
            OverlayId.Happiness => OverlayRenderer.OverlayType.Desirability, // Repurpose desirability slot
            OverlayId.Pollution => OverlayRenderer.OverlayType.Pollution,
            OverlayId.Crime => OverlayRenderer.OverlayType.Crime,
            OverlayId.FireRisk => OverlayRenderer.OverlayType.Fire,
            OverlayId.PowerGrid => OverlayRenderer.OverlayType.Power,
            OverlayId.Water => OverlayRenderer.OverlayType.Water,
            _ => OverlayRenderer.OverlayType.None,
        };
        _overlayRenderer.Opacity = _fadeOpacity * 0.5f;
    }

    /// <summary>
    /// Bind the overlay texture for rendering in the terrain shader.
    /// </summary>
    public void Bind(uint textureUnit = 1)
    {
        if (_activeOverlay != OverlayId.None)
        {
            _overlayRenderer.Bind(textureUnit);
        }
    }

    /// <summary>
    /// Get the overlay type integer for the shader uniform (u_overlayType).
    /// </summary>
    public int GetShaderOverlayType() => (int)_activeOverlay;

    /// <summary>
    /// Fill the happiness buffer from building data. Called externally when
    /// simulation snapshots include per-building happiness.
    /// </summary>
    /// <param name="globalHappiness">Global happiness value (0.0-1.0) to fill all tiles with.
    /// In a full implementation, this would be per-building or per-tile.</param>
    public void UpdateHappinessData(float globalHappiness)
    {
        Array.Fill(_happinessBuffer, globalHappiness);
    }

    /// <summary>
    /// Fill the overlay buffer from tile data based on the active overlay type.
    /// </summary>
    private void FillOverlayBuffer(TileData tiles, OverlayId overlay)
    {
        int count = tiles.Count;

        switch (overlay)
        {
            case OverlayId.Traffic:
                Array.Copy(tiles.Traffic, _overlayBuffer, count);
                break;

            case OverlayId.LandValue:
                Array.Copy(tiles.LandValue, _overlayBuffer, count);
                break;

            case OverlayId.Happiness:
                // Use pre-computed happiness buffer
                Array.Copy(_happinessBuffer, _overlayBuffer, count);
                break;

            case OverlayId.Pollution:
                Array.Copy(tiles.Pollution, _overlayBuffer, count);
                break;

            case OverlayId.Crime:
                Array.Copy(tiles.Crime, _overlayBuffer, count);
                break;

            case OverlayId.FireRisk:
                Array.Copy(tiles.FireRisk, _overlayBuffer, count);
                break;

            case OverlayId.PowerGrid:
                // Convert byte (0 or 1) to float (0.0 or 1.0)
                for (int i = 0; i < count; i++)
                {
                    _overlayBuffer[i] = tiles.PowerGrid[i] != 0 ? 1f : 0f;
                }
                break;

            case OverlayId.Water:
                // Convert byte (0 or 1) to float (0.0 or 1.0)
                for (int i = 0; i < count; i++)
                {
                    _overlayBuffer[i] = tiles.WaterGrid[i] != 0 ? 1f : 0f;
                }
                break;

            default:
                Array.Clear(_overlayBuffer, 0, count);
                break;
        }
    }

    public void Dispose()
    {
        _overlayRenderer.Dispose();
    }
}
