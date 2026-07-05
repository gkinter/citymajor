"use client";

import type { ReactNode } from "react";
import { HUD_TOAST_STACK_GAP, HUD_ZONE } from "@/lib/hud-theme";

type HudToastStackProps = {
  children: ReactNode;
};

/** Vertical toast column for /play — research unlock + cash crisis, etc. */
export function HudToastStack({ children }: HudToastStackProps) {
  return (
    <div
      data-testid="hud-toast-stack"
      style={{
        ...HUD_ZONE.playToastStack,
        display: "flex",
        flexDirection: "column",
        gap: HUD_TOAST_STACK_GAP,
        alignItems: "flex-start",
        pointerEvents: "none",
      }}
    >
      {children}
    </div>
  );
}
