import { describe, expect, it } from "vitest";
import {
  formatGoodFlowUnits,
  formatGoodPrice,
  formatInventoryRate,
  formatProductionVsDemand,
} from "./economy-goods";

describe("formatGoodPrice", () => {
  it("formats sub-dollar prices with two decimals", () => {
    expect(formatGoodPrice(2.84)).toBe("$2.84");
  });

  it("formats mid-range prices with one decimal", () => {
    expect(formatGoodPrice(12.5)).toBe("$12.5");
  });

  it("formats large prices without decimals", () => {
    expect(formatGoodPrice(128.7)).toBe("$129");
  });

  it("returns dash for invalid values", () => {
    expect(formatGoodPrice(Number.NaN)).toBe("—");
    expect(formatGoodPrice(-1)).toBe("—");
  });
});

describe("formatGoodFlowUnits", () => {
  it("formats small units with two decimals", () => {
    expect(formatGoodFlowUnits(2.84)).toBe("2.84");
  });

  it("formats mid-range units with one decimal", () => {
    expect(formatGoodFlowUnits(12.5)).toBe("12.5");
  });

  it("formats large units without decimals", () => {
    expect(formatGoodFlowUnits(128.7)).toBe("129");
  });

  it("returns dash for invalid values", () => {
    expect(formatGoodFlowUnits(Number.NaN)).toBe("—");
    expect(formatGoodFlowUnits(-1)).toBe("—");
  });
});

describe("formatInventoryRate", () => {
  it("formats surplus inventory as positive percent", () => {
    expect(formatInventoryRate(0.32)).toBe("+32%");
  });

  it("formats deficit inventory without plus sign", () => {
    expect(formatInventoryRate(-0.5)).toBe("-50%");
  });

  it("clamps out-of-range rates", () => {
    expect(formatInventoryRate(2)).toBe("+100%");
    expect(formatInventoryRate(-2)).toBe("-100%");
  });

  it("returns dash for invalid values", () => {
    expect(formatInventoryRate(Number.NaN)).toBe("—");
  });
});

describe("formatProductionVsDemand", () => {
  it("joins production and demand units", () => {
    expect(formatProductionVsDemand(12.4, 18.2)).toBe("12.4 prod / 18.2 dem");
  });
});
