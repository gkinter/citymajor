export type BuildCategory = "civic" | "education" | "safety" | "utilities";
export type BuildCatalogEntry = {
  typeId: number;
  name: string;
  category: BuildCategory;
  unlockKey?: string;
};
export const BUILD_TABS: { id: BuildCategory; label: string }[] = [
  { id: "civic", label: "Civic" },
  { id: "education", label: "Education" },
  { id: "safety", label: "Safety" },
  { id: "utilities", label: "Utilities" },
];
export const BUILD_CATALOG: BuildCatalogEntry[] = [
  { typeId: 521, name: "City Hall", category: "civic", unlockKey: "zoning_office" },
  { typeId: 526, name: "Public Library", category: "civic", unlockKey: "public_library" },
  { typeId: 522, name: "Elementary School", category: "education", unlockKey: "elementary_school" },
  { typeId: 524, name: "High School", category: "education", unlockKey: "military_academy" },
  { typeId: 523, name: "Fire Station", category: "safety", unlockKey: "fire_station" },
  { typeId: 527, name: "Police Station", category: "safety", unlockKey: "police_station" },
  { typeId: 601, name: "Town Well", category: "utilities", unlockKey: "well" },
  { typeId: 610, name: "Coal Power Plant", category: "utilities", unlockKey: "coal_power_plant" },
  { typeId: 612, name: "Water Tower", category: "utilities", unlockKey: "reservoir" },
];
export function buildsForCategory(c: BuildCategory) {
  return BUILD_CATALOG.filter((e) => e.category === c);
}
