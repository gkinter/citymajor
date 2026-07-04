"use client";

import type { CSSProperties } from "react";
import type { NarrativeEventResponse } from "@/lib/narrative-templates";

const panelShell: CSSProperties = {
  position: "absolute",
  top: 0,
  right: 0,
  width: "min(420px, 92vw)",
  height: "100%",
  zIndex: 20,
  display: "flex",
  flexDirection: "column",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 13,
  lineHeight: 1.55,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.92)",
  borderLeft: "1px solid rgba(120, 160, 220, 0.3)",
  boxShadow: "-8px 0 32px rgba(0, 0, 0, 0.45)",
  pointerEvents: "auto",
};

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: "1px solid rgba(120, 160, 220, 0.2)",
};

const closeButtonStyle: CSSProperties = {
  padding: "4px 10px",
  border: "1px solid rgba(120, 160, 220, 0.25)",
  borderRadius: 6,
  background: "rgba(16, 22, 38, 0.6)",
  color: "#e8eef8",
  cursor: "pointer",
  fontSize: 12,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 24px",
};

const optionStyle: CSSProperties = {
  marginTop: 12,
  padding: "10px 12px",
  borderRadius: 6,
  border: "1px solid rgba(120, 160, 220, 0.18)",
  background: "rgba(16, 22, 38, 0.45)",
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
    <aside style={panelShell} role="dialog" aria-label="Daily Herald" aria-modal="true">
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}>
            The Daily Herald
          </div>
          <div style={{ opacity: 0.65, fontSize: 11, marginTop: 2 }}>
            narrative remaining today: {formatQuota(quotaRemaining)}
          </div>
        </div>
        <button type="button" style={closeButtonStyle} onClick={onClose} aria-label="Close">
          ✕
        </button>
      </header>

      <div style={bodyStyle}>
        {loading ? (
          <div style={{ opacity: 0.75 }}>Fetching today&apos;s lead story…</div>
        ) : error ? (
          <div style={{ color: "#ff9aa8" }}>{error}</div>
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
