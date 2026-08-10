import { describe, expect, it } from "vitest";
import {
  formatEmergencyResponseMinutes,
  parseEmergencyResponseMinutes,
} from "./emergency-response";

describe("parseEmergencyResponseMinutes", () => {
  it("accepts finite non-negative numbers", () => {
    expect(parseEmergencyResponseMinutes(4.25)).toBe(4.25);
    expect(parseEmergencyResponseMinutes(0)).toBe(0);
    expect(parseEmergencyResponseMinutes(30)).toBe(30);
  });

  it("rejects non-finite or negative values", () => {
    expect(parseEmergencyResponseMinutes(-1)).toBeUndefined();
    expect(parseEmergencyResponseMinutes(Number.NaN)).toBeUndefined();
    expect(parseEmergencyResponseMinutes(Number.POSITIVE_INFINITY)).toBeUndefined();
    expect(parseEmergencyResponseMinutes("4")).toBeUndefined();
    expect(parseEmergencyResponseMinutes(null)).toBeUndefined();
  });
});

describe("formatEmergencyResponseMinutes", () => {
  it("formats sub-10 minutes to one decimal", () => {
    expect(formatEmergencyResponseMinutes(4.25)).toBe("4.3m");
    expect(formatEmergencyResponseMinutes(1)).toBe("1.0m");
  });

  it("formats larger values without a fractional part", () => {
    expect(formatEmergencyResponseMinutes(12.4)).toBe("12m");
    expect(formatEmergencyResponseMinutes(30)).toBe("30m");
    expect(formatEmergencyResponseMinutes(150.2)).toBe("150m");
  });

  it("shows an em dash for invalid input", () => {
    expect(formatEmergencyResponseMinutes(Number.NaN)).toBe("—");
    expect(formatEmergencyResponseMinutes(-2)).toBe("—");
  });
});
