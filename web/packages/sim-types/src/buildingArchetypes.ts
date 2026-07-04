/**
 * Building archetype taxonomy for 3D mesh selection.
 *
 * Ports classification logic from Forge.Engine.Rendering.BuildingRenderer.
 * ADR: docs/design/BUILDING_ARCHETYPE_3D.md (Linear SB-3677)
 */

// ---------------------------------------------------------------------------
// TypeId range constants (must match BuildingRenderer.cs)
// ---------------------------------------------------------------------------

export const TYPE_ID = {
  RES_LOW_START: 100,
  RES_LOW_END: 199,
  RES_HIGH_START: 200,
  RES_HIGH_END: 299,
  COM_START: 300,
  COM_END: 399,
  IND_START: 400,
  IND_END: 499,
} as const;

export const CONSTRUCTION_CONDITION_THRESHOLD = 50;
export const PIXELS_PER_STORY = 16;
export const MAX_STORIES = 12;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

export enum BuildingCategory {
  ResidentialLow = 'res_low',
  ResidentialHigh = 'res_high',
  Commercial = 'com',
  Industrial = 'ind',
  Service = 'svc',
}

export enum Era {
  Frontier = 0,
  Industrial = 1,
  Postwar = 2,
  Modern = 3,
  Future = 4,
}

export enum BuildingState {
  Constructing = 0,
  Operational = 1,
  Abandoned = 2,
  Demolishing = 3,
}

export type RoofArchetype =
  | 'peaked'
  | 'flat_lip'
  | 'sawtooth'
  | 'flat_accent'
  | 'flat';

// ---------------------------------------------------------------------------
// Era slug helpers
// ---------------------------------------------------------------------------

const ERA_SLUGS: Record<Era, string> = {
  [Era.Frontier]: 'frontier',
  [Era.Industrial]: 'industrial',
  [Era.Postwar]: 'postwar',
  [Era.Modern]: 'modern',
  [Era.Future]: 'future',
};

export function eraSlug(era: Era): string {
  return ERA_SLUGS[era];
}

// ---------------------------------------------------------------------------
// Material hints (hex colors per era, ported from GetBuildingColor)
// ---------------------------------------------------------------------------

export interface MaterialHint {
  /** Primary wall color endpoint */
  wallA: string;
  /** Secondary wall color endpoint (lerp target) */
  wallB: string;
  /** Optional roof tint override */
  roof?: string;
}

export type CategoryEraPalette = Record<Era, MaterialHint>;

/** Wall color lerp endpoints per category × era (hex #RRGGBB). */
export const ERA_MATERIAL_HINTS: Record<
  Exclude<BuildingCategory, BuildingCategory.Service>,
  CategoryEraPalette
> = {
  [BuildingCategory.ResidentialLow]: {
    [Era.Frontier]: { wallA: '#8D6E63', wallB: '#A1887F' },
    [Era.Industrial]: { wallA: '#B71C1C', wallB: '#C62828' },
    [Era.Postwar]: { wallA: '#ECEFF1', wallB: '#FFF9C4' },
    [Era.Modern]: { wallA: '#E0E0E0', wallB: '#CFD8DC' },
    [Era.Future]: { wallA: '#E1F5FE', wallB: '#B2EBF2' },
  },
  [BuildingCategory.ResidentialHigh]: {
    [Era.Frontier]: { wallA: '#8D6E63', wallB: '#A1887F' },
    [Era.Industrial]: { wallA: '#C62828', wallB: '#D32F2F' },
    [Era.Postwar]: { wallA: '#CFD8DC', wallB: '#ECEFF1' },
    [Era.Modern]: { wallA: '#90CAF9', wallB: '#E0E0E0' },
    [Era.Future]: { wallA: '#B2EBF2', wallB: '#E1F5FE' },
  },
  [BuildingCategory.Commercial]: {
    [Era.Frontier]: { wallA: '#78909C', wallB: '#8D6E63' },
    [Era.Industrial]: { wallA: '#455A64', wallB: '#607D8B' },
    [Era.Postwar]: { wallA: '#78909C', wallB: '#90A4AE' },
    [Era.Modern]: { wallA: '#1565C0', wallB: '#0D47A1' },
    [Era.Future]: { wallA: '#0D47A1', wallB: '#1565C0' },
  },
  [BuildingCategory.Industrial]: {
    [Era.Frontier]: { wallA: '#5D4037', wallB: '#6D4C41' },
    [Era.Industrial]: { wallA: '#37474F', wallB: '#455A64' },
    [Era.Postwar]: { wallA: '#455A64', wallB: '#546E7A' },
    [Era.Modern]: { wallA: '#455A64', wallB: '#607D8B' },
    [Era.Future]: { wallA: '#546E7A', wallB: '#78909C' },
  },
};

