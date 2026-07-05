import type { ServiceViewMode } from "@/lib/sim-bridge";

/** Playable service coverage modes in the /play toolbar (excludes education stub). */
export type ServiceCoverageMode = Exclude<ServiceViewMode, "off" | "education">;

export type ServiceModeDefinition = {
  id: ServiceCoverageMode;
  label: string;
  shortLabel: string;
  /** Full-coverage swatch — matches ServiceCoverageOverlay hue anchor. */
  overlayColor: string;
  /** Hue degrees for 3D heatmap — keep in sync with ServiceCoverageOverlay. */
  hue: number;
};

export const SERVICE_COVERAGE_MODES: readonly ServiceModeDefinition[] = [
  {
    id: "health",
    label: "Health",
    shortLabel: "Med",
    overlayColor: "#4cd964",
    hue: 130,
  },
  {
    id: "police",
    label: "Police",
    shortLabel: "PD",
    overlayColor: "#5dade2",
    hue: 220,
  },
  {
    id: "fire",
    label: "Fire",
    shortLabel: "FD",
    overlayColor: "#f39c12",
    hue: 28,
  },
] as const;

/** SimCity-style heat scale: red (none) → amber → green (full). */
export const COVERAGE_HEAT_LEGEND = [
  { label: "Low", color: "#c0392b" },
  { label: "Mid", color: "#f1c40f" },
  { label: "Full", color: "#2ecc71" },
] as const;

export function serviceModeById(
  id: ServiceCoverageMode,
): ServiceModeDefinition {
  const mode = SERVICE_COVERAGE_MODES.find((m) => m.id === id);
  if (!mode) throw new Error(`Unknown service mode: ${id}`);
  return mode;
}

/** Hue anchor per service type — shared by toolbar swatches and 3D overlay. */
export function hueForServiceMode(mode: ServiceViewMode): number {
  const match = SERVICE_COVERAGE_MODES.find((m) => m.id === mode);
  if (match) return match.hue;
  if (mode === "education") return 270;
  return 0;
}
