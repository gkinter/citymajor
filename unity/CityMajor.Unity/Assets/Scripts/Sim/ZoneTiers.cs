using CityMajor.Input;
using CityMajor.UI;

namespace CityMajor.Sim
{
    /// <summary>
    /// Paintable zone tier metadata — mirrors <c>web/lib/zone-tiers.ts</c> (P2.1 era gates).
    /// Park (engine byte 8) is a no-growth recreation zone; density levels use
    /// <see cref="ZoneDensityTiers"/> (Frontier→Industrial).
    /// </summary>
    public static class ZoneTiers
    {
        /// <summary>Max baseline R/C/I engine byte (procedural / no extended palette).</summary>
        public const byte BaselineMaxZoneType = 4;

        /// <summary>technologies.json index for T086 Basic Zoning Laws.</summary>
        public const int MixedTechId = 85;

        /// <summary>technologies.json index for T134 Stock Exchange.</summary>
        public const int OfficeTechId = 135;

        /// <summary>technologies.json index for T112 Public Parks Act.</summary>
        public const int ParkTechId = 113;

        public readonly struct Tier
        {
            public readonly ZonePaintTool.ZoneKind Kind;
            public readonly string Label;
            public readonly string ShortLabel;
            public readonly byte EngineZoneType;
            public readonly int MinEra;
            /// <summary>-1 = no research gate.</summary>
            public readonly int RequiredTechId;

            public Tier(
                ZonePaintTool.ZoneKind kind,
                string label,
                string shortLabel,
                byte engineZoneType,
                int minEra,
                int requiredTechId = -1)
            {
                Kind = kind;
                Label = label;
                ShortLabel = shortLabel;
                EngineZoneType = engineZoneType;
                MinEra = minEra;
                RequiredTechId = requiredTechId;
            }
        }

        public static readonly Tier[] Paintable =
        {
            new(ZonePaintTool.ZoneKind.Residential, "Residential", "R", 1, 0),
            new(ZonePaintTool.ZoneKind.Commercial, "Commercial", "C", 3, 0),
            new(ZonePaintTool.ZoneKind.Industrial, "Industrial", "I", 4, 0),
            new(ZonePaintTool.ZoneKind.Office, "Office", "Office", 5, 1, OfficeTechId),
            new(ZonePaintTool.ZoneKind.Mixed, "Mixed Use", "Mixed", 6, 0, MixedTechId),
            new(ZonePaintTool.ZoneKind.Agricultural, "Agricultural", "Ag", 7, 0),
            new(ZonePaintTool.ZoneKind.Park, "Park", "Park", 8, 0, ParkTechId),
        };

        public static bool TryGet(ZonePaintTool.ZoneKind kind, out Tier tier)
        {
            foreach (var t in Paintable)
            {
                if (t.Kind != kind)
                    continue;
                tier = t;
                return true;
            }

            tier = default;
            return false;
        }

        public static bool IsExtended(ZonePaintTool.ZoneKind kind) =>
            kind is ZonePaintTool.ZoneKind.Office
                or ZonePaintTool.ZoneKind.Mixed
                or ZonePaintTool.ZoneKind.Agricultural
                or ZonePaintTool.ZoneKind.Park;

        public static bool IsVisible(Tier tier, bool supportsExtendedBytes) =>
            tier.EngineZoneType <= BaselineMaxZoneType || supportsExtendedBytes;

        public static bool IsEraUnlocked(Tier tier, int currentEra) =>
            currentEra >= tier.MinEra;

        public static bool IsUnlocked(Tier tier, CitySimBridge sim)
        {
            if (sim == null)
                return !IsExtended(tier.Kind);

            var supportsExtended = sim.UsesForgeSimCore;
            if (!IsVisible(tier, supportsExtended))
                return false;

            var era = ResolveEra(sim);
            if (!IsEraUnlocked(tier, era))
                return false;

            if (tier.RequiredTechId < 0)
                return true;

            // Without SimCore, research gates cannot be satisfied for extended tiers.
            if (!sim.UsesForgeSimCore)
                return false;

            return sim.IsTechUnlocked(tier.RequiredTechId);
        }

