"use client";

import { useEffect, useRef, type CSSProperties } from "react";
import {
  HUD_COLORS,
  hudActionButton,
  hudPanelStatusBox,
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

function HeraldLoadingState() {
  return (
    <div
      className="hud-herald-status hud-herald-status--loading"
      style={hudPanelStatusBox("loading")}
      aria-live="polite"
      aria-label="Loading Herald story"
    >
      <p className="hud-herald-status__eyebrow">Press room</p>
      <p className="hud-herald-status__message">Fetching today&apos;s lead story…</p>
      <div className="hud-herald-skeleton" aria-hidden>
        <div className="hud-herald-skeleton__line hud-herald-skeleton__line--short" />
        <div className="hud-herald-skeleton__line hud-herald-skeleton__line--headline" />
        <div className="hud-herald-skeleton__line hud-herald-skeleton__line--medium" />
        <div className="hud-herald-skeleton__line" />
      </div>
    </div>
  );
}

function HeraldErrorState({ message }: { message: string }) {
  return (
    <div
      className="hud-herald-status hud-herald-status--error"
      style={hudPanelStatusBox("error")}
      role="alert"
    >
      <p className="hud-herald-status__eyebrow">Transmission failed</p>
      <p className="hud-herald-status__message">{message}</p>
    </div>
  );
}

function HeraldEmptyState() {
  return (
    <div
      className="hud-herald-status"
      style={hudPanelStatusBox("neutral")}
    >
      <p className="hud-herald-status__eyebrow">No edition</p>
      <p className="hud-herald-status__message">
        No story loaded — open Herald when a city event fires or quota allows a
        fresh headline.
      </p>
    </div>
  );
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
      className="hud-herald-panel"
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
          <HeraldLoadingState />
        ) : error ? (
          <HeraldErrorState message={error} />
        ) : event ? (
          <>
            <div
              className="hud-herald-bucket"
              title={bucketReason ?? undefined}
            >
              {event.bucket.replace(/_/g, " ")} · {event.source}
            </div>
            {bucketReason ? (
              <p className="hud-herald-driver" title={bucketReason}>
                Story driver: {bucketReason}
              </p>
            ) : null}
            <h2 id="herald-event-headline" className="hud-herald-headline">
              {event.headline}
            </h2>
            <p className="hud-herald-body">{event.body}</p>
            {event.options.length > 0 ? (
              <div style={{ marginTop: 20 }}>
                <div className="hud-herald-options__heading">
                  Council options
                </div>
                {event.options.map((opt) => (
                  <button
                    key={opt.id}
                    type="button"
                    className="hud-herald-option"
                    onClick={() => onOptionSelect?.(opt.id)}
                    disabled={!onOptionSelect}
                    aria-label={`Council option: ${opt.label}. ${opt.tradeoff}`}
                  >
                    <div className="hud-herald-option__label">{opt.label}</div>
                    <div className="hud-herald-option__tradeoff">{opt.tradeoff}</div>
                  </button>
                ))}
              </div>
            ) : null}
          </>
        ) : (
          <HeraldEmptyState />
        )}
      </div>
    </aside>
  );
}
