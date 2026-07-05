import type { ZoningTool } from "@/lib/zoning";
import {
  isZoneTierUnlocked,
  ZONE_TIERS,
  type ZoneTier,
} from "@/lib/zone-tiers";

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
