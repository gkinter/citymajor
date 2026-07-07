"use client";

import { hudActionButton } from "@/lib/hud-theme";

type EconomyButtonProps = {
  title: string;
  onClick: () => void;
  active?: boolean;
};

export function EconomyButton({ title, onClick, active }: EconomyButtonProps) {
  return (
    <button
      type="button"
      className="hud-economy-button"
      data-onboarding-target="economy"
      style={{
        ...hudActionButton(),
        border: active
          ? "1px solid rgba(94, 200, 255, 0.7)"
          : "1px solid rgba(120, 160, 220, 0.25)",
        background: active ? "rgba(94, 200, 255, 0.15)" : "rgba(16, 22, 38, 0.6)",
      }}
      title={title}
      onClick={onClick}
    >
      Economy
    </button>
  );
}
