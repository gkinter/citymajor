using System.Text;

namespace Forge.Engine.Network;

/// <summary>
/// All game packets implement this interface for type-safe serialization.
/// Each packet has a unique ID (0-255) and can serialize/deserialize itself.
/// </summary>
public interface IPacket
{
    byte PacketId { get; }
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}

/// <summary>
/// Packet type registry: maps packet IDs to factory functions for deserialization.
/// All packet types are auto-registered at startup.
/// </summary>
public static class PacketRegistry
{
    private static readonly Func<IPacket>?[] Factories = new Func<IPacket>?[256];

    static PacketRegistry()
    {
        Register<CitySnapshotPacket>();
        Register<PlayerCommandPacket>();
        Register<TradeOfferPacket>();
        Register<TradeResponsePacket>();
        Register<ChatMessagePacket>();
        Register<GameSpeedPacket>();
        Register<SyncCheckPacket>();
        Register<HeartbeatPacket>();
    }

    /// <summary>Register a packet type. The packet must have a parameterless constructor.</summary>
    public static void Register<T>() where T : IPacket, new()
    {
        var proto = new T();
        Factories[proto.PacketId] = () => new T();
    }

    /// <summary>Deserialize a packet from raw bytes (including the packet ID prefix).</summary>
    public static IPacket? Deserialize(byte[] data)
    {
        if (data.Length == 0) return null;

        byte id = data[0];
        var factory = Factories[id];
        if (factory == null) return null;

        var packet = factory();
        using var ms = new MemoryStream(data, 1, data.Length - 1);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
        packet.Deserialize(reader);
        return packet;
    }

    /// <summary>Serialize a packet to raw bytes (with packet ID prefix).</summary>
    public static byte[] Serialize(IPacket packet)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(packet.PacketId);
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        packet.Serialize(writer);
        return ms.ToArray();
    }
}

// ==========================================================================
// Packet implementations
// ==========================================================================

/// <summary>
/// Quarterly city state snapshot. Contains compressed population, budget, and
/// key metrics. Sent from server to all clients periodically (~2KB).
/// </summary>
public sealed class CitySnapshotPacket : IPacket
{
    public byte PacketId => 10;

    public uint GameTick;
    public int Population;
    public long CityFunds;
    public float Happiness;
    public float TrafficFlow;
    public float PowerUtilization;
    public float WaterUtilization;
    public int ResidentialDemand;
    public int CommercialDemand;
    public int IndustrialDemand;
    public byte[] CompressedTileDeltas = Array.Empty<byte>();

    public void Serialize(BinaryWriter w)
    {
        w.Write(GameTick);
        w.Write(Population);
        w.Write(CityFunds);
        w.Write(Happiness);
        w.Write(TrafficFlow);
        w.Write(PowerUtilization);
        w.Write(WaterUtilization);
        w.Write(ResidentialDemand);
        w.Write(CommercialDemand);
        w.Write(IndustrialDemand);
        w.Write(CompressedTileDeltas.Length);
        w.Write(CompressedTileDeltas);
    }

    public void Deserialize(BinaryReader r)
    {
        GameTick = r.ReadUInt32();
        Population = r.ReadInt32();
        CityFunds = r.ReadInt64();
        Happiness = r.ReadSingle();
        TrafficFlow = r.ReadSingle();
        PowerUtilization = r.ReadSingle();
        WaterUtilization = r.ReadSingle();
        ResidentialDemand = r.ReadInt32();
        CommercialDemand = r.ReadInt32();
        IndustrialDemand = r.ReadInt32();
        int len = r.ReadInt32();
        CompressedTileDeltas = r.ReadBytes(len);
    }
}

/// <summary>
/// Player command: place road, zone area, build, bulldoze, etc.
/// Sent from client to server. The server validates and rebroadcasts.
/// </summary>
public sealed class PlayerCommandPacket : IPacket
{
    public byte PacketId => 20;

    public byte CommandType;
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public ushort DataId;
    public float DataValue;
    public byte PlayerId;

    public void Serialize(BinaryWriter w)
    {
        w.Write(CommandType);
        w.Write(X);
        w.Write(Y);
        w.Write(Width);
        w.Write(Height);
        w.Write(DataId);
        w.Write(DataValue);
        w.Write(PlayerId);
    }

    public void Deserialize(BinaryReader r)
    {
        CommandType = r.ReadByte();
        X = r.ReadInt32();
        Y = r.ReadInt32();
        Width = r.ReadInt32();
        Height = r.ReadInt32();
        DataId = r.ReadUInt16();
        DataValue = r.ReadSingle();
        PlayerId = r.ReadByte();
    }
}

/// <summary>
/// Trade offer between two players: resources, money, or land.
/// </summary>
public sealed class TradeOfferPacket : IPacket
{
    public byte PacketId => 30;

    public uint TradeId;
    public byte FromPlayer;
    public byte ToPlayer;
    public long OfferedMoney;
    public long RequestedMoney;
    public ushort OfferedResourceType;
    public int OfferedResourceAmount;
    public ushort RequestedResourceType;
    public int RequestedResourceAmount;
    public string Message = string.Empty;

