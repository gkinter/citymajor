import { describe, expect, it } from "vitest";
import {
  ILLEGAL_HIGHWAY_MERGE_TOAST,
  ROAD_FLAG_RAMP,
  ROAD_TIER_DEFINITIONS,
  resolveRampPaintTier,
  roadTierLabel,
  roadToolOverlayColor,
} from "./road-types";

describe("road-types", () => {
  it("labels tiers Local / Collector / Highway", () => {
    expect(ROAD_TIER_DEFINITIONS.map((d) => d.label)).toEqual([
      "Local",
      "Collector",
      "Highway",
    ]);
    expect(roadTierLabel(0)).toBe("Local");
    expect(roadTierLabel(1)).toBe("Collector");
    expect(roadTierLabel(2)).toBe("Highway");
  });

  it("exposes illegal highway merge toast copy", () => {
    expect(ILLEGAL_HIGHWAY_MERGE_TOAST).toBe(
      "Illegal highway merge — use a ramp",
    );
  });

  it("encodes Ramp as structure bits 6+7 (0xC0)", () => {
    expect(ROAD_FLAG_RAMP).toBe(0xc0);
  });

  it("resolves ramp paint tier to collector when unlocked, else local", () => {
    // Only dirt_road is baseline — empty unlocks → local (0).
    expect(resolveRampPaintTier([])).toBe(0);
    expect(resolveRampPaintTier(undefined)).toBe(0);
  });

  it("uses distinct ramp overlay color", () => {
    expect(roadToolOverlayColor("ramp")).toBe("#b45309");
    expect(roadToolOverlayColor(2)).not.toBe(roadToolOverlayColor("ramp"));
  });
});
