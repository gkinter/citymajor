/**
 * Tracks which catalog GLB modules finished loading (or failed) inside the R3F tree.
 * Box instancing stays visible until a bucket reports loaded — avoids a black city
 * while large Meshy GLBs (~10MB) stream in or when a load fails.
 */

const loadedCatalogKeys = new Set<string>();
const failedCatalogKeys = new Set<string>();

export function markGltfCatalogLoaded(catalogKey: string): void {
  loadedCatalogKeys.add(catalogKey);
  failedCatalogKeys.delete(catalogKey);
}

export function markGltfCatalogFailed(catalogKey: string): void {
  failedCatalogKeys.add(catalogKey);
  loadedCatalogKeys.delete(catalogKey);
}

export function isGltfCatalogLoaded(catalogKey: string): boolean {
  return loadedCatalogKeys.has(catalogKey);
}

export function isGltfCatalogFailed(catalogKey: string): boolean {
  return failedCatalogKeys.has(catalogKey);
}

/** @internal test helper */
export function resetGltfCatalogLoadState(): void {
  loadedCatalogKeys.clear();
  failedCatalogKeys.clear();
}
