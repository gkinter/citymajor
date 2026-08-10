import { describe, expect, it } from "vitest";
import {
  DEFAULT_ROAD_ELEVATION,
  encodeRoadFlags,
  ILLEGAL_HIGHWAY_MERGE_TOAST,
  ROAD_FLAG_BRIDGE,
  ROAD_FLAG_RAMP,
  ROAD_FLAG_TUNNEL,
  ROAD_TIER_DEFINITIONS,
  resolveRampPaintTier,
  roadElevationFlags,
  roadElevationLabel,
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

  it("maps elevation mode to mutually exclusive bridge/tunnel flags", () => {
    expect(DEFAULT_ROAD_ELEVATION).toBe("none");
    expect(roadElevationFlags("none")).toEqual({
      bridge: false,
      tunnel: false,
    });
    expect(roadElevationFlags("bridge")).toEqual({
      bridge: true,
      tunnel: false,
    });
    expect(roadElevationFlags("tunnel")).toEqual({
      bridge: false,
      tunnel: true,
    });
    expect(roadElevationLabel("bridge")).toBe("Bridge");
    expect(roadElevationLabel("tunnel")).toBe("Tunnel");
    expect(roadElevationLabel("none")).toBe("Grade");
  });

  it("encodes tier + elevation into RoadFlags bits", () => {
    expect(encodeRoadFlags(0, "none")).toBe(0x01);
    expect(encodeRoadFlags(1, "none")).toBe(0x11);
    expect(encodeRoadFlags(2, "none")).toBe(0x21);
    expect(encodeRoadFlags(1, "bridge")).toBe(0x11 | ROAD_FLAG_BRIDGE);
    expect(encodeRoadFlags(2, "tunnel")).toBe(0x21 | ROAD_FLAG_TUNNEL);
    expect(encodeRoadFlags(0, "bridge") & ROAD_FLAG_BRIDGE).toBe(
      ROAD_FLAG_BRIDGE,
    );
    expect(encodeRoadFlags(0, "bridge") & ROAD_FLAG_TUNNEL).toBe(0);
    expect(encodeRoadFlags(0, "tunnel") & ROAD_FLAG_TUNNEL).toBe(
      ROAD_FLAG_TUNNEL,
    );
    expect(encodeRoadFlags(0, "tunnel") & ROAD_FLAG_BRIDGE).toBe(0);
  });

  it("ramp overrides elevation in encodeRoadFlags", () => {
    expect(encodeRoadFlags(1, "bridge", true) & ROAD_FLAG_RAMP).toBe(
      ROAD_FLAG_RAMP,
    );
  });
});
