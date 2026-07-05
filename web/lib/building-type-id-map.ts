/** TypeId band constants — must match @citymajor/sim-types TYPE_ID. */
const TYPE_ID = {
  RES_LOW_START: 100,
  RES_HIGH_START: 200,
  COM_START: 300,
  IND_START: 400,
  IND_END: 499,
} as const;

export type BuildingRecord = {
  id: string;
  category: string;
  era: number;
  density?: number;
};

function categoryBase(category: string, density: number): number | null {
  switch (category) {
    case "residential":
      return density >= 2 ? TYPE_ID.RES_HIGH_START : TYPE_ID.RES_LOW_START;
    case "commercial":
      return TYPE_ID.COM_START;
    case "industrial":
      return TYPE_ID.IND_START;
    case "service":
      return null;
    default:
      return null;
  }
}

/** buildings.json slug → sim TypeId (DATA_BRIDGE rules). */
export function buildSlugToTypeIdMap(
  buildingRecords: readonly BuildingRecord[],
): Readonly<Record<string, number>> {
  const map: Record<string, number> = {};
  const zoneBands = new Map<string, BuildingRecord[]>();
  const serviceBands = new Map<number, BuildingRecord[]>();
  const heroBuildings: BuildingRecord[] = [];

  for (const building of buildingRecords) {
    const base = categoryBase(building.category, building.density ?? 0);
    if (base !== null) {
      const key = `${base}:${building.era}`;
      const band = zoneBands.get(key) ?? [];
      band.push(building);
      zoneBands.set(key, band);
      continue;
    }
    if (building.category === "service") {
      const band = serviceBands.get(building.era) ?? [];
      band.push(building);
      serviceBands.set(building.era, band);
      continue;
    }
    if (building.category === "infrastructure" || building.category === "special") {
      heroBuildings.push(building);
    }
  }

  for (const [key, band] of zoneBands) {
    const [baseStr] = key.split(":");
    const base = Number(baseStr);
    band.sort((a, b) => a.id.localeCompare(b.id));
    band.forEach((building, index) => {
      if (index >= 20) return;
      map[building.id] = base + building.era * 20 + index;
    });
  }

  for (const [era, band] of serviceBands) {
    band.sort((a, b) => a.id.localeCompare(b.id));
    band.forEach((building, index) => {
      if (index >= 20) return;
      map[building.id] = 500 + era * 20 + index;
    });
  }

  heroBuildings.sort((a, b) => a.id.localeCompare(b.id));
  heroBuildings.forEach((building, index) => {
    map[building.id] = 600 + index;
  });

  return map;
}

/** Whether a TypeId is in the sim taxonomy (zone, service bridge, or hero band). */
export function isKnownBuildingTypeId(typeId: number): boolean {
  if (typeId >= TYPE_ID.RES_LOW_START && typeId <= TYPE_ID.IND_END) return true;
  if (typeId >= 500 && typeId < 600) return true;
  if (typeId >= 600 && typeId < 700) return true;
  return false;
}