    public void Serialize(BinaryWriter w)
    {
        w.Write(TradeId);
        w.Write(FromPlayer);
        w.Write(ToPlayer);
        w.Write(OfferedMoney);
        w.Write(RequestedMoney);
        w.Write(OfferedResourceType);
        w.Write(OfferedResourceAmount);
        w.Write(RequestedResourceType);
        w.Write(RequestedResourceAmount);
        w.Write(Message);
    }

    public void Deserialize(BinaryReader r)
    {
        TradeId = r.ReadUInt32();
        FromPlayer = r.ReadByte();
        ToPlayer = r.ReadByte();
        OfferedMoney = r.ReadInt64();
        RequestedMoney = r.ReadInt64();
        OfferedResourceType = r.ReadUInt16();
        OfferedResourceAmount = r.ReadInt32();
        RequestedResourceType = r.ReadUInt16();
        RequestedResourceAmount = r.ReadInt32();
        Message = r.ReadString();
    }
}

/// <summary>
/// Response to a trade offer: accept, reject, or counter with modifications.
/// </summary>
public sealed class TradeResponsePacket : IPacket
{
    public byte PacketId => 31;

    public uint TradeId;
    public byte ResponseType; // 0=reject, 1=accept, 2=counter
    public byte FromPlayer;
    public long CounterMoney;
    public int CounterResourceAmount;

    public void Serialize(BinaryWriter w)
    {
        w.Write(TradeId);
        w.Write(ResponseType);
        w.Write(FromPlayer);
        w.Write(CounterMoney);
        w.Write(CounterResourceAmount);
    }

    public void Deserialize(BinaryReader r)
    {
        TradeId = r.ReadUInt32();
        ResponseType = r.ReadByte();
        FromPlayer = r.ReadByte();
        CounterMoney = r.ReadInt64();
        CounterResourceAmount = r.ReadInt32();
    }
}

/// <summary>
/// Player chat message. Broadcast to all connected clients.
/// </summary>
public sealed class ChatMessagePacket : IPacket
{
    public byte PacketId => 40;

    public byte PlayerId;
    public string PlayerName = string.Empty;
    public string Message = string.Empty;
    public long TimestampUnixMs;

    public void Serialize(BinaryWriter w)
    {
        w.Write(PlayerId);
        w.Write(PlayerName);
        w.Write(Message);
        w.Write(TimestampUnixMs);
    }

    public void Deserialize(BinaryReader r)
    {
        PlayerId = r.ReadByte();
        PlayerName = r.ReadString();
        Message = r.ReadString();
        TimestampUnixMs = r.ReadInt64();
    }
}

/// <summary>
/// Synchronize game speed across all clients. Sent by host.
/// </summary>
public sealed class GameSpeedPacket : IPacket
{
    public byte PacketId => 50;

    public byte Speed; // 0=paused, 1=normal, 2=fast, 3=ultra
    public uint CurrentTick;

    public void Serialize(BinaryWriter w)
    {
        w.Write(Speed);
        w.Write(CurrentTick);
    }

    public void Deserialize(BinaryReader r)
    {
        Speed = r.ReadByte();
        CurrentTick = r.ReadUInt32();
    }
}

/// <summary>
/// CRC32 desync detection. Each client computes a checksum of critical simulation
/// state and sends it to the server. If checksums diverge, the server triggers
/// a full state resync.
/// </summary>
public sealed class SyncCheckPacket : IPacket
{
    public byte PacketId => 60;

    public uint GameTick;
    public uint Crc32Population;
    public uint Crc32Budget;
    public uint Crc32Roads;
    public uint Crc32Buildings;

    public void Serialize(BinaryWriter w)
    {
        w.Write(GameTick);
        w.Write(Crc32Population);
        w.Write(Crc32Budget);
        w.Write(Crc32Roads);
        w.Write(Crc32Buildings);
    }

    public void Deserialize(BinaryReader r)
    {
        GameTick = r.ReadUInt32();
        Crc32Population = r.ReadUInt32();
        Crc32Budget = r.ReadUInt32();
        Crc32Roads = r.ReadUInt32();
        Crc32Buildings = r.ReadUInt32();
    }

    /// <summary>
    /// Compute a CRC32 checksum over the given byte span using the standard
    /// CRC-32/ISO-HDLC polynomial (0xEDB88320 reflected).
    /// </summary>
    public static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                uint mask = (uint)(-(int)(crc & 1));
                crc = (crc >> 1) ^ (0xEDB88320u & mask);
            }
        }
        return ~crc;
    }
}

/// <summary>
/// Keepalive heartbeat with round-trip ping measurement.
/// Sent periodically (every 1-2 seconds) by both client and server.
/// </summary>
public sealed class HeartbeatPacket : IPacket
{
    public byte PacketId => 70;

    public long SentTimestampMs;
    public long EchoTimestampMs; // 0 on initial send; set to SentTimestampMs in echo response
    public byte PlayerId;

    public void Serialize(BinaryWriter w)
    {
        w.Write(SentTimestampMs);
        w.Write(EchoTimestampMs);
        w.Write(PlayerId);
    }

    public void Deserialize(BinaryReader r)
    {
        SentTimestampMs = r.ReadInt64();
        EchoTimestampMs = r.ReadInt64();
        PlayerId = r.ReadByte();
    }
}
