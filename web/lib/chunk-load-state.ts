/** Chunks that have entered the camera frustum at least once (streaming debug). */

const loadedChunkIndices = new Set<number>();

export function markChunkLoaded(chunkIndex: number): void {
  loadedChunkIndices.add(chunkIndex);
}

export function getLoadedChunkCount(): number {
  return loadedChunkIndices.size;
}

/** @internal test helper */
export function resetChunkLoadState(): void {
  loadedChunkIndices.clear();
}