/** Service building accent colors by typeId % 5 */
export const SERVICE_MATERIAL_HINTS: MaterialHint[] = [
  { wallA: '#D96659', wallB: '#D96659', roof: '#D96659' }, // fire — red accent
  { wallA: '#5980CC', wallB: '#5980CC', roof: '#5980CC' }, // police — blue
  { wallA: '#D9D9CC', wallB: '#D9D9CC', roof: '#F5F5F0' }, // hospital — cream
  { wallA: '#73B373', wallB: '#73B373', roof: '#73B373' }, // school — green
  { wallA: '#CCBFA6', wallB: '#CCBFA6', roof: '#EBEBEB' }, // civic — tan
];

export const NIGHT_WINDOW_COLOR = '#FFE082';

// ---------------------------------------------------------------------------
// Classification (ports BuildingRenderer.cs)
// ---------------------------------------------------------------------------

export function classifyBuilding(typeId: number): BuildingCategory {
  if (typeId >= TYPE_ID.RES_LOW_START && typeId <= TYPE_ID.RES_LOW_END) {
    return BuildingCategory.ResidentialLow;
  }
  if (typeId >= TYPE_ID.RES_HIGH_START && typeId <= TYPE_ID.RES_HIGH_END) {
    return BuildingCategory.ResidentialHigh;
  }
  if (typeId >= TYPE_ID.COM_START && typeId <= TYPE_ID.COM_END) {
    return BuildingCategory.Commercial;
  }
  if (typeId >= TYPE_ID.IND_START && typeId <= TYPE_ID.IND_END) {
    return BuildingCategory.Industrial;
  }
  return BuildingCategory.Service;
}

function categoryBase(category: BuildingCategory): number {
  switch (category) {
    case BuildingCategory.ResidentialLow:
      return TYPE_ID.RES_LOW_START;
    case BuildingCategory.ResidentialHigh:
      return TYPE_ID.RES_HIGH_START;
    case BuildingCategory.Commercial:
      return TYPE_ID.COM_START;
    case BuildingCategory.Industrial:
      return TYPE_ID.IND_START;
    default:
      return 0;
  }
}

/**
 * Derive visual era from TypeId sub-range within category.
 * Service buildings always return Modern.
 */
export function deriveEra(typeId: number): Era {
  const category = classifyBuilding(typeId);
  if (category === BuildingCategory.Service) {
    return Era.Modern;
  }
  const offset = typeId - categoryBase(category);
  return Math.min(Math.max(Math.floor(offset / 20), 0), 4) as Era;
}

/** Variant index within era band (0–19). */
export function deriveVariant(typeId: number): number {
  const category = classifyBuilding(typeId);
  if (category === BuildingCategory.Service) {
    return typeId % 20;
  }
  const offset = typeId - categoryBase(category);
  return offset % 20;
}

/**
 * Mesh archetype key: `{category}_{era}_{variant}` e.g. `res_low_frontier_05`
 */
export function archetypeKey(typeId: number, variant?: number): string {
  const category = classifyBuilding(typeId);
  const era = deriveEra(typeId);
  const v = variant ?? deriveVariant(typeId);
  const variantStr = String(v).padStart(2, '0');
  return `${category}_${eraSlug(era)}_${variantStr}`;
}

