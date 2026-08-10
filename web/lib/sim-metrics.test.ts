import { describe, expect, it } from "vitest";
import {
  deriveNarrativeBucket,
  explainNarrativeBucket,
  GOODS_SHORTAGE_THRESHOLD,
  PROSPERITY_APPROVAL,
  PROSPERITY_FUNDS,
  RCI_EXTREME_DEMAND,
} from "./sim-metrics";

const balancedHealthcare = 0.5;

describe("deriveNarrativeBucket", () => {
  it("maps goodsShortageIndex >= 0.35 to economy_shortage", () => {
    expect(
      deriveNarrativeBucket({
        healthcareCoverage: balancedHealthcare,
        goodsShortageIndex: GOODS_SHORTAGE_THRESHOLD,
      }),
    ).toBe("economy_shortage");
  });

  it("maps goods shortage plus extreme residential demand to housing_shortage", () => {
    expect(
      deriveNarrativeBucket({
        healthcareCoverage: balancedHealthcare,
        goodsShortageIndex: 0.5,
        residentialDemand: RCI_EXTREME_DEMAND,
      }),
    ).toBe("housing_shortage");
  });

  it("blocks prosperity when goods shortage is high", () => {
    expect(
      deriveNarrativeBucket({
        healthcareCoverage: balancedHealthcare,
        goodsShortageIndex: 0.4,
        approval: PROSPERITY_APPROVAL,
        cityFunds: PROSPERITY_FUNDS,
        commercialDemand: RCI_EXTREME_DEMAND,
        industrialDemand: RCI_EXTREME_DEMAND,
      }),
    ).toBe("economy_shortage");
  });
});

describe("explainNarrativeBucket", () => {
  it("explains economy_shortage from goods shortage index", () => {
    const { bucket, reason } = explainNarrativeBucket({
      healthcareCoverage: balancedHealthcare,
      goodsShortageIndex: 0.42,
    });

    expect(bucket).toBe("economy_shortage");
    expect(reason).toContain("42%");
    expect(reason).toContain("supply-chain");
  });

  it("explains housing_shortage when goods and residential demand collide", () => {
    const { bucket, reason } = explainNarrativeBucket({
      healthcareCoverage: balancedHealthcare,
      goodsShortageIndex: 0.5,
      residentialDemand: RCI_EXTREME_DEMAND,
    });

    expect(bucket).toBe("housing_shortage");
    expect(reason).toContain("Goods shortage");
    expect(reason).toContain("housing pressure");
  });
});
