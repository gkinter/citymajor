import type { RciDemand, SimResources } from "@/lib/sim-bridge";
import { resolveRci } from "@/lib/zoning-economy";

export type RciCategory = keyof RciDemand;

/** Rough good → zoning hint for economy panel links. */
const GOOD_RCI_HINT: Partial<Record<string, RciCategory>> = {
  Food: "residential",
  Water: "residential",
  Healthcare: "residential",
  Clothing: "residential",
  ConsumerGoods: "residential",
  Electricity: "residential",
  Education: "residential",
  Entertainment: "commercial",
  FinancialServices: "commercial",
  DigitalServices: "commercial",
  Wheat: "industrial",
  Livestock: "industrial",
  Timber: "industrial",
  IronOre: "industrial",
  Coal: "industrial",
  Steel: "industrial",
  Lumber: "industrial",
  Fuel: "industrial",
  Electronics: "industrial",
  Vehicles: "industrial",
};

const RCI_LETTER: Record<RciCategory, "R" | "C" | "I"> = {
  residential: "R",
  commercial: "C",
  industrial: "I",
};

/** PascalCase enum name → display label. */
export function formatGoodName(name: string): string {
  return name.replace(/([a-z])([A-Z])/g, "$1 $2");
}

/** Zoning hint when a good maps to an R/C/I category. */
export function zoningHintForGood(name: string): string | undefined {
  const category = GOOD_RCI_HINT[name];
  if (!category) return undefined;
  const letter = RCI_LETTER[category];
  return `Shortage may ease with more ${letter} zoning`;
}

/** Fallback economy view from RCI when WASM goods export is absent. */
export function economyFromRciFallback(
  resources: SimResources | null,
): { label: string; shortages: { name: string; magnitude: number }[]; surpluses: { name: string; magnitude: number }[] } | null {
  const rci = resolveRci(resources);
  if (!rci) return null;

  const rows: { name: string; demand: number }[] = [
    { name: "Residential (RCI)", demand: rci.residential },
    { name: "Commercial (RCI)", demand: rci.commercial },
    { name: "Industrial (RCI)", demand: rci.industrial },
  ];

  const shortages = rows
    .filter((row) => row.demand > 0.05)
    .sort((a, b) => b.demand - a.demand)
    .map((row) => ({ name: row.name, magnitude: row.demand }));

  const surpluses = rows
    .filter((row) => row.demand < -0.05)
    .sort((a, b) => a.demand - b.demand)
    .map((row) => ({ name: row.name, magnitude: -row.demand }));

  return {
    label: "RCI demand (goods export pending WASM rebuild)",
    shortages,
    surpluses,
  };
}
