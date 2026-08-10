import { describe, expect, it } from "vitest";
import {
  formatCommuterCoverage,
  formatOdTilePair,
  parseCommuteOdRow,
  parseCommuteOdSample,
  parseCommuterCoverage,
  topCommuteOdPairs,
} from "./commute-od";

describe("parseCommuterCoverage", () => {
  it("accepts finite 0–1 values", () => {
    expect(parseCommuterCoverage(0)).toBe(0);
    expect(parseCommuterCoverage(0.93)).toBe(0.93);
    expect(parseCommuterCoverage(1)).toBe(1);
  });

  it("clamps out-of-range finite numbers", () => {
    expect(parseCommuterCoverage(1.4)).toBe(1);
    expect(parseCommuterCoverage(-0.2)).toBe(0);
  });

  it("rejects non-finite / non-number", () => {
    expect(parseCommuterCoverage(undefined)).toBeUndefined();
    expect(parseCommuterCoverage(null)).toBeUndefined();
    expect(parseCommuterCoverage("0.9")).toBeUndefined();
    expect(parseCommuterCoverage(Number.NaN)).toBeUndefined();
    expect(parseCommuterCoverage(Number.POSITIVE_INFINITY)).toBeUndefined();
  });
});

describe("parseCommuteOdSample", () => {
  it("parses camelCase WASM rows", () => {
    expect(
      parseCommuteOdSample([
        {
          homeTileX: 10,
          homeTileZ: 12,
          workTileX: 40,
          workTileZ: 41,
          tripCount: 7,
        },
      ]),
    ).toEqual([
      {
        homeTileX: 10,
        homeTileZ: 12,
        workTileX: 40,
        workTileZ: 41,
        tripCount: 7,
      },
    ]);
  });

  it("skips malformed rows and returns undefined when none remain", () => {
    expect(parseCommuteOdSample([])).toBeUndefined();
    expect(parseCommuteOdSample(null)).toBeUndefined();
    expect(parseCommuteOdSample("nope")).toBeUndefined();
    expect(
      parseCommuteOdSample([
        { homeTileX: 1, homeTileZ: 2 },
        null,
        42,
      ]),
    ).toBeUndefined();
  });

  it("defaults missing tripCount to 0 and keeps valid siblings", () => {
    expect(
      parseCommuteOdSample([
        {
          homeTileX: 1,
          homeTileZ: 1,
          workTileX: 2,
          workTileZ: 2,
        },
        {
          homeTileX: 3,
          homeTileZ: 3,
          workTileX: 4,
          workTileZ: 4,
          tripCount: 2,
        },
      ]),
    ).toEqual([
      {
        homeTileX: 1,
        homeTileZ: 1,
        workTileX: 2,
        workTileZ: 2,
        tripCount: 0,
      },
      {
        homeTileX: 3,
        homeTileZ: 3,
        workTileX: 4,
        workTileZ: 4,
        tripCount: 2,
      },
    ]);
  });

  it("rejects negative tripCount rows", () => {
    expect(
      parseCommuteOdRow({
        homeTileX: 1,
        homeTileZ: 1,
        workTileX: 2,
        workTileZ: 2,
        tripCount: -1,
      }),
    ).toBeNull();
  });
});

describe("topCommuteOdPairs / formatters", () => {
  it("sorts by tripCount and respects limit", () => {
    const top = topCommuteOdPairs(
      [
        {
          homeTileX: 0,
          homeTileZ: 0,
          workTileX: 1,
          workTileZ: 1,
          tripCount: 2,
        },
        {
          homeTileX: 5,
          homeTileZ: 5,
          workTileX: 6,
          workTileZ: 6,
          tripCount: 9,
        },
        {
          homeTileX: 2,
          homeTileZ: 2,
          workTileX: 3,
          workTileZ: 3,
          tripCount: 4,
        },
      ],
      2,
    );
    expect(top.map((r) => r.tripCount)).toEqual([9, 4]);
  });

  it("formats coverage percent and tile pairs", () => {
    expect(formatCommuterCoverage(0.934)).toBe("93%");
    expect(
      formatOdTilePair({
        homeTileX: 10,
        homeTileZ: 12,
        workTileX: 40,
        workTileZ: 41,
        tripCount: 3,
      }),
    ).toBe("(10,12) → (40,41)");
  });
});
