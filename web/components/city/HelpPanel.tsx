"use client";

import { useEffect, useRef, type CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_Z,
  hudActionButton,
  hudModalShell,
} from "@/lib/hud-theme";
import {
  PLAY_BUILD_MODES,
  PLAY_KEYBOARD_SHORTCUTS,
  type PlayKeyboardShortcutEntry,
} from "@/lib/play-keyboard";

const overlayStyle: CSSProperties = {
  position: "fixed",
  inset: 0,
  zIndex: HUD_Z.modal,
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  padding: 16,
  background: HUD_COLORS.overlay,
  pointerEvents: "auto",
};

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
  flexShrink: 0,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 20px",
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 10,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const rowStyle: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "72px 1fr",
  gap: 12,
  alignItems: "baseline",
  padding: "6px 0",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const keyStyle: CSSProperties = {
  fontWeight: 700,
  color: HUD_COLORS.accentHighlight,
  fontSize: 12,
};

const CATEGORY_ORDER: PlayKeyboardShortcutEntry["category"][] = [
  "help",
  "tools",
  "panels",
  "speed",
];

const CATEGORY_LABELS: Record<PlayKeyboardShortcutEntry["category"], string> = {
  help: "Help",
  tools: "Tools & zoning",
  panels: "Panels",
  speed: "Game speed",
};

type HelpPanelProps = {
  open: boolean;
  onClose: () => void;
};

function ShortcutRow({ entry }: { entry: PlayKeyboardShortcutEntry }) {
  return (
    <div style={rowStyle}>
      <span style={keyStyle}>{entry.keys}</span>
      <div>
        <div style={{ fontWeight: 600 }}>{entry.label}</div>
        <div style={{ fontSize: 11, color: HUD_COLORS.textMuted, marginTop: 2 }}>
          {entry.description}
        </div>
      </div>
    </div>
  );
}

export function HelpPanel({ open, onClose }: HelpPanelProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previouslyFocusedRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const panel = panelRef.current;
    panel?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" || event.key === "?") {
        event.preventDefault();
        onClose();
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      previouslyFocusedRef.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  const shortcutsByCategory = CATEGORY_ORDER.map((category) => ({
    category,
    entries: PLAY_KEYBOARD_SHORTCUTS.filter((entry) => entry.category === category),
  })).filter((group) => group.entries.length > 0);

  return (
    <div
      style={overlayStyle}
      role="presentation"
      onClick={onClose}
      data-testid="help-panel-overlay"
    >
      <div
        ref={panelRef}
        style={hudModalShell()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="help-panel-title"
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
        data-testid="help-panel"
      >
        <header style={headerStyle}>
          <div>
            <div
              id="help-panel-title"
              style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}
            >
              Keyboard & build reference
            </div>
            <div style={{ fontSize: 11, color: HUD_COLORS.textMuted, marginTop: 4 }}>
              Press <span style={{ color: HUD_COLORS.accentHighlight }}>?</span> or{" "}
              <span style={{ color: HUD_COLORS.accentHighlight }}>Esc</span> to close
            </div>
          </div>
          <button
            type="button"
            style={{ ...hudActionButton(), padding: "4px 10px", fontSize: 12 }}
            onClick={onClose}
            aria-label="Close help"
          >
            ✕
          </button>
        </header>

        <div style={bodyStyle}>
          {shortcutsByCategory.map(({ category, entries }) => (
            <section key={category} style={{ marginBottom: 20 }}>
              <div style={sectionTitle}>{CATEGORY_LABELS[category]}</div>
              {entries.map((entry) => (
                <ShortcutRow key={`${category}-${entry.keys}-${entry.label}`} entry={entry} />
              ))}
            </section>
          ))}

          <section>
            <div style={sectionTitle}>Build modes</div>
            {PLAY_BUILD_MODES.map((mode) => (
              <div key={mode.id} style={rowStyle}>
                <span style={keyStyle}>{mode.label}</span>
                <div style={{ fontSize: 12, color: HUD_COLORS.textMuted }}>
                  {mode.description}
                </div>
              </div>
            ))}
          </section>
        </div>
      </div>
    </div>
  );
}