        public static int ResolveEra(CitySimBridge sim)
        {
            if (sim != null && sim.UsesForgeSimCore)
                return sim.LatestSnapshot.Era;
            return EraNames.DefaultEra;
        }

        public static string LockReason(Tier tier, CitySimBridge sim)
        {
            if (sim == null || !sim.UsesForgeSimCore)
                return "Requires Forge sim (extended zone types)";

            var era = ResolveEra(sim);
            if (!IsEraUnlocked(tier, era))
                return $"Locked — reach {EraNames.HudEraName(tier.MinEra)} era";

            if (tier.RequiredTechId >= 0 && !sim.IsTechUnlocked(tier.RequiredTechId))
            {
                var name = sim.TechName(tier.RequiredTechId);
                return $"Locked — research {name}";
            }

            return "Locked";
        }
    }

    /// <summary>
    /// Density brush gates (P2.2 / U3.4) — Frontier→Industrial arc.
    /// Low always; Med needs Brick and Masonry (T029); High needs Industrial + Reinforced Concrete (T031).
    /// </summary>
    public static class ZoneDensityTiers
    {
        /// <summary>technologies.json index for T029 Brick and Masonry.</summary>
        public const int MediumTechId = 28;

        /// <summary>technologies.json index for T031 Reinforced Concrete.</summary>
        public const int HighTechId = 30;

        public readonly struct Level
        {
            public readonly byte Density;
            public readonly string Label;
            public readonly int MinEra;
            /// <summary>-1 = no research gate.</summary>
            public readonly int RequiredTechId;

            public Level(byte density, string label, int minEra, int requiredTechId = -1)
            {
                Density = density;
                Label = label;
                MinEra = minEra;
                RequiredTechId = requiredTechId;
            }
        }

        public static readonly Level[] Levels =
        {
            new(1, "Low", 0),
            new(2, "Med", 0, MediumTechId),
            new(3, "High", 1, HighTechId),
        };

        public static bool TryGet(byte density, out Level level)
        {
            var normalized = ZonePaintTool.NormalizeDensity(density);
            foreach (var l in Levels)
            {
                if (l.Density != normalized)
                    continue;
                level = l;
                return true;
            }

            level = default;
            return false;
        }

        public static bool IsUnlocked(byte density, CitySimBridge sim)
        {
            if (!TryGet(density, out var level))
                return false;

            // Procedural / no sim: only Low is safe.
            if (sim == null || !sim.UsesForgeSimCore)
                return level.Density == 1;

            var era = ZoneTiers.ResolveEra(sim);
            if (era < level.MinEra)
                return false;

            if (level.RequiredTechId < 0)
                return true;

            return sim.IsTechUnlocked(level.RequiredTechId);
        }

        public static string LockReason(byte density, CitySimBridge sim)
        {
            if (!TryGet(density, out var level))
                return "Unknown density";

            if (sim == null || !sim.UsesForgeSimCore)
                return level.Density == 1
                    ? ""
                    : "Requires Forge sim (density research gates)";

            var era = ZoneTiers.ResolveEra(sim);
            if (era < level.MinEra)
                return $"Locked — reach {EraNames.HudEraName(level.MinEra)} era";

            if (level.RequiredTechId >= 0 && !sim.IsTechUnlocked(level.RequiredTechId))
            {
                var name = sim.TechName(level.RequiredTechId);
                return $"Locked — research {name}";
            }

            return "Locked";
        }

        /// <summary>Highest unlocked density ≤ preferred, else lowest unlocked (Low).</summary>
        public static byte ClampToUnlocked(byte preferred, CitySimBridge sim)
        {
            var want = ZonePaintTool.NormalizeDensity(preferred);
            if (IsUnlocked(want, sim))
                return want;

            for (var d = want; d >= 1; d--)
            {
                if (IsUnlocked(d, sim))
                    return d;
            }

            return 1;
        }

        /// <summary>Next unlocked density after current (cycles Low→Med→High among unlocked).</summary>
        public static byte NextUnlocked(byte current, CitySimBridge sim)
        {
            var start = ZonePaintTool.NormalizeDensity(current);
            for (var step = 1; step <= 3; step++)
            {
                var next = (byte)((start - 1 + step) % 3 + 1);
                if (IsUnlocked(next, sim))
                    return next;
            }

            return 1;
        }
    }
}
