using Forge.Engine.Network;
using Xunit;

namespace Forge.Engine.Tests;

public class PacketSerializationTests
{
    /// <summary>Helper: serialize then deserialize a packet and return the result.</summary>
    private static T RoundTrip<T>(T packet) where T : IPacket
    {
        byte[] data = PacketRegistry.Serialize(packet);
        var deserialized = PacketRegistry.Deserialize(data);
        Assert.NotNull(deserialized);
        Assert.IsType<T>(deserialized);
        return (T)deserialized;
    }

    [Fact]
    public void CitySnapshotPacket_RoundTrip()
    {
        var original = new CitySnapshotPacket
        {
            GameTick = 12345,
            Population = 50000,
            CityFunds = 1_000_000L,
            Happiness = 0.75f,
            TrafficFlow = 0.42f,
            PowerUtilization = 0.88f,
            WaterUtilization = 0.65f,
            ResidentialDemand = 120,
            CommercialDemand = -30,
            IndustrialDemand = 45,
            CompressedTileDeltas = new byte[] { 1, 2, 3, 4, 5 },
        };

        var result = RoundTrip(original);

        Assert.Equal(12345u, result.GameTick);
        Assert.Equal(50000, result.Population);
        Assert.Equal(1_000_000L, result.CityFunds);
        Assert.Equal(0.75f, result.Happiness);
        Assert.Equal(0.42f, result.TrafficFlow);
        Assert.Equal(0.88f, result.PowerUtilization);
        Assert.Equal(0.65f, result.WaterUtilization);
        Assert.Equal(120, result.ResidentialDemand);
        Assert.Equal(-30, result.CommercialDemand);
        Assert.Equal(45, result.IndustrialDemand);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result.CompressedTileDeltas);
    }

    [Fact]
    public void CitySnapshotPacket_EmptyDeltas_RoundTrip()
    {
        var original = new CitySnapshotPacket
        {
            GameTick = 1,
            CompressedTileDeltas = Array.Empty<byte>(),
        };

        var result = RoundTrip(original);
        Assert.Empty(result.CompressedTileDeltas);
    }

    [Fact]
    public void PlayerCommandPacket_RoundTrip()
    {
        var original = new PlayerCommandPacket
        {
            CommandType = 2, // PlaceZone
            X = 100,
            Y = 200,
            Width = 5,
            Height = 5,
            DataId = 3,
            DataValue = 0.15f,
            PlayerId = 7,
        };

        var result = RoundTrip(original);

        Assert.Equal(2, result.CommandType);
        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
        Assert.Equal(5, result.Width);
        Assert.Equal(5, result.Height);
        Assert.Equal(3, result.DataId);
        Assert.Equal(0.15f, result.DataValue);
        Assert.Equal(7, result.PlayerId);
    }

    [Fact]
    public void TradeOfferPacket_RoundTrip()
    {
        var original = new TradeOfferPacket
        {
            TradeId = 999,
            FromPlayer = 1,
            ToPlayer = 2,
            OfferedMoney = 50000,
            RequestedMoney = 0,
            OfferedResourceType = 3,
            OfferedResourceAmount = 100,
            RequestedResourceType = 0,
            RequestedResourceAmount = 0,
            Message = "Trade oil for cash?",
        };

        var result = RoundTrip(original);

        Assert.Equal(999u, result.TradeId);
        Assert.Equal(1, result.FromPlayer);
        Assert.Equal(2, result.ToPlayer);
        Assert.Equal(50000L, result.OfferedMoney);
        Assert.Equal(0L, result.RequestedMoney);
        Assert.Equal(3, result.OfferedResourceType);
        Assert.Equal(100, result.OfferedResourceAmount);
        Assert.Equal("Trade oil for cash?", result.Message);
    }

    [Fact]
    public void TradeOfferPacket_EmptyMessage_RoundTrip()
    {
        var original = new TradeOfferPacket { TradeId = 1, Message = string.Empty };
        var result = RoundTrip(original);
        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public void TradeResponsePacket_RoundTrip()
    {
        var original = new TradeResponsePacket
        {
            TradeId = 999,
            ResponseType = 1, // Accept
            FromPlayer = 2,
            CounterMoney = 0,
            CounterResourceAmount = 0,
        };

        var result = RoundTrip(original);

        Assert.Equal(999u, result.TradeId);
        Assert.Equal(1, result.ResponseType);
        Assert.Equal(2, result.FromPlayer);
    }

    [Fact]
    public void TradeResponsePacket_CounterOffer_RoundTrip()
    {
        var original = new TradeResponsePacket
        {
            TradeId = 42,
            ResponseType = 2, // Counter
            FromPlayer = 3,
            CounterMoney = 75000,
            CounterResourceAmount = 50,
        };

        var result = RoundTrip(original);

        Assert.Equal(42u, result.TradeId);
        Assert.Equal(2, result.ResponseType);
        Assert.Equal(75000L, result.CounterMoney);
        Assert.Equal(50, result.CounterResourceAmount);
    }

    [Fact]
    public void ChatMessagePacket_RoundTrip()
    {
        var original = new ChatMessagePacket
        {
            PlayerId = 4,
            PlayerName = "TestPlayer",
            Message = "Hello, world! \ud83c\udfd9\ufe0f",
            TimestampUnixMs = 1700000000000L,
        };

        var result = RoundTrip(original);

        Assert.Equal(4, result.PlayerId);
        Assert.Equal("TestPlayer", result.PlayerName);
        Assert.Equal("Hello, world! \ud83c\udfd9\ufe0f", result.Message);
        Assert.Equal(1700000000000L, result.TimestampUnixMs);
    }

    [Fact]
    public void ChatMessagePacket_EmptyMessage_RoundTrip()
    {
        var original = new ChatMessagePacket
        {
            PlayerId = 0,
            PlayerName = "",
            Message = "",
            TimestampUnixMs = 0,
        };

        var result = RoundTrip(original);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(string.Empty, result.PlayerName);
    }

    [Fact]
    public void GameSpeedPacket_RoundTrip()
    {
        var original = new GameSpeedPacket
        {
            Speed = 2,
            CurrentTick = 54321,
        };

        var result = RoundTrip(original);

        Assert.Equal(2, result.Speed);
        Assert.Equal(54321u, result.CurrentTick);
    }

    [Fact]
    public void SyncCheckPacket_RoundTrip()
    {
        var original = new SyncCheckPacket
        {
            GameTick = 1000,
            Crc32Population = 0xDEADBEEF,
            Crc32Budget = 0xCAFEBABE,
            Crc32Roads = 0x12345678,
            Crc32Buildings = 0xABCD1234,
        };

        var result = RoundTrip(original);

        Assert.Equal(1000u, result.GameTick);
        Assert.Equal(0xDEADBEEFu, result.Crc32Population);
        Assert.Equal(0xCAFEBABEu, result.Crc32Budget);
        Assert.Equal(0x12345678u, result.Crc32Roads);
        Assert.Equal(0xABCD1234u, result.Crc32Buildings);
    }

    [Fact]
    public void SyncCheckPacket_ComputeCrc32_DeterministicAndNonZero()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        uint crc1 = SyncCheckPacket.ComputeCrc32(data);
        uint crc2 = SyncCheckPacket.ComputeCrc32(data);

        Assert.Equal(crc1, crc2); // Deterministic.
        Assert.NotEqual(0u, crc1); // Non-trivial.
    }

    [Fact]
    public void SyncCheckPacket_ComputeCrc32_DifferentDataDifferentHash()
    {
        byte[] data1 = { 0x01, 0x02, 0x03 };
        byte[] data2 = { 0x01, 0x02, 0x04 };

        uint crc1 = SyncCheckPacket.ComputeCrc32(data1);
        uint crc2 = SyncCheckPacket.ComputeCrc32(data2);

        Assert.NotEqual(crc1, crc2);
    }

    [Fact]
    public void HeartbeatPacket_RoundTrip()
    {
        var original = new HeartbeatPacket
        {
            SentTimestampMs = 1700000000000L,
            EchoTimestampMs = 1700000000050L,
            PlayerId = 3,
        };

        var result = RoundTrip(original);

        Assert.Equal(1700000000000L, result.SentTimestampMs);
        Assert.Equal(1700000000050L, result.EchoTimestampMs);
        Assert.Equal(3, result.PlayerId);
    }

    [Fact]
    public void HeartbeatPacket_InitialPing_ZeroEcho()
    {
        var original = new HeartbeatPacket
        {
            SentTimestampMs = 1700000000000L,
            EchoTimestampMs = 0,
            PlayerId = 1,
        };

        var result = RoundTrip(original);
        Assert.Equal(0L, result.EchoTimestampMs);
    }

    // =========================================================================
    // PacketRegistry edge cases
    // =========================================================================

    [Fact]
    public void PacketRegistry_UnknownPacketId_ReturnsNull()
    {
        byte[] data = { 255 }; // Unknown ID
        var result = PacketRegistry.Deserialize(data);
        Assert.Null(result);
    }

    [Fact]
    public void PacketRegistry_EmptyData_ReturnsNull()
    {
        var result = PacketRegistry.Deserialize(Array.Empty<byte>());
        Assert.Null(result);
    }

    [Fact]
    public void PacketRegistry_SerializedDataStartsWithPacketId()
    {
        var packet = new HeartbeatPacket { SentTimestampMs = 42 };
        byte[] data = PacketRegistry.Serialize(packet);

        Assert.True(data.Length > 0);
        Assert.Equal(packet.PacketId, data[0]);
    }

    // =========================================================================
    // NetworkStats tests
    // =========================================================================

    [Fact]
    public void NetworkStats_DefaultValues_AreZero()
    {
        var stats = new NetworkStats();

        Assert.Equal(0, stats.PingMs);
        Assert.Equal(0L, stats.BytesReceivedPerSec);
        Assert.Equal(0L, stats.BytesSentPerSec);
        Assert.Equal(0f, stats.PacketLossRatio);
        Assert.Equal(0L, stats.TotalPacketsReceived);
        Assert.Equal(0L, stats.TotalPacketsSent);
    }

    // =========================================================================
    // All packet types have unique IDs
    // =========================================================================

    [Fact]
    public void AllPacketTypes_HaveUniqueIds()
    {
        var packets = new IPacket[]
        {
            new CitySnapshotPacket(),
            new PlayerCommandPacket(),
            new TradeOfferPacket(),
            new TradeResponsePacket(),
            new ChatMessagePacket(),
            new GameSpeedPacket(),
            new SyncCheckPacket(),
            new HeartbeatPacket(),
        };

        var ids = new HashSet<byte>();
        foreach (var p in packets)
        {
            Assert.True(ids.Add(p.PacketId), $"Duplicate packet ID: {p.PacketId} on {p.GetType().Name}");
        }
    }
}
