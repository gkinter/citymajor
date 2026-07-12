using System;
using System.Collections.Generic;
using System.Globalization;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Modern-era GLTF catalog — ports <c>resolveCatalogKey</c> from web/lib/gltf-catalog.ts
    /// scoped to the 12 keys in base/data/tech/unity-modern-unlocks.json shippedGltfCatalog.
    /// </summary>
    public static class GltfCatalog
    {
        public const string ModernEra = "modern";
        public const string AssetRoot = "Assets/Art/Gltf/modern";

        /// <summary>Keys shipped with the Unity v1 modern manifest.</summary>
        public static readonly IReadOnlyList<string> ShippedKeys = new[]
        {
            "res_low_modern_00",
            "res_high_modern_00",
            "com_modern_00",
            "ind_modern_00",
            "svc_modern_00",
            "svc_modern_01",
            "svc_modern_02",
            "svc_modern_03",
            "svc_modern_04",
            "svc_modern_05",
            "svc_modern_06",
            "svc_modern_07",
        };

        static readonly Dictionary<string, string> Catalog = BuildCatalog();

        static Dictionary<string, string> BuildCatalog()
        {
            var map = new Dictionary<string, string>(ShippedKeys.Count);
            foreach (var key in ShippedKeys)
                map[key] = $"{AssetRoot}/{key}.glb";
            return map;
        }

        public static bool TryGetAssetPath(string catalogKey, out string assetPath) =>
            Catalog.TryGetValue(catalogKey, out assetPath);

        public static string AssetPath(string catalogKey) =>
            Catalog.TryGetValue(catalogKey, out var path) ? path : null;

        public static bool HasCatalogEntry(string key) => Catalog.ContainsKey(key);

        /// <summary>Parse <c>{category}_{era}_{variant}</c> keys from sim-types archetypeKey().</summary>
        public static bool TryParseArchetypeKey(string key, out string category, out string era, out int variant)
        {
            category = era = null;
            variant = 0;

            var marker = $"_{ModernEra}_";
            var idx = key.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
                return false;

            category = key.Substring(0, idx);
            if (!int.TryParse(key.Substring(idx + marker.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out variant))
                return false;

            era = ModernEra;
            return true;
        }

        static List<string> ShippedKeysForCategoryEra(string category, string era)
        {
            var prefix = $"{category}_{era}_";
            var list = new List<string>();
            foreach (var key in ShippedKeys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    list.Add(key);
            }
            return list;
        }

        /// <summary>
        /// Map any sim archetype key to a shipped catalog key (same category × era).
        /// Variants without a dedicated GLB reuse era representatives for visual variety.
        /// </summary>
        public static string ResolveCatalogKey(string archetypeKey)
        {
            if (string.IsNullOrEmpty(archetypeKey))
                return null;

            if (HasCatalogEntry(archetypeKey))
                return archetypeKey;

            if (!TryParseArchetypeKey(archetypeKey, out var category, out _, out var variant))
                return null;

            var exact = $"{category}_{ModernEra}_{variant:D2}";
            if (HasCatalogEntry(exact))
                return exact;

            var candidates = ShippedKeysForCategoryEra(category, ModernEra);
            if (candidates.Count > 0)
                return candidates[variant % candidates.Count];

            if (category == "svc" && HasCatalogEntry("svc_modern_00"))
                return "svc_modern_00";

            return null;
        }
    }
}
