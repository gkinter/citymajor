import { describe, expect, it } from "vitest";
import { deriveBucketFromEventType } from "./event-catalog";

describe("deriveBucketFromEventType", () => {
  it("maps supply-chain shortage events to economy_shortage", () => {
    expect(deriveBucketFromEventType("goods_shortage_alert")).toBe(
      "economy_shortage",
    );
    expect(deriveBucketFromEventType("supply_chain_disruption")).toBe(
      "economy_shortage",
    );
    expect(deriveBucketFromEventType("warehouse_stockout")).toBe(
      "economy_shortage",
    );
  });

  it("still maps housing pressure to housing_shortage", () => {
    expect(deriveBucketFromEventType("housing_crisis")).toBe(
      "housing_shortage",
    );
  });
});
