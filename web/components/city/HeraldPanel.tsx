"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_RADIUS,
  hudActionButton,
  hudSlidePanel,
} from "@/lib/hud-theme";
import type { NarrativeEventResponse } from "@/lib/narrative-templates";

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 24px",
};

const optionStyle: CSSProperties = {
  marginTop: 12,
  padding: "10px 12px",
  borderRadius: HUD_RADIUS.sm,
  border: `1px solid ${HUD_COLORS.borderSubtle}`,
  background: HUD_COLORS.rowBg,
};

function formatQuota(remaining: number | undefined): string {
  if (remaining === undefined) return "…";
  if (remaining === Number.MAX_SAFE_INTEGER) return "∞";
  return String(remaining);
}

type HeraldPanelProps = {
  open: boolean;
  onClose: () => void;
  event: NarrativeEventResponse | null;
  loading: boolean;
  error: string | null;
  quotaRemaining?: number;
};

export function HeraldPanel({
  open,
  onClose,
  event,
  loading,
  error,
  quotaRemaining,
}: HeraldPanelProps) {
  if (!open) return null;

  return (
    <aside style={hudSlidePanel()} role="dialog" aria-label="Daily Herald" aria-modal="true">
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}>
            The Daily Herald
          </div>
          <div style={{ opacity: 0.65, fontSize: 11, marginTop: 2 }}>
            narrative remaining today: {formatQuota(quotaRemaining)}
          </div>
        </div>
        <button
          type="button"
          style={{ ...hudActionButton(), padding: "4px 10px", fontSize: 12 }}
          onClick={onClose}
          aria-label="Close"
        >
          ✕
        </button>
      </header>

      <div style={bodyStyle}>
        {loading ? (
          <div style={{ opacity: 0.75 }}>Fetching today&apos;s lead story…</div>
        ) : error ? (
          <div style={{ color: HUD_COLORS.error }}>{error}</div>
        ) : event ? (
          <>
            <div
              style={{
                fontSize: 11,
                textTransform: "uppercase",
                letterSpacing: "0.08em",
                opacity: 0.55,
                marginBottom: 8,
              }}
            >
              {event.bucket.replace(/_/g, " ")} · {event.source}
            </div>
            <h2 style={{ margin: "0 0 12px", fontSize: 18, fontWeight: 700, lineHeight: 1.35 }}>
              {event.headline}
            </h2>
            <p style={{ margin: 0, opacity: 0.92 }}>{event.body}</p>
            {event.options.length > 0 ? (
              <div style={{ marginTop: 20 }}>
                <div style={{ fontSize: 11, opacity: 0.6, marginBottom: 8 }}>
                  Council options
                </div>
                {event.options.map((opt) => (
                  <div key={opt.id} style={optionStyle}>
                    <div style={{ fontWeight: 600 }}>{opt.label}</div>
                    <div style={{ fontSize: 11, opacity: 0.7, marginTop: 4 }}>{opt.tradeoff}</div>
                  </div>
                ))}
              </div>
            ) : null}
          </>
        ) : (
          <div style={{ opacity: 0.65 }}>No story loaded.</div>
        )}
      </div>
    </aside>
  );
}
