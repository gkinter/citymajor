using CityMajor.Input;
using CityMajor.UI;

namespace CityMajor.Sim
{
    /// <summary>
    /// Paintable zone tier metadata — mirrors <c>web/lib/zone-tiers.ts</c> (P2.1 era gates).
    /// </summary>
    public static class ZoneTiers
    {
        /// <summary>Max baseline R/C/I engine byte (procedural / no extended palette).</summary>
        public const byte BaselineMaxZoneType = 4;

        /// <summary>technologies.json index for T086 Basic Zoning Laws.</summary>
        public const int MixedTechId = 85;

        /// <summary>technologies.json index for T134 Stock Exchange.</summary>
        public const int OfficeTechId = 135;

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
                or ZonePaintTool.ZoneKind.Agricultural;

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
}
