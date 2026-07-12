using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

/// <summary>Stepped service coverage samples for Unity GL overlays (not full tile grid).</summary>
public static class ServiceCoverageExport
{
    public static ServiceCoverageDto[] Sample(WorldState state, ServiceSystem? services, int step = 8)
    {
        if (services is null || step < 1)
            return [];

        var tiles = state.Tiles;
        var health = services.HealthCoverage;
        var police = services.PoliceCoverage;
        var fire = services.FireCoverage;
        var education = services.EducationCoverage;
        var list = new List<ServiceCoverageDto>(256);

        for (var y = 0; y < tiles.Size; y += step)
        for (var x = 0; x < tiles.Size; x += step)
        {
            var idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0)
                continue;

            var h = Math.Clamp(health.GetValue(x, y), 0f, 1f);
            var p = Math.Clamp(police.GetValue(x, y), 0f, 1f);
            var f = Math.Clamp(fire.GetValue(x, y), 0f, 1f);
            var e = Math.Clamp(education.GetValue(x, y), 0f, 1f);
            if (h < 0.05f && p < 0.05f && f < 0.05f && e < 0.05f)
                continue;

            list.Add(new ServiceCoverageDto
            {
                TileX = x,
                TileZ = y,
                Health = h,
                Police = p,
                Fire = f,
                Education = e,
            });
        }

        return list.ToArray();
    }
}
