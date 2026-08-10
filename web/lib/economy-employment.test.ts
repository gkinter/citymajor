import { describe, expect, it } from "vitest";
import { formatEmploymentShare } from "./economy-employment";

describe("formatEmploymentShare", () => {
  it("formats mid-range unemployment as a whole percent", () => {
    expect(formatEmploymentShare(0.124)).toBe("12%");
  });

  it("clamps below 0 and above 1", () => {
    expect(formatEmploymentShare(-0.2)).toBe("0%");
    expect(formatEmploymentShare(1.5)).toBe("100%");
  });
});
