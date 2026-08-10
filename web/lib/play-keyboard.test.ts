/**
 * @vitest-environment jsdom
 */
import { describe, expect, it, vi } from "vitest";
import { handlePlayKeyboardShortcut } from "@/lib/play-keyboard";

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
