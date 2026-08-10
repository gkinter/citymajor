import { describe, expect, it } from "vitest";
import {
  formatModeShare,
  formatModeShareTriplet,
  hasModeShares,
  parseModeShare,
} from "./mode-share";

describe("parseModeShare", () => {
  it("accepts finite 0–1 values", () => {
    expect(parseModeShare(0)).toBe(0);
    expect(parseModeShare(0.65)).toBe(0.65);
    expect(parseModeShare(1)).toBe(1);
  });

  it("clamps out-of-range finite numbers", () => {
    expect(parseModeShare(1.2)).toBe(1);
    expect(parseModeShare(-0.1)).toBe(0);
  });

  it("rejects non-finite / non-number", () => {
    expect(parseModeShare(undefined)).toBeUndefined();
    expect(parseModeShare(null)).toBeUndefined();
    expect(parseModeShare("0.65")).toBeUndefined();
    expect(parseModeShare(Number.NaN)).toBeUndefined();
    expect(parseModeShare(Number.POSITIVE_INFINITY)).toBeUndefined();
  });
});

describe("formatModeShare / formatModeShareTriplet", () => {
  it("formats percent shares", () => {
    expect(formatModeShare(0.654)).toBe("65%");
    expect(formatModeShare(0)).toBe("0%");
    expect(formatModeShare(1)).toBe("100%");
  });

  it("formats the Transport HUD triplet", () => {
    expect(formatModeShareTriplet(0.65, 0.2, 0.15)).toBe(
      "Car 65% · Transit 20% · Walk 15%",
    );
  });
});

describe("hasModeShares", () => {
  it("is true when any share is defined", () => {
    expect(hasModeShares({})).toBe(false);
    expect(hasModeShares({ carModeShare: 0.65 })).toBe(true);
    expect(hasModeShares({ transitModeShare: 0.2 })).toBe(true);
    expect(hasModeShares({ walkModeShare: 0.15 })).toBe(true);
  });
});
