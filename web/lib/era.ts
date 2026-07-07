/**
 * Visual era labels + HUD badge colors (SB-3692).
 * Aligns with packages/sim-types Era enum (Frontier → Future).
 */

export const HUD_ERA_NAMES = [
  "Frontier",
  "Industrial",
  "Postwar",
  "Modern",
  "Future",
] as const;

/** Badge accent per era — wallA from residential-low palette. */
export const HUD_ERA_BADGE_COLORS: Record<number, { text: string; bg: string; border: string }> = {
  0: { text: "#E8D5C4", bg: "rgba(141, 110, 99, 0.28)", border: "rgba(141, 110, 99, 0.55)" },
  1: { text: "#FFCDD2", bg: "rgba(183, 28, 28, 0.28)", border: "rgba(198, 40, 40, 0.55)" },
  2: { text: "#ECEFF1", bg: "rgba(120, 144, 156, 0.28)", border: "rgba(144, 164, 174, 0.55)" },
  3: { text: "#BBDEFB", bg: "rgba(21, 101, 192, 0.28)", border: "rgba(66, 165, 245, 0.55)" },
  4: { text: "#E1BEE7", bg: "rgba(123, 31, 162, 0.28)", border: "rgba(171, 71, 188, 0.55)" },
};

const FALLBACK_BADGE = {
  text: "#e8eef8",
  bg: "rgba(120, 160, 220, 0.15)",
  border: "rgba(120, 160, 220, 0.35)",
};

export function hudEraName(era: number): string {
  return HUD_ERA_NAMES[era] ?? "Unknown";
}

export function hudEraBadgeStyle(era: number): {
  color: string;
  background: string;
  border: string;
} {
  const palette = HUD_ERA_BADGE_COLORS[era] ?? FALLBACK_BADGE;
  return {
    color: palette.text,
    background: palette.bg,
    border: `1px solid ${palette.border}`,
  };
}
