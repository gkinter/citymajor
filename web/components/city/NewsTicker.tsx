"use client";

import { useMemo } from "react";
import { getEventDefinition } from "@/lib/event-catalog";
import type { ActiveEventSnapshot } from "@/lib/sim-bridge";

const IDLE_MESSAGE = "City Major Gazette — municipal wires quiet";

type NewsTickerProps = {
  activeEvents?: ActiveEventSnapshot[];
};

function eventHeadline(event: ActiveEventSnapshot): string {
  const name = getEventDefinition(event.typeId)?.name ?? event.typeId;
  if (event.phase === "active") return name;
  return `${name} (${event.phase})`;
}

export function NewsTicker({ activeEvents }: NewsTickerProps) {
  const tickerText = useMemo(() => {
    const events = activeEvents ?? [];
    if (!events.length) return IDLE_MESSAGE;
    return events.map(eventHeadline).join("   ◆   ");
  }, [activeEvents]);

  return (
    <div className="hud-ticker" aria-live="polite" aria-label="City news ticker" data-onboarding-target="news-ticker">
      <div className="hud-ticker__label" aria-hidden>
        News
      </div>
      <div className="hud-ticker__viewport">
        <div className="hud-ticker__track">
          <span className="hud-ticker__text">{tickerText}</span>
          <span className="hud-ticker__text" aria-hidden>
            {tickerText}
          </span>
        </div>
      </div>
    </div>
  );
}
