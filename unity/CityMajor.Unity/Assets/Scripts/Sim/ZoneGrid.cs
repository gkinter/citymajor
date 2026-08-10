using CityMajor.Input;
using UnityEngine;

namespace CityMajor.Sim
{
    /// <summary>
    /// Tile zone storage for the 256×256 modern-era map.
    /// </summary>
    public sealed class ZoneGrid : MonoBehaviour
    {
        public int MapSize { get; private set; }
        public float TileSize { get; private set; }

        ZonePaintTool.ZoneKind[] _zones;

        public void Initialize(int mapSize, float tileSize)
        {
            MapSize = mapSize;
            TileSize = tileSize;
            _zones = new ZonePaintTool.ZoneKind[mapSize * mapSize];
        }

        public bool InBounds(int x, int y) =>
            x >= 0 && y >= 0 && x < MapSize && y < MapSize;

        public ZonePaintTool.ZoneKind GetZone(int x, int y) =>
            InBounds(x, y) ? _zones[y * MapSize + x] : ZonePaintTool.ZoneKind.None;

        public void SetZone(int x, int y, ZonePaintTool.ZoneKind kind)
        {
            if (!InBounds(x, y))
                return;
            _zones[y * MapSize + x] = kind;
        }

        /// <summary>Apply engine zone bytes from Forge.SimCore snapshot (sim is source of truth).</summary>
        public void SyncFromEngineZones(byte[] engineZones)
        {
            if (engineZones == null || _zones == null)
                return;

            var n = Mathf.Min(engineZones.Length, _zones.Length);
            for (var i = 0; i < n; i++)
                _zones[i] = CitySimBridge.EngineZoneToKind(engineZones[i]);
        }

        public Vector2Int WorldToTile(Vector3 world)
        {
            var x = Mathf.FloorToInt(world.x / TileSize);
            var y = Mathf.FloorToInt(world.z / TileSize);
            return new Vector2Int(x, y);
        }

        public Vector3 TileCenter(int x, int y) =>
            new Vector3((x + 0.5f) * TileSize, 0.05f, (y + 0.5f) * TileSize);

        public Color ZoneColor(ZonePaintTool.ZoneKind kind) => kind switch
        {
            ZonePaintTool.ZoneKind.Residential => new Color(0.37f, 0.70f, 0.96f, 0.45f),
            ZonePaintTool.ZoneKind.Commercial => new Color(0.94f, 0.70f, 0.16f, 0.45f),
            ZonePaintTool.ZoneKind.Industrial => new Color(0.72f, 0.45f, 0.20f, 0.45f),
            ZonePaintTool.ZoneKind.Office => new Color(0.75f, 0.52f, 0.99f, 0.45f),
            ZonePaintTool.ZoneKind.Mixed => new Color(0.18f, 0.83f, 0.66f, 0.45f),
            ZonePaintTool.ZoneKind.Agricultural => new Color(0.52f, 0.80f, 0.09f, 0.45f),
            _ => Color.clear,
        };
    }
}
