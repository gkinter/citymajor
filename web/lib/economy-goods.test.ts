import { describe, expect, it } from "vitest";
import { formatGoodPrice } from "./economy-goods";

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
