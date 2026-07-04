/**
 * GLTF asset catalog — maps building archetype keys to public asset URLs.
 *
 * Placeholder meshes live under `public/assets/gltf/{era}/{key}.glb` until the
 * Meshy art pipeline delivers final assets. See docs/MESHY_ASSET_PIPELINE.md.
 */

const ERA_SLUGS = [
  "frontier",
  "industrial",
  "postwar",
  "modern",
  "future",
] as const;

export type GltfEraSlug = (typeof ERA_SLUGS)[number];

/** Spike placeholders — one per category × representative era. */
export const PLACEHOLDER_ARCHETYPE_KEYS = [
  "res_low_frontier_00",
  "com_frontier_00",
  "ind_industrial_00",
  "res_high_industrial_00",
  "svc_modern_00",
] as const;

export type PlaceholderArchetypeKey = (typeof PLACEHOLDER_ARCHETYPE_KEYS)[number];

/** Extract era folder slug from an archetype key (`res_low_frontier_05` → `frontier`). */
export function eraSlugFromArchetypeKey(key: string): GltfEraSlug | null {
  for (const era of ERA_SLUGS) {
    if (key.includes(`_${era}_`)) return era;
  }
  return null;
}

/** Public URL path for a GLTF asset, or null if the key has no known era segment. */
export function gltfPublicPath(key: string): string | null {
  const era = eraSlugFromArchetypeKey(key);
  if (!era) return null;
  return `/assets/gltf/${era}/${key}.glb`;
}

/**
 * Catalog of placeholder GLB modules shipped with the web client.
 * Keys follow sim-types `archetypeKey()` convention (BUILDING_ARCHETYPE_3D ADR).
 */
export const GLTF_CATALOG: Record<PlaceholderArchetypeKey, string> = {
  res_low_frontier_00: "/assets/gltf/frontier/res_low_frontier_00.glb",
  com_frontier_00: "/assets/gltf/frontier/com_frontier_00.glb",
  ind_industrial_00: "/assets/gltf/industrial/ind_industrial_00.glb",
  res_high_industrial_00: "/assets/gltf/industrial/res_high_industrial_00.glb",
  svc_modern_00: "/assets/gltf/modern/svc_modern_00.glb",
};

/** Resolve catalog path, falling back to era-derived path for unknown variants. */
export function resolveGltfPath(key: string): string | null {
  if (key in GLTF_CATALOG) {
    return GLTF_CATALOG[key as PlaceholderArchetypeKey];
  }
  return gltfPublicPath(key);
}

/** True when a shipped GLB exists for this archetype (catalog or era path). */
export function hasGltfAsset(key: string): boolean {
  return key in GLTF_CATALOG;
}

/** All catalog GLB URLs for play-page preload. */
export function allGltfPaths(): string[] {
  return Object.values(GLTF_CATALOG);
}