export function roofArchetype(category: BuildingCategory): RoofArchetype {
  switch (category) {
    case BuildingCategory.ResidentialLow:
      return 'peaked';
    case BuildingCategory.ResidentialHigh:
    case BuildingCategory.Commercial:
      return 'flat_lip';
    case BuildingCategory.Industrial:
      return 'sawtooth';
    case BuildingCategory.Service:
      return 'flat_accent';
    default:
      return 'flat';
  }
}

/**
 * Story count from category + building level (ports ComputeStories).
 */
export function computeStories(category: BuildingCategory, level: number): number {
  const lvl = Math.max(level, 1);

  let baseStories: number;
  let totalStories: number;

  switch (category) {
    case BuildingCategory.ResidentialLow:
      baseStories = 1;
      totalStories = baseStories + Math.min(lvl - 1, 2);
      break;
    case BuildingCategory.ResidentialHigh:
      baseStories = 3;
      totalStories = baseStories + (lvl - 1) * 2;
      break;
    case BuildingCategory.Commercial:
      baseStories = 2;
      totalStories = baseStories + Math.min((lvl - 1) * 2, 8);
      break;
    case BuildingCategory.Industrial:
      baseStories = 1;
      totalStories = baseStories + Math.min(lvl - 1, 2);
      break;
    case BuildingCategory.Service:
      baseStories = 2;
      totalStories = baseStories + Math.min(lvl - 1, 1);
      break;
    default:
      baseStories = 1;
      totalStories = baseStories;
  }

  return Math.min(Math.max(totalStories, 1), MAX_STORIES);
}

/** Color variation factor 0–1 from typeId (matches C# typeId % 7 / 7). */
export function typeIdVariation(typeId: number): number {
  return (typeId % 7) / 7;
}

function hexToRgb(hex: string): [number, number, number] {
  const n = parseInt(hex.replace('#', ''), 16);
  return [(n >> 16) & 0xff, (n >> 8) & 0xff, n & 0xff];
}

function lerpHex(a: string, b: string, t: number): string {
  const [ar, ag, ab] = hexToRgb(a);
  const [br, bg, bb] = hexToRgb(b);
  const r = Math.round(ar + (br - ar) * t);
  const g = Math.round(ag + (bg - ag) * t);
  const bl = Math.round(ab + (bb - ab) * t);
  return `#${((1 << 24) + (r << 16) + (g << 8) + bl).toString(16).slice(1)}`;
}

/** Resolved wall color for a building (lerped palette + variation). */
export function resolveWallColor(typeId: number): string {
  const category = classifyBuilding(typeId);
  if (category === BuildingCategory.Service) {
    const hint = SERVICE_MATERIAL_HINTS[typeId % 5];
    return hint.wallA;
  }
  const era = deriveEra(typeId);
  const palette =
    ERA_MATERIAL_HINTS[category as Exclude<BuildingCategory, BuildingCategory.Service>];
  const hint = palette[era];
  return lerpHex(hint.wallA, hint.wallB, typeIdVariation(typeId));
}

/** Abandoned-state material modifier (desaturate + darken). */
export function applyAbandonedTint(hex: string): string {
  const [r, g, b] = hexToRgb(hex);
  const grey = (r + g + b) / 3;
  const mix = (c: number) => Math.round((c * 0.4 + grey * 0.6) * 0.7);
  return `#${((1 << 24) + (mix(r) << 16) + (mix(g) << 8) + mix(b)).toString(16).slice(1)}`;
}

export interface BuildingArchetype {
  typeId: number;
  category: BuildingCategory;
  era: Era;
  variant: number;
  key: string;
  roof: RoofArchetype;
  wallColor: string;
  /** Story count derived from level (present when `level > 0` is passed to `describeBuilding`). */
  stories?: number;
}

/** Full archetype descriptor for a TypeId. */
export function describeBuilding(typeId: number, level = 1): BuildingArchetype {
  const category = classifyBuilding(typeId);
  const base: BuildingArchetype = {
    typeId,
    category,
    era: deriveEra(typeId),
    variant: deriveVariant(typeId),
    key: archetypeKey(typeId),
    roof: roofArchetype(category),
    wallColor: resolveWallColor(typeId),
  };
  if (level > 0) base.stories = computeStories(category, level);
  return base;
}
