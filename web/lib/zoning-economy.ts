import { RCI_EXTREME_DEMAND } from "@/lib/sim-metrics";
import type { RciDemand, SimResources } from "@/lib/sim-bridge";
import type { ZoningTool } from "@/lib/zoning";

/** Demand at or above this (-1..+1) suggests zoning this type. */
export const RCI_HIGH_DEMAND = 0.35;

/** Demand at or below this counts as surplus — deprioritize zoning. */
export const RCI_SURPLUS = -0.25;

const ZONE_RCI_KEY: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  keyof RciDemand
> = {
  residential: "residential",
  residential_high: "residential",
  commercial: "commercial",
  industrial: "industrial",
  office: "commercial",
  mixed: "residential",
  agricultural: "industrial",
};

const ZONE_LETTER: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  string
> = {
  residential: "R",
  residential_high: "R",
  commercial: "C",
  industrial: "I",
  office: "O",
  mixed: "M",
  agricultural: "A",
};

const ZONE_NAME: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  string
> = {
  residential: "residential",
  residential_high: "high-density residential",
  commercial: "commercial",
  industrial: "industrial",
  office: "office",
  mixed: "mixed-use",
  agricultural: "agricultural",
};

function clampDemand(value: number): number {
  return Math.max(-1, Math.min(1, value));
}

function hasWasmRci(resources: SimResources): boolean {
  return (
    resources.residentialDemand !== undefined &&
    resources.commercialDemand !== undefined &&
    resources.industrialDemand !== undefined
  );
}

/** Fallback when WASM status lacks EconomySystem RCI fields (pre-rebuild bundle). */
function mockRciFromSnapshot(resources: SimResources): RciDemand {
  const pop = resources.population;
  if (pop <= 0) {
    return { residential: 0, commercial: 0, industrial: 0 };
  }
  const eraFactor = Math.min(1, (resources.era ?? 0) / 4);
  return {
    residential: clampDemand(pop / 12_000 - 0.15),
    commercial: clampDemand(pop / 9_000 - 0.2),
    industrial: clampDemand(eraFactor * 0.45 + pop / 20_000 - 0.25),
  };
}

/** Resolve R/C/I demand for HUD and zoning tooltips. */
export function resolveRci(resources: SimResources | null): RciDemand | null {
  if (!resources) return null;
  if (hasWasmRci(resources)) {
    return {
      residential: clampDemand(resources.residentialDemand!),
      commercial: clampDemand(resources.commercialDemand!),
      industrial: clampDemand(resources.industrialDemand!),
    };
  }
  return mockRciFromSnapshot(resources);
}

export function zoningDemandForTool(
  tool: ZoningTool,
  rci: RciDemand | null,
): number | null {
  if (!rci || tool === "bulldoze" || tool === "road") return null;
  return rci[ZONE_RCI_KEY[tool]];
}

/**
 * Player-facing cause→effect hint for zoning toolbar buttons.
 * Example: "High residential demand — zone R"
 */
export function zoningDemandHint(
  tool: ZoningTool,
  rci: RciDemand | null,
): string | undefined {
  if (tool === "bulldoze") {
    return "Clear zones to prepare land for redevelopment";
  }
  if (tool === "road") {
    return "Paint roads to connect zones and unlock growth";
  }

  const demand = zoningDemandForTool(tool, rci);
  if (demand === null) return undefined;

  const letter = ZONE_LETTER[tool];
  const name = ZONE_NAME[tool];

  if (demand >= RCI_EXTREME_DEMAND) {
    return `High ${name} demand — zone ${letter}`;
  }
  if (demand >= RCI_HIGH_DEMAND) {
    return `Rising ${name} demand — zone ${letter} to grow`;
  }
  if (demand <= RCI_SURPLUS) {
    const label = name.charAt(0).toUpperCase() + name.slice(1);
    return `${label} surplus — try other zones`;
  }
  return `Balanced ${name} demand`;
}

/** True when HUD demand suggests prioritizing this zoning tool. */
export function isZoningDemandHigh(
  tool: ZoningTool,
  rci: RciDemand | null,
): boolean {
  const demand = zoningDemandForTool(tool, rci);
  return demand !== null && demand >= RCI_HIGH_DEMAND;
}
