import { describe, expect, it } from "vitest";
import {
  ILLEGAL_HIGHWAY_MERGE_TOAST,
  ROAD_TIER_DEFINITIONS,
  roadTierLabel,
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
});
