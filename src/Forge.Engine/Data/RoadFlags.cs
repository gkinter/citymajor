namespace Forge.Engine.Data;

/// <summary>
/// Bit constants for <see cref="TileData.RoadFlags"/> (bits 0–3 connectivity, 4–5 tier, 6–7 structure).
/// Structure bits are mutually exclusive: none / bridge / tunnel / ramp.
/// </summary>
public static class RoadFlags
{
  public const byte North = 0x01;
  public const byte East = 0x02;
  public const byte South = 0x04;
  public const byte West = 0x08;

  public const byte TierMask = 0x30;
  public const byte Bridge = 0x40;
  public const byte Tunnel = 0x80;
  /// <summary>Highway ramp connector — both structure bits set (mutually exclusive with bridge/tunnel).</summary>
  public const byte Ramp = Bridge | Tunnel; // 0xC0
  public const byte StructureMask = Bridge | Tunnel;

  public const float BridgeCostMultiplier = 1.15f;
  public const float TunnelCostMultiplier = 1.25f;

  public static bool IsBridge(byte flags) => (flags & StructureMask) == Bridge;
  public static bool IsTunnel(byte flags) => (flags & StructureMask) == Tunnel;
  public static bool IsRamp(byte flags) => (flags & StructureMask) == Ramp;
}
