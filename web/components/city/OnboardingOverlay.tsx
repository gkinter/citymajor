"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ONBOARDING_STORAGE_KEY } from "@/lib/constants";
import { ENGINE_ZONE_TYPE } from "@/lib/zoning";

const STEP_COUNT = 5;

type OnboardingStep = {
  id: string;
  title: string;
  body: string;
  spotlight: "center" | "zoning" | "resources" | "herald" | "save" | null;
  cardPlacement: "center" | "bottom" | "top" | "right" | "left";
  hint: string;
  manualAdvance?: boolean;
};

const STEPS: OnboardingStep[] = [
  {
    id: "welcome",
    title: "Welcome, Mayor",
    body: "You have been sworn in to lead a frontier settlement. This short tour covers the essentials — zone land, watch your city grow, read the Herald, and save your progress.",
    spotlight: null,
    cardPlacement: "center",
    hint: "Takes about two minutes.",
    manualAdvance: true,
  },
  {
    id: "zone-residential",
    title: "Zone residential",
    body: "Select Residential in the toolbar, then click tiles on the map to paint housing zones. Roads and demand help buildings appear over time.",
    spotlight: "zoning",
    cardPlacement: "bottom",
    hint: "Paint at least one residential tile.",
  },
  {
    id: "watch-growth",
    title: "Watch your city grow",
    body: "Speed up time and keep an eye on population in the resource bar. As zones develop, residents move in and your frontier becomes a town.",
    spotlight: "resources",
    cardPlacement: "top",
    hint: "Wait for population to rise.",
  },
  {
    id: "open-herald",
    title: "Open the Herald",
    body: "The Daily Herald delivers narrative events from your city — council debates, milestones, and crises. Open it when you want the story of your mayorship.",
    spotlight: "herald",
    cardPlacement: "right",
    hint: "Click the Herald button.",
  },
  {
    id: "save-city",
    title: "Save your city",
    body: "Use Save to snapshot your mayor's term. Slots are limited by your tier — save before experimenting with big changes.",
    spotlight: "save",
    cardPlacement: "left",
    hint: "Save your city to finish.",
  },
];

function readOnboardingDone(): boolean {
  if (typeof window === "undefined") return true;
  return window.localStorage.getItem(ONBOARDING_STORAGE_KEY) === "1";
}

function markOnboardingDone(): void {
  window.localStorage.setItem(ONBOARDING_STORAGE_KEY, "1");
}

export type OnboardingOverlayProps = {
  residentialZonePainted: boolean;
  population: number | null;
  heraldOpen: boolean;
  saveCompleted: boolean;
};

export function OnboardingOverlay({
  residentialZonePainted,
  population,
  heraldOpen,
  saveCompleted,
}: OnboardingOverlayProps) {
  const [visible, setVisible] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);
  const [allowContinue, setAllowContinue] = useState(false);
  const growthBaselineRef = useRef<number | null>(null);

  useEffect(() => {
    setVisible(!readOnboardingDone());
  }, []);

  useEffect(() => {
    setAllowContinue(false);
    if (!visible) return;
    const step = STEPS[stepIndex];
    if (step?.manualAdvance) return;
    const timer = window.setTimeout(() => setAllowContinue(true), 10_000);
    return () => window.clearTimeout(timer);
  }, [visible, stepIndex]);

  const dismiss = useCallback(() => {
    markOnboardingDone();
    setVisible(false);
  }, []);

  const advance = useCallback(() => {
    if (stepIndex >= STEP_COUNT - 1) {
      dismiss();
      return;
    }
    setStepIndex((i) => i + 1);
  }, [dismiss, stepIndex]);

  const step = STEPS[stepIndex];

  useEffect(() => {
    if (!visible || !step) return;

    if (step.id === "watch-growth") {
      if (growthBaselineRef.current === null && population !== null) {
        growthBaselineRef.current = population;
      }
      if (
        growthBaselineRef.current !== null &&
        population !== null &&
        population > growthBaselineRef.current
      ) {
        const timer = window.setTimeout(advance, 600);
        return () => window.clearTimeout(timer);
      }
      return;
    }

    if (step.id === "zone-residential" && residentialZonePainted) {
      const timer = window.setTimeout(advance, 400);
      return () => window.clearTimeout(timer);
    }

    if (step.id === "open-herald" && heraldOpen) {
      const timer = window.setTimeout(advance, 400);
      return () => window.clearTimeout(timer);
    }

    if (step.id === "save-city" && saveCompleted) {
      const timer = window.setTimeout(advance, 500);
      return () => window.clearTimeout(timer);
    }
  }, [
    visible,
    step,
    residentialZonePainted,
    population,
    heraldOpen,
    saveCompleted,
    advance,
  ]);

  if (!visible || !step) return null;

  const canManualAdvance = step.manualAdvance === true || allowContinue;
  const spotlightClass = step.spotlight
    ? `hud-onboarding__spotlight hud-onboarding__spotlight--${step.spotlight}`
    : null;

  return (
    <div
      className="hud-onboarding"
      role="dialog"
      aria-modal="true"
      aria-labelledby="onboarding-title"
      aria-describedby="onboarding-body"
    >
      {step.spotlight === null ? (
        <div className="hud-onboarding__scrim" aria-hidden />
      ) : (
        <div className={spotlightClass ?? undefined} aria-hidden />
      )}

      <div className={`hud-onboarding__card hud-onboarding__card--${step.cardPlacement}`}>
        <div className="hud-onboarding__header">
          <span className="hud-onboarding__step">
            Step {stepIndex + 1} of {STEP_COUNT}
          </span>
          <button
            type="button"
            className="hud-onboarding__dismiss"
            onClick={dismiss}
            aria-label="Dismiss tutorial"
          >
            Skip
          </button>
        </div>

        <h2 id="onboarding-title" className="hud-onboarding__title">
          {step.title}
        </h2>
        <p id="onboarding-body" className="hud-onboarding__body">
          {step.body}
        </p>

        <div className="hud-onboarding__footer">
          <span className="hud-onboarding__hint">{step.hint}</span>
          {canManualAdvance ? (
            <button type="button" className="hud-onboarding__next" onClick={advance}>
              {step.manualAdvance
                ? stepIndex >= STEP_COUNT - 1
                  ? "Done"
                  : "Begin tour"
                : "Continue"}
            </button>
          ) : (
            <button type="button" className="hud-onboarding__next" disabled>
              {stepIndex >= STEP_COUNT - 1 ? "Finishing…" : "Continue"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

/** Returns true when the painted zone type is residential (low or high). */
export function isResidentialZonePaint(zoneType: number): boolean {
  return (
    zoneType === ENGINE_ZONE_TYPE.residential ||
    zoneType === ENGINE_ZONE_TYPE.residential + 1
  );
}
