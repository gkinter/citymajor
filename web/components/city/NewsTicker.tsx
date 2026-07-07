"use client";

import { useEffect, useMemo, useState } from "react";
import { narrativeFromSimEvent } from "@/lib/event-catalog";
import { narrativeFromBucket } from "@/lib/narrative-templates";
import { deriveNarrativeBucket } from "@/lib/sim-metrics";
import type { ActiveEventSnapshot, SimResources } from "@/lib/sim-bridge";

const ROTATE_MS = 8_000;
const IDLE_MESSAGE = "City Major Gazette — municipal wires quiet";

type NewsTickerProps = {
  activeEvents?: ActiveEventSnapshot[];
  simResources?: SimResources | null;
};

function narrativeMetricsFromResources(
  resources?: SimResources | null,
): Parameters<typeof deriveNarrativeBucket>[0] {
  return {
    healthcareCoverage: resources?.healthcareCoverage ?? 0.5,
    approval: resources?.approval,
    cityFunds: resources?.cityFunds,
    residentialDemand: resources?.residentialDemand,
    commercialDemand: resources?.commercialDemand,
    industrialDemand: resources?.industrialDemand,
  };
}

/** Herald-template headlines for the bottom ticker — live events first, else metrics bucket. */
export function buildTickerHeadlines(
  activeEvents: ActiveEventSnapshot[] | undefined,
  simResources?: SimResources | null,
): string[] {
  const events = activeEvents ?? [];
  if (events.length) {
    const headlines = events.map(
      (event) => narrativeFromSimEvent(event.typeId).headline,
    );
    return [...new Set(headlines)];
  }

  if (!simResources) {
    return [IDLE_MESSAGE];
  }

  const bucket = deriveNarrativeBucket(
    narrativeMetricsFromResources(simResources),
  );
  return [narrativeFromBucket(bucket).headline];
}

export function NewsTicker({ activeEvents, simResources }: NewsTickerProps) {
  const headlines = useMemo(
    () => buildTickerHeadlines(activeEvents, simResources),
    [activeEvents, simResources],
  );

  const [index, setIndex] = useState(0);

  useEffect(() => {
    setIndex(0);
  }, [headlines]);

  useEffect(() => {
    if (headlines.length <= 1) return;
    const id = window.setInterval(() => {
      setIndex((current) => (current + 1) % headlines.length);
    }, ROTATE_MS);
    return () => window.clearInterval(id);
  }, [headlines]);

  const tickerText = headlines[index] ?? IDLE_MESSAGE;

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
