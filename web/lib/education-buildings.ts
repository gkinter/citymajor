/**
 * Education service buildings — TypeIds and GLTF keys from DATA_BRIDGE.md.
 * v1 build menu: frontier schoolhouse + industrial-era elementary/high school tier.
 */

export type EducationBuildingEntry = {
  /** buildings.json slug */
  slug: string;
  /** HUD label */
  label: string;
  /** Sim TypeId (500 + era*20 + index) */
  typeId: number;
  /** GLTF catalog key under /assets/gltf/modern/ */
  gltfKey: string;
  /** Era band (0=frontier … 3=future) */
  era: number;
};

/** Placeable education buildings for the v1 build toolbar. */
export const EDUCATION_BUILDINGS: readonly EducationBuildingEntry[] = [
  {
    slug: "svc_frontier_schoolhouse",
    label: "Schoolhouse",
    typeId: 503,
    gltfKey: "svc_modern_03",
    era: 0,
  },
  {
    slug: "svc_industrial_high_school",
    label: "Elementary",
    typeId: 524,
    gltfKey: "svc_modern_04",
    era: 1,
  },
] as const;

export type EducationBuildTypeId = (typeof EDUCATION_BUILDINGS)[number]["typeId"];

export function educationBuildingByTypeId(
  typeId: number,
): EducationBuildingEntry | undefined {
  return EDUCATION_BUILDINGS.find((entry) => entry.typeId === typeId);
}

export function isEducationBuildTypeId(typeId: number): typeId is EducationBuildTypeId {
  return educationBuildingByTypeId(typeId) !== undefined;
}
