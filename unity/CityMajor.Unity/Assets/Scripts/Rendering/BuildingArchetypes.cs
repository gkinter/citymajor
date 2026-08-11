using CityMajor.Input;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Building archetype taxonomy — ports classifyBuilding / archetypeKey from
    /// web/packages/sim-types/src/buildingArchetypes.ts (modern-era Unity v1 scope).
    /// </summary>
    public static class BuildingArchetypes
    {
        public enum BuildingCategory
        {
            ResidentialLow,
            ResidentialHigh,
            Commercial,
            Industrial,
            Service,
        }

        public const int ResLowStart = 160;
        public const int ResLowEnd = 179;
        public const int ResHighStart = 260;
        public const int ResHighEnd = 279;
        public const int ComStart = 360;
        public const int ComEnd = 379;
        public const int IndStart = 460;
        public const int IndEnd = 479;
        public const int SvcStart = 500;
        public const int SvcEnd = 599;

        public static BuildingCategory ClassifyBuilding(int typeId)
        {
            if (typeId >= ResLowStart && typeId <= ResLowEnd)
                return BuildingCategory.ResidentialLow;
            if (typeId >= ResHighStart && typeId <= ResHighEnd)
                return BuildingCategory.ResidentialHigh;
            if (typeId >= ComStart && typeId <= ComEnd)
                return BuildingCategory.Commercial;
            if (typeId >= IndStart && typeId <= IndEnd)
                return BuildingCategory.Industrial;
            return BuildingCategory.Service;
        }

        static int CategoryBase(BuildingCategory category) => category switch
        {
            BuildingCategory.ResidentialLow => ResLowStart,
            BuildingCategory.ResidentialHigh => ResHighStart,
            BuildingCategory.Commercial => ComStart,
            BuildingCategory.Industrial => IndStart,
            _ => 0,
        };

        /// <summary>Modern Unity v1 — zone buildings use era index 3; service always modern.</summary>
        public static int DeriveVariant(int typeId)
        {
            var category = ClassifyBuilding(typeId);
            if (category == BuildingCategory.Service)
                return typeId % 20;
            var offset = typeId - CategoryBase(category);
            return offset % 20;
        }

        /// <summary>Mesh archetype key: <c>{category}_modern_{variant}</c>.</summary>
        public static string ArchetypeKey(int typeId, int? variant = null)
        {
            var category = ClassifyBuilding(typeId);
            var v = variant ?? DeriveVariant(typeId);
            var prefix = CategoryPrefix(category);
            return $"{prefix}_{GltfCatalog.ModernEra}_{v:D2}";
        }

        static string CategoryPrefix(BuildingCategory category) => category switch
        {
            BuildingCategory.ResidentialLow => "res_low",
            BuildingCategory.ResidentialHigh => "res_high",
            BuildingCategory.Commercial => "com",
            BuildingCategory.Industrial => "ind",
            BuildingCategory.Service => "svc",
            _ => "svc",
        };

        /// <summary>
        /// Resolve a catalog key for a sim TypeId (archetypeKey → resolveCatalogKey).
        /// </summary>
        public static string CatalogKeyForTypeId(int typeId) =>
            GltfCatalog.ResolveCatalogKey(ArchetypeKey(typeId));

        /// <summary>
        /// Minimal zone-kind → default shipped catalog key when TypeId is unavailable.
        /// </summary>
        public static string DefaultCatalogKeyForZone(ZonePaintTool.ZoneKind zone) => zone switch
        {
            ZonePaintTool.ZoneKind.Residential => "res_low_modern_00",
            ZonePaintTool.ZoneKind.Commercial => "com_modern_00",
            ZonePaintTool.ZoneKind.Industrial => "ind_modern_00",
            ZonePaintTool.ZoneKind.Office => "com_modern_00",
            ZonePaintTool.ZoneKind.Mixed => "res_low_modern_00",
            ZonePaintTool.ZoneKind.Agricultural => "ind_modern_00",
            ZonePaintTool.ZoneKind.Park => "svc_modern_00",
            _ => null,
        };

        /// <summary>Map painted zone to a representative TypeId band for archetype derivation.</summary>
        public static int DefaultTypeIdForZone(ZonePaintTool.ZoneKind zone) => zone switch
        {
            ZonePaintTool.ZoneKind.Residential => ResLowStart,
            ZonePaintTool.ZoneKind.Commercial => ComStart,
            ZonePaintTool.ZoneKind.Industrial => IndStart,
            ZonePaintTool.ZoneKind.Office => ComStart,
            ZonePaintTool.ZoneKind.Mixed => ResLowStart,
            ZonePaintTool.ZoneKind.Agricultural => IndStart,
            ZonePaintTool.ZoneKind.Park => SvcStart,
            _ => 0,
        };
    }
}
