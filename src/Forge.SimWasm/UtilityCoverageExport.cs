using Forge.Engine.Simulation;

namespace Forge.SimWasm;

/// <summary>Stepped power/water coverage samples for Unity GL overlays (not full tile grid).</summary>
public static class UtilityCoverageExport
{
    public static UtilityCoverageDto[] Sample(WorldState state, int step = 8)
    {
        if (step < 1)
            return [];

        var tiles = state.Tiles;
        var power = tiles.PowerGrid;
        var water = tiles.WaterGrid;
        var list = new List<UtilityCoverageDto>(256);

        for (var y = 0; y < tiles.Size; y += step)
        for (var x = 0; x < tiles.Size; x += step)
        {
            var idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0)
                continue;

            var p = power[idx] != 0 ? 1f : 0f;
            var w = water[idx] != 0 ? 1f : 0f;

            list.Add(new UtilityCoverageDto
            {
                TileX = x,
                TileZ = y,
                Power = p,
                Water = w,
            });
        }

        return list.ToArray();
    }
}
