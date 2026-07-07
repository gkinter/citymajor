"use client";

import { useEffect } from "react";
import { ERA_TRANSITION_FANFARE_MS } from "@/lib/constants";
import { eraTransitionModalCopy } from "@/lib/era-narrative";
import { hudEraName } from "@/lib/era";

type EraTransitionModalProps = {
  era: number | null;
  onDismiss: () => void;
};

export function EraTransitionModal({ era, onDismiss }: EraTransitionModalProps) {
  useEffect(() => {
    if (era === null) return;
    const timer = window.setTimeout(onDismiss, ERA_TRANSITION_FANFARE_MS);
    return () => window.clearTimeout(timer);
  }, [era, onDismiss]);

  if (era === null) return null;

  const copy = eraTransitionModalCopy(era);

  return (
    <div
      className="hud-era"
      role="dialog"
      aria-modal="true"
      aria-labelledby="era-transition-title"
      onClick={onDismiss}
    >
      <div className="hud-era__card" onClick={(e) => e.stopPropagation()}>
        <p className="hud-era__eyebrow">{copy.eyebrow}</p>
        <h2 id="era-transition-title" className="hud-era__title">
          {copy.title}
        </h2>
        <p className="hud-era__body">{copy.body}</p>
        <div className="hud-era__badge" data-era={era}>
          {hudEraName(era)}
        </div>
      </div>
    </div>
  );
}
