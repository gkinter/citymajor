/**
 * GLTF asset catalog — maps building archetype keys to public asset URLs.
 *
 * Placeholder meshes live under `public/assets/gltf/{era}/{key}.glb` until the
 * Meshy art pipeline delivers final assets. See docs/MESHY_ASSET_PIPELINE.md
 * and docs/design/DATA_BRIDGE.md.
 */

const ERA_SLUGS = [
  "frontier",
  "industrial",
  "postwar",
  "modern",
  "future",
] as const;

export type GltfEraSlug = (typeof ERA_SLUGS)[number];

/**
 * Archetype keys with shipped or documented GLB paths — aligned to
 * `scripts/meshy/manifest.json` spike batch (one representative per category × era).
 */
export const SHIPPED_GLTF_KEYS = [
  "res_low_frontier_00",
  "res_low_frontier_01",
  "res_low_frontier_02",
  "res_low_frontier_03",
  "res_low_frontier_04",
  "res_low_frontier_05",
  "res_low_frontier_06",
  "res_low_frontier_07",
  "res_low_frontier_08",
  "res_low_frontier_09",
  "res_low_frontier_10",
  "res_low_frontier_11",
  "res_low_frontier_12",
  "res_low_frontier_13",
  "res_low_frontier_14",
  "res_low_frontier_15",
  "res_low_frontier_16",
  "res_low_frontier_17",
  "res_low_frontier_18",
  "res_low_frontier_19",
  "res_low_frontier_20",
  "res_low_frontier_21",
  "res_low_frontier_22",
  "com_frontier_00",
  "ind_frontier_00",
  "res_high_frontier_01",
  "res_low_industrial_00",
  "res_low_industrial_01",
  "res_low_industrial_02",
  "res_high_industrial_00",
  "res_high_industrial_01",
  "res_high_industrial_03",
  "com_industrial_00",
  "com_industrial_01",
  "ind_industrial_00",
  "ind_industrial_01",
  "res_low_postwar_00",
  "res_high_postwar_00",
  "com_postwar_00",
  "ind_postwar_00",
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
  "res_low_future_00",
  "com_future_00",
  "ind_future_00",
] as const;

export type ShippedGltfKey = (typeof SHIPPED_GLTF_KEYS)[number];

/**
 * Hero landmark GLBs under `public/assets/gltf/heroes/` — not in `manifest.json`
 * or `SHIPPED_GLTF_KEYS` (see `scripts/meshy/validate-manifest.mjs` heroes skip).
 * Spec: docs/design/MESHY_HERO_LANDMARKS.md
 */
export const OPTIONAL_HERO_LANDMARKS = {
  hero_frontier_city_hall: "/assets/gltf/heroes/hero_frontier_city_hall.glb",
  hero_frontier_church: "/assets/gltf/heroes/hero_frontier_church.glb",
  hero_industrial_steel_mill: "/assets/gltf/heroes/hero_industrial_steel_mill.glb",
  hero_industrial_train_station: "/assets/gltf/heroes/hero_industrial_train_station.glb",
  hero_postwar_civic_hall: "/assets/gltf/heroes/hero_postwar_civic_hall.glb",
  hero_postwar_hospital: "/assets/gltf/heroes/hero_postwar_hospital.glb",
  hero_modern_glass_tower: "/assets/gltf/heroes/hero_modern_glass_tower.glb",
  hero_future_eco_tower: "/assets/gltf/heroes/hero_future_eco_tower.glb",
} as const;

export type OptionalHeroLandmarkKey = keyof typeof OPTIONAL_HERO_LANDMARKS;

export function heroGltfPublicPath(filename: string): string {
  return `/assets/gltf/heroes/${filename}`;
}


/** @deprecated Use SHIPPED_GLTF_KEYS */
export const PLACEHOLDER_ARCHETYPE_KEYS = SHIPPED_GLTF_KEYS;

/** @deprecated Use ShippedGltfKey */
export type PlaceholderArchetypeKey = ShippedGltfKey;

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
 * Catalog of GLB modules shipped or documented with the web client.
 * Keys follow sim-types `archetypeKey()` convention (BUILDING_ARCHETYPE_3D ADR).
 */
