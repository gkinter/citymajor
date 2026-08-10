import { describe, expect, it } from "vitest";
import { techIdFromCatalogId } from "@/lib/tech-catalog";
import {
  getVisibleZoneTiers,
  isZoneTierEraUnlocked,
  isZoneTierUnlocked,
  isZoneTierVisible,
  simSupportsExtendedZoneBytes,
  zoneTierByTool,
} from "./zone-tiers";

describe("zone-tiers P2.1", () => {
  it("hides office/mixed/ag when sim lacks extended zone bytes", () => {
    const visible = getVisibleZoneTiers(false).map((t) => t.tool);
    expect(visible).toEqual([
      "residential",
      "residential_high",
      "commercial",
      "industrial",
    ]);
    expect(simSupportsExtendedZoneBytes(false)).toBe(false);
    expect(isZoneTierVisible(zoneTierByTool("office")!, false)).toBe(false);
  });

  it("shows extended tiers when WASM PaintZone is available", () => {
    const visible = getVisibleZoneTiers(true).map((t) => t.tool);
    expect(visible).toContain("office");
    expect(visible).toContain("mixed");
    expect(visible).toContain("agricultural");
  });

  it("gates office at Industrial era and mixed at Colonial (Frontier)", () => {
    const office = zoneTierByTool("office")!;
    const mixed = zoneTierByTool("mixed")!;
    const ag = zoneTierByTool("agricultural")!;

    expect(isZoneTierEraUnlocked(office, 0)).toBe(false);
    expect(isZoneTierEraUnlocked(office, 1)).toBe(true);
    expect(isZoneTierEraUnlocked(mixed, 0)).toBe(true);
    expect(isZoneTierEraUnlocked(ag, 0)).toBe(true);
  });

  it("unlocks office with Industrial era + Stock Exchange research", () => {
    const office = zoneTierByTool("office")!;
    const t134 = techIdFromCatalogId("T134");
    expect(t134).toBeGreaterThanOrEqual(0);
    expect(isZoneTierUnlocked(office, [], 1)).toBe(false);
    expect(isZoneTierUnlocked(office, [t134], 0)).toBe(false);
    expect(isZoneTierUnlocked(office, [t134], 1)).toBe(true);
  });

  it("unlocks agricultural at Frontier without research", () => {
    const ag = zoneTierByTool("agricultural")!;
    expect(isZoneTierUnlocked(ag, [], 0)).toBe(true);
  });
});
