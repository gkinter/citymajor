import { describe, expect, it } from "vitest";
import { buildTickerHeadlines } from "@/components/city/NewsTicker";

describe("buildTickerHeadlines", () => {
  it("returns economy_shortage headline when goods shortage is high", () => {
    const headlines = buildTickerHeadlines(undefined, {
      tick: 1,
      population: 1200,
      cityFunds: 50_000,
      era: 0,
      goodsShortageIndex: 0.42,
      healthcareCoverage: 0.5,
    });

    expect(headlines[0]).toContain("supply chains strain");
  });

  it("prefers active sim events over metric bucket headlines", () => {
    const headlines = buildTickerHeadlines(
      [{ eventId: 1, typeId: "housing_crisis", phase: "active", severity: 1, tileX: 0, tileY: 0 }],
      { tick: 1, population: 1200, cityFunds: 50_000, era: 0 },
    );

    expect(headlines[0]).toContain("Housing Crisis");
  });

  it("shows housing_shortage headline from active sim event", () => {
    const headlines = buildTickerHeadlines(
      [{ eventId: 2, typeId: "housing_shortage", phase: "active", severity: 1, tileX: 0, tileY: 0 }],
      { tick: 1, population: 1200, cityFunds: 50_000, era: 0 },
    );

    expect(headlines[0]).toContain("Housing Shortage");
  });
});
