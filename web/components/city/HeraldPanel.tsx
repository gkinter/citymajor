"use client";

import { useEffect, useRef, type CSSProperties } from "react";
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

const optionButtonStyle: CSSProperties = {
  ...optionStyle,
  width: "100%",
  textAlign: "left",
  cursor: "pointer",
  font: "inherit",
  color: "inherit",
};

const FOCUSABLE_SELECTOR =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusableElements(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
}

function trapTabKey(event: KeyboardEvent, container: HTMLElement): void {
  const focusable = getFocusableElements(container);
  if (focusable.length === 0) {
    event.preventDefault();
    return;
  }
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

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
  specialEdition?: boolean;
  /** Why this Herald bucket was chosen — links live sim metrics to the story. */
  bucketReason?: string | null;
  onOptionSelect?: (optionId: string) => void;
};

export function HeraldPanel({
  open,
  onClose,
  event,
  loading,
  error,
  quotaRemaining,
  specialEdition = false,
  bucketReason,
  onOptionSelect,
}: HeraldPanelProps) {
  const panelRef = useRef<HTMLElement>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previouslyFocusedRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const panel = panelRef.current;
    if (panel) {
      const focusable = getFocusableElements(panel);
      (focusable[0] ?? panel).focus();
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }
      if (event.key === "Tab" && panel) trapTabKey(event, panel);
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      previouslyFocusedRef.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <aside
      ref={panelRef}
      style={hudSlidePanel()}
      role="dialog"
      aria-labelledby="herald-panel-title"
      aria-describedby="herald-panel-subtitle"
      aria-modal="true"
      aria-busy={loading}
      tabIndex={-1}
    >
      <header style={headerStyle}>
        <div>
          <div
            id="herald-panel-title"
            style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}
          >
            {specialEdition ? "The Daily Herald — Special Edition" : "The Daily Herald"}
          </div>
          <div
            id="herald-panel-subtitle"
            style={{ opacity: 0.65, fontSize: 11, marginTop: 2 }}
          >
            {specialEdition
              ? "population and tech gates cleared — commemorative issue"
              : `narrative remaining today: ${formatQuota(quotaRemaining)}`}
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
          <div style={{ opacity: 0.75 }} aria-live="polite">
            Fetching today&apos;s lead story…
          </div>
        ) : error ? (
          <div style={{ color: HUD_COLORS.error }} role="alert">
            {error}
          </div>
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
              title={bucketReason ?? undefined}
            >
              {event.bucket.replace(/_/g, " ")} · {event.source}
            </div>
            {bucketReason ? (
              <p
                style={{
                  margin: "0 0 10px",
                  fontSize: 11,
                  lineHeight: 1.45,
                  opacity: 0.72,
                  color: HUD_COLORS.accentHighlight,
                }}
                title={bucketReason}
              >
                Story driver: {bucketReason}
              </p>
            ) : null}
            <h2
              id="herald-event-headline"
              style={{ margin: "0 0 12px", fontSize: 18, fontWeight: 700, lineHeight: 1.35 }}
            >
              {event.headline}
            </h2>
            <p style={{ margin: 0, opacity: 0.92 }}>{event.body}</p>
            {event.options.length > 0 ? (
              <div style={{ marginTop: 20 }}>
                <div style={{ fontSize: 11, opacity: 0.6, marginBottom: 8 }}>
                  Council options
                </div>
                {event.options.map((opt) => (
                  <button
                    key={opt.id}
                    type="button"
                    style={optionButtonStyle}
                    onClick={() => onOptionSelect?.(opt.id)}
                    disabled={!onOptionSelect}
                    aria-label={`Council option: ${opt.label}. ${opt.tradeoff}`}
                  >
                    <div style={{ fontWeight: 600 }}>{opt.label}</div>
                    <div style={{ fontSize: 11, opacity: 0.7, marginTop: 4 }}>
                      {opt.tradeoff}
                    </div>
                  </button>
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
