/**
 * @vitest-environment jsdom
 */
import { describe, expect, it, vi } from "vitest";
import {
  handlePlayKeyboardShortcut,
  isPanelToggleShortcutKey,
} from "@/lib/play-keyboard";

function keyEvent(key: string, target?: EventTarget): KeyboardEvent {
  return {
    key,
    defaultPrevented: false,
    ctrlKey: false,
    metaKey: false,
    altKey: false,
    target: target ?? document.body,
    preventDefault: vi.fn(),
  } as unknown as KeyboardEvent;
}

describe("isPanelToggleShortcutKey", () => {
  it("treats E / ? as panel toggles that must work while panels are open", () => {
    expect(isPanelToggleShortcutKey({ key: "e" })).toBe(true);
    expect(isPanelToggleShortcutKey({ key: "E" })).toBe(true);
    expect(isPanelToggleShortcutKey({ key: "?" })).toBe(true);
  });

  it("does not treat tool hotkeys as panel toggles", () => {
    expect(isPanelToggleShortcutKey({ key: "b" })).toBe(false);
    expect(isPanelToggleShortcutKey({ key: "z" })).toBe(false);
    expect(isPanelToggleShortcutKey({ key: "r" })).toBe(false);
    expect(isPanelToggleShortcutKey({ key: "Escape" })).toBe(false);
  });
});

describe("handlePlayKeyboardShortcut", () => {
  it("toggles economy panel on E when handler is wired", () => {
    const onToggleEconomy = vi.fn();
    const event = keyEvent("e");

    const consumed = handlePlayKeyboardShortcut(event, {
      onToggleBuildMenu: vi.fn(),
      onEnterZoneMode: vi.fn(),
      onSelectRoad: vi.fn(),
      onToggleEconomy,
    });

    expect(consumed).toBe(true);
    expect(onToggleEconomy).toHaveBeenCalledOnce();
    expect(event.preventDefault).toHaveBeenCalled();
  });

  it("invokes economy toggle on each E press (open then close)", () => {
    const onToggleEconomy = vi.fn();
    const handlers = {
      onToggleBuildMenu: vi.fn(),
      onEnterZoneMode: vi.fn(),
      onSelectRoad: vi.fn(),
      onToggleEconomy,
    };

    expect(handlePlayKeyboardShortcut(keyEvent("e"), handlers)).toBe(true);
    expect(handlePlayKeyboardShortcut(keyEvent("E"), handlers)).toBe(true);
    expect(onToggleEconomy).toHaveBeenCalledTimes(2);
  });

  it("ignores E when economy handler is not provided", () => {
    const event = keyEvent("e");

    const consumed = handlePlayKeyboardShortcut(event, {
      onToggleBuildMenu: vi.fn(),
      onEnterZoneMode: vi.fn(),
      onSelectRoad: vi.fn(),
    });

    expect(consumed).toBe(false);
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it("skips shortcuts when focus is in an input", () => {
    const onToggleEconomy = vi.fn();
    const input = document.createElement("input");
    const event = keyEvent("e", input);

    const consumed = handlePlayKeyboardShortcut(event, {
      onToggleBuildMenu: vi.fn(),
      onEnterZoneMode: vi.fn(),
      onSelectRoad: vi.fn(),
      onToggleEconomy,
    });

    expect(consumed).toBe(false);
    expect(onToggleEconomy).not.toHaveBeenCalled();
  });
});
