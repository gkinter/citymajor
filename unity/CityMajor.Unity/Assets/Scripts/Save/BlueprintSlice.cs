namespace CityMajor.Save
{
    /// <summary>
    /// CMJR district blueprint slice — chunk type 0x02 (v2.5 marketplace scaffold).
    /// Binary layout TBD; types only for editor tooling hooks.
    /// </summary>
    public static class BlueprintChunkIds
    {
        public const byte DistrictSlice = 0x02;
    }

    public sealed class BlueprintSliceHeader
    {
        public byte ChunkId { get; init; } = BlueprintChunkIds.DistrictSlice;
        public int OriginTileX { get; init; }
        public int OriginTileZ { get; init; }
        public int WidthTiles { get; init; }
        public int HeightTiles { get; init; }
        public string AuthorSteamId { get; init; } = "";
        public string DisplayName { get; init; } = "";
    }

    /// <summary>Placeholder export — returns header bytes only until CMJR chunk writer lands.</summary>
    public static class BlueprintSliceWriter
    {
        public static byte[] WriteHeaderOnly(BlueprintSliceHeader header)
        {
            // 32-byte stub: [chunk][x][z][w][h] little-endian ints + padding
            var buf = new byte[32];
            buf[0] = header.ChunkId;
            WriteInt(buf, 1, header.OriginTileX);
            WriteInt(buf, 5, header.OriginTileZ);
            WriteInt(buf, 9, header.WidthTiles);
            WriteInt(buf, 13, header.HeightTiles);
            return buf;
        }

        static void WriteInt(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16);
            buf[offset + 3] = (byte)(value >> 24);
        }
    }
}