export const GLTF_CATALOG: Record<ShippedGltfKey, string> = {
  res_low_frontier_00: "/assets/gltf/frontier/res_low_frontier_00.glb",
  res_low_frontier_01: "/assets/gltf/frontier/res_low_frontier_01.glb",
  res_low_frontier_02: "/assets/gltf/frontier/res_low_frontier_02.glb",
  res_low_frontier_03: "/assets/gltf/frontier/res_low_frontier_03.glb",
  res_low_frontier_04: "/assets/gltf/frontier/res_low_frontier_04.glb",
  res_low_frontier_05: "/assets/gltf/frontier/res_low_frontier_05.glb",
  res_low_frontier_06: "/assets/gltf/frontier/res_low_frontier_06.glb",
  res_low_frontier_07: "/assets/gltf/frontier/res_low_frontier_07.glb",
  res_low_frontier_08: "/assets/gltf/frontier/res_low_frontier_08.glb",
  res_low_frontier_09: "/assets/gltf/frontier/res_low_frontier_09.glb",
  res_low_frontier_10: "/assets/gltf/frontier/res_low_frontier_10.glb",
  res_low_frontier_11: "/assets/gltf/frontier/res_low_frontier_11.glb",
  res_low_frontier_12: "/assets/gltf/frontier/res_low_frontier_12.glb",
  res_low_frontier_13: "/assets/gltf/frontier/res_low_frontier_13.glb",
  res_low_frontier_14: "/assets/gltf/frontier/res_low_frontier_14.glb",
  res_low_frontier_15: "/assets/gltf/frontier/res_low_frontier_15.glb",
  res_low_frontier_16: "/assets/gltf/frontier/res_low_frontier_16.glb",
  res_low_frontier_17: "/assets/gltf/frontier/res_low_frontier_17.glb",
  res_low_frontier_18: "/assets/gltf/frontier/res_low_frontier_18.glb",
  res_low_frontier_19: "/assets/gltf/frontier/res_low_frontier_19.glb",
  res_low_frontier_20: "/assets/gltf/frontier/res_low_frontier_20.glb",
  res_low_frontier_21: "/assets/gltf/frontier/res_low_frontier_21.glb",
  res_low_frontier_22: "/assets/gltf/frontier/res_low_frontier_22.glb",
  com_frontier_00: "/assets/gltf/frontier/com_frontier_00.glb",
  ind_frontier_00: "/assets/gltf/frontier/ind_frontier_00.glb",
  res_high_frontier_01: "/assets/gltf/frontier/res_high_frontier_01.glb",
  res_low_industrial_00: "/assets/gltf/industrial/res_low_industrial_00.glb",
  res_low_industrial_01: "/assets/gltf/industrial/res_low_industrial_01.glb",
  res_low_industrial_02: "/assets/gltf/industrial/res_low_industrial_02.glb",
  res_high_industrial_00: "/assets/gltf/industrial/res_high_industrial_00.glb",
  res_high_industrial_01: "/assets/gltf/industrial/res_high_industrial_01.glb",
  res_high_industrial_03: "/assets/gltf/industrial/res_high_industrial_03.glb",
  com_industrial_00: "/assets/gltf/industrial/com_industrial_00.glb",
  com_industrial_01: "/assets/gltf/industrial/com_industrial_01.glb",
  ind_industrial_00: "/assets/gltf/industrial/ind_industrial_00.glb",
  ind_industrial_01: "/assets/gltf/industrial/ind_industrial_01.glb",
  res_low_postwar_00: "/assets/gltf/postwar/res_low_postwar_00.glb",
  res_high_postwar_00: "/assets/gltf/postwar/res_high_postwar_00.glb",
  com_postwar_00: "/assets/gltf/postwar/com_postwar_00.glb",
  ind_postwar_00: "/assets/gltf/postwar/ind_postwar_00.glb",
  res_low_modern_00: "/assets/gltf/modern/res_low_modern_00.glb",
  res_high_modern_00: "/assets/gltf/modern/res_high_modern_00.glb",
  com_modern_00: "/assets/gltf/modern/com_modern_00.glb",
  ind_modern_00: "/assets/gltf/modern/ind_modern_00.glb",
  svc_modern_00: "/assets/gltf/modern/svc_modern_00.glb",
  svc_modern_01: "/assets/gltf/modern/svc_modern_01.glb",
  svc_modern_02: "/assets/gltf/modern/svc_modern_02.glb",
  svc_modern_03: "/assets/gltf/modern/svc_modern_03.glb",
  svc_modern_04: "/assets/gltf/modern/svc_modern_04.glb",
  svc_modern_05: "/assets/gltf/modern/svc_modern_05.glb",
  svc_modern_06: "/assets/gltf/modern/svc_modern_06.glb",
  svc_modern_07: "/assets/gltf/modern/svc_modern_07.glb",
  res_low_future_00: "/assets/gltf/future/res_low_future_00.glb",
  com_future_00: "/assets/gltf/future/com_future_00.glb",
  ind_future_00: "/assets/gltf/future/ind_future_00.glb",
};

export type ParsedArchetypeKey = {
  category: string;
  era: GltfEraSlug;
  variant: number;
};

