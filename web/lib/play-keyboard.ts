import type { ZoningTool } from "@/lib/zoning";
import {
  isZoneTierUnlocked,
  ZONE_TIERS,
  type ZoneTier,
} from "@/lib/zone-tiers";

/** Active build mode in /play — mirrors PlayClient state. */
export type PlayBuildMode = "zone" | "road" | "plop" | "education";

export type PlayKeyboardShortcutEntry = {
  keys: string;
  label: string;
  description: string;
  category: "tools" | "panels" | "speed" | "help";
};

export type PlayBuildModeEntry = {
  id: PlayBuildMode;
  label: string;
  description: string;
};

/** Keyboard shortcuts shown in HelpPanel — keep in sync with handlePlayKeyboardShortcut. */
export const PLAY_KEYBOARD_SHORTCUTS: PlayKeyboardShortcutEntry[] = [
  {
    keys: "?",
    label: "Help",
    description: "Toggle keyboard shortcuts and build mode reference",
    category: "help",
  },
  {
    keys: "B",
    label: "Build menu",
    description: "Open or close the build catalog",
    category: "tools",
  },
  {
    keys: "Z",
    label: "Zone mode",
    description: "Return to zoning paint mode",
    category: "tools",
  },
  {
    keys: "R",
    label: "Road",
    description: "Select the road paint tool",
    category: "tools",
  },
  ...ZONE_TIERS.map((tier, index) => ({
    keys: String(index + 1),
    label: tier.shortLabel,
    description: `Select ${tier.label} zone tier`,
    category: "tools" as const,
  })),
  {
    keys: "Esc",
    label: "Close panels",
    description: "Dismiss open slide-out panels and modals",
    category: "panels",
  },
];

/** Build modes referenced by HelpPanel. */
export const PLAY_BUILD_MODES: PlayBuildModeEntry[] = [
  {
    id: "zone",
    label: "Zone",
    description: "Paint residential, commercial, industrial, and specialty districts",
  },
  {
    id: "road",
    label: "Road",
    description: "Lay road tiles; pick tier from the road toolbar (local, avenue, highway)",
  },
  {
    id: "plop",
    label: "Plop",
    description: "Place individual civic and service buildings from the build catalog",
  },
  {
    id: "education",
    label: "Education",
    description: "Place schools and colleges; shows education coverage overlay",
  },
];

/** True when the event target is an editable field — skip game hotkeys. */
export function isEditableKeyboardTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return true;
  return target.isContentEditable;
}

/** Map digit keys 1–7 to zone tiers in toolbar order. */
export function zoneTierForDigitKey(key: string): ZoneTier | undefined {
  const digit = Number(key);
  if (!Number.isInteger(digit) || digit < 1 || digit > ZONE_TIERS.length) {
    return undefined;
  }
  return ZONE_TIERS[digit - 1];
}

export type PlayKeyboardHandlers = {
  onToggleHelp?: () => void;
  onToggleBuildMenu: () => void;
  onEnterZoneMode: () => void;
  onSelectRoad: () => void;
  onSelectZoneTool: (tool: ZoningTool) => void;
  unlockedTechIds?: number[];
};

/**
 * Handle /play HUD keyboard shortcuts.
 * Returns true when a shortcut was consumed.
 */
export function handlePlayKeyboardShortcut(
  event: KeyboardEvent,
  handlers: PlayKeyboardHandlers,
): boolean {
  if (event.defaultPrevented) return false;
  if (event.ctrlKey || event.metaKey || event.altKey) return false;
  if (isEditableKeyboardTarget(event.target)) return false;

  if (event.key === "?") {
    if (handlers.onToggleHelp) {
      event.preventDefault();
      handlers.onToggleHelp();
      return true;
    }
    return false;
  }

  const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;

  if (key === "b") {
    event.preventDefault();
    handlers.onToggleBuildMenu();
    return true;
  }

  if (key === "z") {
    event.preventDefault();
    handlers.onEnterZoneMode();
    return true;
  }

  if (key === "r") {
    event.preventDefault();
    handlers.onSelectRoad();
    return true;
  }

  const tier = zoneTierForDigitKey(event.key);
  if (tier) {
    const unlocked = isZoneTierUnlocked(tier, handlers.unlockedTechIds);
    if (!unlocked) return false;
    event.preventDefault();
    handlers.onSelectZoneTool(tier.tool);
    return true;
  }

  return false;
}
