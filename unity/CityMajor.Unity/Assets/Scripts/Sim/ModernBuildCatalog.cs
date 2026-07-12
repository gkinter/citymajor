namespace CityMajor.Sim
{
    /// <summary>Modern-era service ploppables for Unity v1 build toolbar (from unity-modern-unlocks.json).</summary>
    public static class ModernBuildCatalog
    {
        public readonly struct Entry
        {
            public string Category { get; init; }
            public string Label { get; init; }
            public int TypeId { get; init; }
            public string CatalogKey { get; init; }
        }

        public static readonly Entry[] ServiceEntries =
        {
            new() { Category = "Fire", Label = "Fire Station", TypeId = 562, CatalogKey = "svc_modern_02" },
            new() { Category = "Police", Label = "Police HQ", TypeId = 564, CatalogKey = "svc_modern_04" },
            new() { Category = "Health", Label = "Hospital", TypeId = 563, CatalogKey = "svc_modern_03" },
            new() { Category = "Education", Label = "Community College", TypeId = 560, CatalogKey = "svc_modern_00" },
            new() { Category = "Education", Label = "University", TypeId = 567, CatalogKey = "svc_modern_07" },
            new() { Category = "Civic", Label = "Public Library", TypeId = 565, CatalogKey = "svc_modern_05" },
            new() { Category = "Civic", Label = "Daycare", TypeId = 561, CatalogKey = "svc_modern_01" },
            new() { Category = "Civic", Label = "Senior Center", TypeId = 566, CatalogKey = "svc_modern_06" },
        };
    }
}