/** Parse `{category}_{era}_{variant}` keys from sim-types `archetypeKey()`. */
export function parseArchetypeKey(key: string): ParsedArchetypeKey | null {
  for (const era of ERA_SLUGS) {
    const marker = `_${era}_`;
    const idx = key.indexOf(marker);
    if (idx === -1) continue;
    const category = key.slice(0, idx);
    const variant = Number.parseInt(key.slice(idx + marker.length), 10);
    if (!Number.isFinite(variant)) return null;
    return { category, era, variant };
  }
  return null;
}

function shippedKeysForCategoryEra(category: string, era: GltfEraSlug): ShippedGltfKey[] {
  const prefix = `${category}_${era}_`;
  return SHIPPED_GLTF_KEYS.filter((k) => k.startsWith(prefix));
}

/**
 * Map any sim archetype key to a shipped catalog key (same category × era).
 * Variants without a dedicated GLB reuse era representatives for visual variety.
 */
export function resolveCatalogKey(archetypeKey: string): ShippedGltfKey | null {
  if (archetypeKey in GLTF_CATALOG) {
    return archetypeKey as ShippedGltfKey;
  }

  const parsed = parseArchetypeKey(archetypeKey);
  if (!parsed) return null;

  const { category, era, variant } = parsed;
  const exact = `${category}_${era}_${String(variant).padStart(2, "0")}`;
  if (exact in GLTF_CATALOG) {
    return exact as ShippedGltfKey;
  }

  const candidates = shippedKeysForCategoryEra(category, era);
  if (candidates.length > 0) {
    return candidates[variant % candidates.length]!;
  }

  if (category === "svc" && "svc_modern_00" in GLTF_CATALOG) {
    return "svc_modern_00";
  }

  return null;
}

/** Resolve catalog path, falling back to era-derived path for unknown variants. */
export function resolveGltfPath(key: string): string | null {
  const catalogKey = resolveCatalogKey(key);
  if (catalogKey) {
    return GLTF_CATALOG[catalogKey];
  }
  return gltfPublicPath(key);
}

/** True when a shipped GLB can be resolved for this archetype key. */
export function hasGltfAsset(key: string): boolean {
  return resolveCatalogKey(key) !== null;
}

/** All catalog GLB URLs for play-page preload. */
export function allGltfPaths(): string[] {
  return Object.values(GLTF_CATALOG);
}

/** Hero landmarks — `web/public/assets/gltf/heroes/` (MESHY_HERO_LANDMARKS.md). */
export const HERO_GLTF_BASE = "/assets/gltf/heroes" as const;

/**
 * Hero GLB keys with documented paths. Assets load on demand (HEAD probe) — not
 * preloaded via `allGltfPaths()` until the file ships.
 */
export const HERO_GLTF_KEYS = [
  "hero_frontier_city_hall",
  "hero_frontier_church",
  "hero_industrial_steel_mill",
  "hero_industrial_train_station",
  "hero_postwar_civic_hall",
  "hero_postwar_hospital",
  "hero_modern_glass_tower",
  "hero_future_eco_tower",
] as const;

export type HeroGltfKey = (typeof HERO_GLTF_KEYS)[number];

export const HERO_GLTF_CATALOG: Record<HeroGltfKey, string> = {
  hero_frontier_city_hall: `${HERO_GLTF_BASE}/hero_frontier_city_hall.glb`,
  hero_frontier_church: `${HERO_GLTF_BASE}/hero_frontier_church.glb`,
  hero_industrial_steel_mill: `${HERO_GLTF_BASE}/hero_industrial_steel_mill.glb`,
  hero_industrial_train_station: `${HERO_GLTF_BASE}/hero_industrial_train_station.glb`,
  hero_postwar_civic_hall: `${HERO_GLTF_BASE}/hero_postwar_civic_hall.glb`,
  hero_postwar_hospital: `${HERO_GLTF_BASE}/hero_postwar_hospital.glb`,
  hero_modern_glass_tower: `${HERO_GLTF_BASE}/hero_modern_glass_tower.glb`,
  hero_future_eco_tower: `${HERO_GLTF_BASE}/hero_future_eco_tower.glb`,
};

/** True when a hero landmark key is registered in `HERO_GLTF_CATALOG`. */
export function hasHeroGltfKey(key: string): key is HeroGltfKey {
  return key in HERO_GLTF_CATALOG;
}

/** Resolve a hero landmark key to its public GLB path, or null when unmapped. */
export function resolveHeroGltfPath(key: string): string | null {
  if (hasHeroGltfKey(key)) {
    return HERO_GLTF_CATALOG[key];
  }
  return null;
}

/** HEAD probe — true when a GLB is present under `public/`. */
export async function checkGltfAssetExists(path: string): Promise<boolean> {
  try {
    const res = await fetch(path, { method: "HEAD", cache: "no-store" });
    const type = res.headers.get("content-type") ?? "";
    return (
      res.ok &&
      (type.includes("model/gltf") ||
        type.includes("octet-stream") ||
        type.includes("application"))
    );
  } catch {
    return false;
  }
}
