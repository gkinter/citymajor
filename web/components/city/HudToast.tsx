"use client";

import { useEffect } from "react";
import type { CSSProperties } from "react";
import { HUD_COLORS, HUD_ZONE, hudPanel } from "@/lib/hud-theme";

type HudToastProps = {
  message: string | null;
  /** Auto-dismiss after ms (default 4s). */
  durationMs?: number;
  onDismiss?: () => void;
  style?: CSSProperties;
};

export function HudToast({
  message,
  durationMs = 4_000,
  onDismiss,
  style,
}: HudToastProps) {
  useEffect(() => {
    if (!message || !onDismiss) return;
    const id = window.setTimeout(onDismiss, durationMs);
    return () => window.clearTimeout(id);
  }, [message, durationMs, onDismiss]);

  if (!message) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      style={{
        ...HUD_ZONE.toast,
        ...hudPanel({
          padding: "8px 12px",
          fontSize: 12,
          color: HUD_COLORS.toast,
          pointerEvents: "none",
          maxWidth: 320,
          lineHeight: 1.4,
        }),
        ...style,
      }}
    >
      {message}
    </div>
  );
}
