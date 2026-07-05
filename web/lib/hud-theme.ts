import type { CSSProperties } from "react";

/** Shared HUD design tokens — cyan glass, CS-lite mid-fidelity. */
export const HUD_COLORS = {
  text: "#e8eef8",
  textMuted: "rgba(232, 238, 248, 0.65)",
  textDim: "rgba(232, 238, 248, 0.45)",
  textDisabled: "rgba(232, 238, 248, 0.35)",
  accent: "#5ec8ff",
  accentHighlight: "#9fd4ff",
  accentSoft: "rgba(94, 200, 255, 0.15)",
  accentBorder: "rgba(94, 200, 255, 0.7)",
  accentRing: "rgba(94, 200, 255, 0.35)",
  border: "rgba(120, 160, 220, 0.25)",
  borderSubtle: "rgba(120, 160, 220, 0.18)",
  borderStrong: "rgba(120, 160, 220, 0.3)",
  panelBg: "rgba(8, 12, 24, 0.82)",
  panelBgSolid: "rgba(8, 12, 24, 0.96)",
  panelBgSoft: "rgba(8, 12, 24, 0.65)",
  buttonBg: "rgba(16, 22, 38, 0.6)",
  rowBg: "rgba(16, 22, 38, 0.45)",
  overlay: "rgba(4, 8, 16, 0.72)",
  error: "#ff9aa8",
  toast: "#c8e8ff",
} as const;

export const HUD_FONT = {
  mono: "ui-monospace, SFMono-Regular, Menlo, monospace",
  sans: 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
} as const;

export const HUD_RADIUS = { sm: 6, md: 8, lg: 10 } as const;

export const HUD_Z = {
  base: 10,
  controls: 15,
  panel: 20,
  /** Slide-out panels (Research, Economy, Herald) — above topRightActions (panel+1). */
  slidePanel: 25,
  modal: 40,
} as const;

/** Screen zones — positioned for 1280×720 without overlap. */
export const HUD_ZONE = {
  topLeft: {
    position: "absolute",
    top: 8,
    left: 12,
    zIndex: HUD_Z.controls,
  },
  topCenter: {
    position: "absolute",
    top: 10,
    left: "50%",
    transform: "translateX(-50%)",
    zIndex: HUD_Z.base,
    pointerEvents: "none",
  },
  topRight: {
    position: "absolute",
    top: 8,
    right: 12,
    zIndex: HUD_Z.controls,
  },
  /** Top-right interactive cluster — above Era Quest panel and decorative wordmark. */
  topRightActions: {
    position: "absolute",
    top: 8,
    right: 12,
    zIndex: HUD_Z.panel + 1,
    pointerEvents: "auto",
  },
  resources: {
    position: "absolute",
    top: 42,
    left: "50%",
    transform: "translateX(-50%)",
    zIndex: HUD_Z.base + 2,
  },
  leftStack: {
    position: "absolute",
    top: 50,
    left: 12,
    zIndex: HUD_Z.controls,
  },
  toast: {
    position: "absolute",
    top: 94,
    left: 12,
    zIndex: HUD_Z.controls,
  },
  bottomLeft: {
    position: "absolute",
    bottom: 12,
    left: 12,
    zIndex: HUD_Z.base,
  },
  bottomCenter: {
    position: "absolute",
    bottom: 46,
    left: "50%",
    transform: "translateX(-50%)",
    zIndex: HUD_Z.base,
  },
  bottomRight: {
    position: "absolute",
    bottom: 12,
    right: 12,
    zIndex: HUD_Z.base,
  },
} as const satisfies Record<string, CSSProperties>;

export function hudPanel(extra?: CSSProperties): CSSProperties {
  return {
    fontFamily: HUD_FONT.mono,
    fontSize: 12,
    lineHeight: 1.45,
    color: HUD_COLORS.text,
    background: HUD_COLORS.panelBg,
    border: `1px solid ${HUD_COLORS.border}`,
    borderRadius: HUD_RADIUS.md,
    ...extra,
  };
}

export function hudToolbar(extra?: CSSProperties): CSSProperties {
  return hudPanel({
    display: "flex",
    gap: 6,
    padding: "8px 10px",
    pointerEvents: "auto",
    ...extra,
  });
}

