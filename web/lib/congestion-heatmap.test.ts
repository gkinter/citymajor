import { describe, expect, it } from "vitest";
import type { RoadGraphSnapshot } from "@/lib/sim-bridge";
import {
  edgeCongestionDensity,
  resolveTrafficOverlayTiles,
  tilesAlongSegment,
  trafficTilesFromRoadGraph,
} from "./congestion-heatmap";

describe("congestion-heatmap", () => {
  it("normalizes edge volumes to 0–1 density", () => {
    expect(edgeCongestionDensity(0, [10, 40], undefined, 40, 0, 0)).toBe(0.25);
    expect(edgeCongestionDensity(1, [10, 40], undefined, 40, 0, 0)).toBe(1);
  });

  it("falls back to travel-time stretch when volumes missing", () => {
    expect(
      edgeCongestionDensity(0, undefined, [2, 8], 0, 2, 8),
    ).toBeCloseTo(0);
    expect(
      edgeCongestionDensity(1, undefined, [2, 8], 0, 2, 8),
    ).toBeCloseTo(1);
  });

  it("traces inclusive segment tiles", () => {
    expect(tilesAlongSegment(0, 0, 2, 0)).toEqual([
      { tileX: 0, tileZ: 0 },
      { tileX: 1, tileZ: 0 },
      { tileX: 2, tileZ: 0 },
    ]);
    expect(tilesAlongSegment(1, 1, 1, 3)).toEqual([
      { tileX: 1, tileZ: 1 },
      { tileX: 1, tileZ: 2 },
      { tileX: 1, tileZ: 3 },
    ]);
  });

  it("builds overlay tiles from roadGraph edgeVolumes", () => {
    const graph: RoadGraphSnapshot = {
      nodeCount: 2,
      nodeTypes: [0, 0],
      nodeTileX: [0, 3],
      nodeTileZ: [0, 0],
      edgeCount: 1,
      edgeFrom: [0],
      edgeTo: [1],
      edgeVolumes: [50],
      travelTimes: [4],
    };

    const tiles = trafficTilesFromRoadGraph(graph);
    expect(tiles).toHaveLength(4);
    expect(tiles.every((t) => t.density === 1)).toBe(true);
    expect(tiles.map((t) => t.tileX).sort()).toEqual([0, 1, 2, 3]);
  });

  it("prefers roadGraph over fallback traffic tiles", () => {
    const graph: RoadGraphSnapshot = {
      nodeCount: 2,
      nodeTypes: [0, 0],
      nodeTileX: [0, 1],
      nodeTileZ: [0, 0],
      edgeCount: 1,
      edgeFrom: [0],
      edgeTo: [1],
      edgeVolumes: [20],
    };
    const resolved = resolveTrafficOverlayTiles(graph, [
      { tileX: 9, tileZ: 9, density: 0.5 },
    ]);
    expect(resolved.some((t) => t.tileX === 9)).toBe(false);
    expect(resolved.length).toBeGreaterThan(0);
  });

  it("falls back when roadGraph lacks edge metrics", () => {
    const fallback = [{ tileX: 2, tileZ: 3, density: 0.4 }];
    expect(resolveTrafficOverlayTiles(undefined, fallback)).toEqual(fallback);
    expect(
      resolveTrafficOverlayTiles(
        {
          nodeCount: 1,
          nodeTypes: [0],
          nodeTileX: [0],
          nodeTileZ: [0],
        },
        fallback,
      ),
    ).toEqual(fallback);
  });
});
