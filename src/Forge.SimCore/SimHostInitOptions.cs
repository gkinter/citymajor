namespace Forge.SimCore;

/// <summary>Initialization profile for <see cref="SimHost"/>.</summary>
public sealed class SimHostInitOptions
{
  public bool SkipStarterCity { get; init; }

  /// <summary>
  /// Unity v1: modern era only — Era 3, starting year 2026, filter technologies.json.
  /// </summary>
  public bool UnityModernProfile { get; init; }

  /// <summary>
  /// Use full <see cref="Forge.Game.Simulation.TrafficSystem"/> (MNL + Frank-Wolfe) instead of
  /// browser-oriented <see cref="Forge.SimWasm.WasmTrafficLite"/>. Default false for WASM / Unity.
  /// </summary>
  public bool UseFullTraffic { get; init; }

  /// <summary>
  /// Override lite traffic zone count. When null, WASM uses <see cref="WasmConfig.TrafficLiteZoneCount"/>;
  /// Unity modern profile uses <see cref="WasmConfig.TrafficLiteZoneCountUnity"/>.
  /// </summary>
  public int? TrafficLiteZoneCount { get; init; }

  /// <summary>Load JSON from disk (Unity editor / standalone).</summary>
  public SimDataPaths? DataPaths { get; init; }

  /// <summary>Load JSON from embedded resources (browser WASM).</summary>
  public Func<string, string>? EmbeddedDataReader { get; init; }
}