export function hudButton(active: boolean, disabled?: boolean): CSSProperties {
  return {
    padding: "6px 12px",
    border: active
      ? `1px solid ${HUD_COLORS.accentBorder}`
      : `1px solid ${HUD_COLORS.borderSubtle}`,
    borderRadius: HUD_RADIUS.sm,
    background: active ? HUD_COLORS.accentSoft : HUD_COLORS.buttonBg,
    color: disabled ? HUD_COLORS.textDisabled : HUD_COLORS.text,
    cursor: disabled ? "not-allowed" : "pointer",
    fontWeight: active ? 700 : 500,
    fontFamily: HUD_FONT.mono,
    fontSize: 12,
    opacity: disabled ? 0.55 : 1,
  };
}

export function hudActionButton(disabled?: boolean): CSSProperties {
  return {
    padding: "8px 14px",
    fontFamily: HUD_FONT.mono,
    fontSize: 12,
    fontWeight: 600,
    color: HUD_COLORS.text,
    background: HUD_COLORS.panelBg,
    border: `1px solid ${HUD_COLORS.border}`,
    borderRadius: HUD_RADIUS.md,
    cursor: disabled ? "not-allowed" : "pointer",
    opacity: disabled ? 0.55 : 1,
  };
}

export function hudBadge(): CSSProperties {
  return {
    padding: "2px 6px",
    borderRadius: 4,
    background: HUD_COLORS.accentSoft,
    border: `1px solid ${HUD_COLORS.accentRing}`,
    fontSize: 11,
    fontWeight: 500,
  };
}

export function hudLabel(): CSSProperties {
  return { opacity: 0.65, marginRight: 6 };
}

/** Era badge chrome — pair with hudEraBadgeStyle(era) from @/lib/era. */
export function hudEraBadgeChrome(extra?: CSSProperties): CSSProperties {
  return {
    display: "inline-flex",
    alignItems: "center",
    padding: "2px 9px",
    borderRadius: 4,
    fontWeight: 700,
    fontSize: 12,
    letterSpacing: "0.03em",
    lineHeight: 1.3,
    ...extra,
  };
}

/** Header row for era panels — label left, badge right. */
export function hudEraHeaderRow(extra?: CSSProperties): CSSProperties {
  return {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 8,
    marginBottom: 8,
    ...extra,
  };
}

/** Compact progress track for era / quest meters. */
export function hudProgressTrack(extra?: CSSProperties): CSSProperties {
  return {
    height: 5,
    borderRadius: 3,
    background: HUD_COLORS.rowBg,
    overflow: "hidden",
    ...extra,
  };
}

export function hudProgressFill(percent: number, extra?: CSSProperties): CSSProperties {
  return {
    height: "100%",
    width: `${Math.min(100, Math.max(0, percent))}%`,
    background: `linear-gradient(90deg, ${HUD_COLORS.accent}, ${HUD_COLORS.accentHighlight})`,
    transition: "width 0.25s ease",
    ...extra,
  };
}

export function hudInfoPanel(extra?: CSSProperties): CSSProperties {
  return hudPanel({
    padding: "10px 14px",
    pointerEvents: "none",
    minWidth: 200,
    ...extra,
  });
}

export function hudModalShell(): CSSProperties {
  return {
    width: "min(480px, 100%)",
    maxHeight: "min(70vh, 560px)",
    display: "flex",
    flexDirection: "column",
    fontFamily: HUD_FONT.mono,
    fontSize: 13,
    color: HUD_COLORS.text,
    background: HUD_COLORS.panelBgSolid,
    border: `1px solid ${HUD_COLORS.borderStrong}`,
    borderRadius: HUD_RADIUS.lg,
    boxShadow: "0 16px 48px rgba(0, 0, 0, 0.5)",
  };
}

export function hudSlidePanel(): CSSProperties {
  return {
    position: "absolute",
    top: 0,
    right: 0,
    width: "min(420px, 92vw)",
    height: "100%",
    zIndex: HUD_Z.slidePanel,
    display: "flex",
    flexDirection: "column",
    fontFamily: HUD_FONT.mono,
    fontSize: 13,
    lineHeight: 1.55,
    color: HUD_COLORS.text,
    background: "rgba(8, 12, 24, 0.92)",
    borderLeft: `1px solid ${HUD_COLORS.borderStrong}`,
    boxShadow: "-8px 0 32px rgba(0, 0, 0, 0.45)",
    pointerEvents: "auto",
  };
}
