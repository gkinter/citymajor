import { describe, expect, it } from "vitest";
import {
  formatActiveEventCountBadge,
  resolveActiveEventCount,
} from "./active-event-count";
import type { ActiveEventSnapshot } from "@/lib/sim-bridge";

const sampleEvent: ActiveEventSnapshot = {
  eventId: 1,
  typeId: "housing_crisis",
  phase: "active",
  severity: 0.7,
  tileX: 4,
  tileY: 8,
};

describe("resolveActiveEventCount", () => {
  it("prefers the WASM scalar when present", () => {
    expect(resolveActiveEventCount(2, [sampleEvent])).toBe(2);
    expect(resolveActiveEventCount(0, [sampleEvent])).toBe(0);
  });

  it("falls back to activeEvents length", () => {
    expect(resolveActiveEventCount(undefined, [sampleEvent])).toBe(1);
    expect(resolveActiveEventCount(undefined, [sampleEvent, sampleEvent])).toBe(
      2,
    );
    expect(resolveActiveEventCount(undefined, undefined)).toBe(0);
  });

  it("rejects non-finite / negative scalars", () => {
    expect(resolveActiveEventCount(Number.NaN, [sampleEvent])).toBe(1);
    expect(resolveActiveEventCount(-1, [sampleEvent])).toBe(1);
  });
});

describe("formatActiveEventCountBadge", () => {
  it("omits zero and negatives", () => {
    expect(formatActiveEventCountBadge(0)).toBeUndefined();
    expect(formatActiveEventCountBadge(-2)).toBeUndefined();
  });

  it("formats positive counts for the Herald badge", () => {
    expect(formatActiveEventCountBadge(1)).toBe("1");
    expect(formatActiveEventCountBadge(3.9)).toBe("3");
  });
});
