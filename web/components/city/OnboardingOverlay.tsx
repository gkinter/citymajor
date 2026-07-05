"use client";

import { useCallback, useEffect, useState } from "react";
import { ONBOARDING_STORAGE_KEY } from "@/lib/constants";
import { ENGINE_ZONE_TYPE } from "@/lib/zoning";

const STEP_COUNT = 5;

type SpotlightTarget =
  | "zoning"
  | "demand"
  | "ticker"
  | "herald"
  | "research"
  | "era";

type OnboardingStep = {
  id: string;
  title: string;
  body: string;
  spotlight: SpotlightTarget | null;
  cardPlacement: "center" | "bottom" | "top" | "right" | "left";
  hint: string;
  manualAdvance?: boolean;
};

const STEPS: OnboardingStep[] = [
  {
    id: "welcome",
    title: "Welcome, Mayor",
    body: "Your city runs on a tight loop: paint residential zones, read R/C/I demand, open the Daily Herald for story events, queue research, and clear Era Quest checklist gates to advance civilization.",
    spotlight: null,
    cardPlacement: "center",
    hint: "Takes about two minutes.",
    manualAdvance: true,
  },
  {
    id: "zone-residential",
    title: "Paint residential zones",
    body: "Select Residential in the zoning toolbar, then brush tiles on the map. Homes need road access — speed up time and watch buildings appear as demand fills in.",
    spotlight: "zoning",
    cardPlacement: "bottom",
    hint: "Paint at least one residential tile.",
  },
  {
    id: "watch-demand",
    title: "Watch demand",
    body: "The R, C, and I meters in the resource bar show shortage (right of center) versus surplus (left). After zoning homes, residential demand often rises — balance with commercial and industrial zones. The News ticker along the bottom scrolls active city events.",
    spotlight: "demand",
    cardPlacement: "top",
    hint: "Glance at demand after zoning.",
  },
  {
    id: "open-herald",
    title: "Open the Herald",
    body: "The Daily Herald turns sim events into narrative — council votes, milestones, and crises. Major events auto-open the Herald; tap the button anytime for a fresh edition driven by your city's state.",
    spotlight: "herald",
    cardPlacement: "right",
    hint: "Click Daily Herald.",
  },
  {
    id: "research-era",
    title: "Research & Era Quest",
    body: "Queue technologies from the Research button beside the Herald. The Era Quest panel tracks checklist objectives — population and technologies researched — you must meet to unlock the next era. Work the checklist; era transitions celebrate your progress.",
    spotlight: "era",
    cardPlacement: "left",
    hint: "Open Research or finish the tour.",
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
  demandVisible: boolean;
  heraldOpen: boolean;
  researchOpen: boolean;
};

export function OnboardingOverlay({
  residentialZonePainted,
  demandVisible,
  heraldOpen,
  researchOpen,
}: OnboardingOverlayProps) {
  const [visible, setVisible] = useState(false);
  const [stepIndex, setStepIndex] = useState(0);
  const [allowContinue, setAllowContinue] = useState(false);

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

    if (step.id === "zone-residential" && residentialZonePainted) {
      const timer = window.setTimeout(advance, 400);
      return () => window.clearTimeout(timer);
    }

    if (step.id === "watch-demand" && residentialZonePainted && demandVisible) {
      const timer = window.setTimeout(advance, 800);
      return () => window.clearTimeout(timer);
    }

    if (step.id === "open-herald" && heraldOpen) {
      const timer = window.setTimeout(advance, 400);
      return () => window.clearTimeout(timer);
    }

    if (step.id === "research-era" && researchOpen) {
      const timer = window.setTimeout(advance, 500);
      return () => window.clearTimeout(timer);
    }
  }, [
    visible,
    step,
    residentialZonePainted,
    demandVisible,
    heraldOpen,
    researchOpen,
    advance,
  ]);

  if (!visible || !step) return null;

  const isLastStep = stepIndex >= STEP_COUNT - 1;
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
                ? isLastStep
                  ? "Done"
                  : "Begin tour"
                : isLastStep
                  ? "Finish tour"
                  : "Continue"}
            </button>
          ) : (
            <button type="button" className="hud-onboarding__next" disabled>
              {isLastStep ? "Finishing…" : "Continue"}
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
