"use client";

import { useEffect, useState } from "react";
import {
  CRISIS_BANKRUPTCY_SESSION_KEY,
  CRISIS_LOW_APPROVAL_SESSION_KEY,
  LOW_APPROVAL_WARNING_THRESHOLD,
} from "@/lib/constants";
import type { SimResources } from "@/lib/sim-bridge";

type CrisisKind = "bankruptcy" | "low-approval";

const COPY: Record<
  CrisisKind,
  { eyebrow: string; title: string; body: string }
> = {
  bankruptcy: {
    eyebrow: "Treasury alert",
    title: "City finances are in the red",
    body:
      "Your treasury balance has dropped below zero. Cut spending, raise revenue, or take on debt before services stall and citizens lose confidence.",
  },
  "low-approval": {
    eyebrow: "Political crisis",
    title: "Mayor approval is critically low",
    body:
      "Approval has fallen below 30%. Address citizen concerns, improve services, or read the Herald for unrest before protests escalate.",
  },
};

function isDismissed(kind: CrisisKind): boolean {
  if (typeof window === "undefined") return true;
  const key =
    kind === "bankruptcy"
      ? CRISIS_BANKRUPTCY_SESSION_KEY
      : CRISIS_LOW_APPROVAL_SESSION_KEY;
  return window.sessionStorage.getItem(key) === "1";
}

function detectCrisis(resources: SimResources): CrisisKind | null {
  if (resources.cityFunds < 0 && !isDismissed("bankruptcy")) {
    return "bankruptcy";
  }
  if (
    resources.approval !== undefined &&
    resources.approval < LOW_APPROVAL_WARNING_THRESHOLD &&
    !isDismissed("low-approval")
  ) {
    return "low-approval";
  }
  return null;
}

type CrisisWarningModalProps = {
  resources: SimResources | null;
};

export function CrisisWarningModal({ resources }: CrisisWarningModalProps) {
  const [activeWarning, setActiveWarning] = useState<CrisisKind | null>(null);

  useEffect(() => {
    if (!resources || activeWarning) return;
    const crisis = detectCrisis(resources);
    if (crisis) setActiveWarning(crisis);
  }, [resources, activeWarning]);

  if (!activeWarning) return null;

  const copy = COPY[activeWarning];

  const dismiss = () => {
    const key =
      activeWarning === "bankruptcy"
        ? CRISIS_BANKRUPTCY_SESSION_KEY
        : CRISIS_LOW_APPROVAL_SESSION_KEY;
    window.sessionStorage.setItem(key, "1");
    setActiveWarning(null);
  };

  return (
    <div
      className="hud-crisis"
      role="dialog"
      aria-modal="true"
      aria-labelledby="crisis-warning-title"
      onClick={dismiss}
    >
      <div className="hud-crisis__card" onClick={(e) => e.stopPropagation()}>
        <p className="hud-crisis__eyebrow">{copy.eyebrow}</p>
        <h2 id="crisis-warning-title" className="hud-crisis__title">
          {copy.title}
        </h2>
        <p className="hud-crisis__body">{copy.body}</p>
        <div className="hud-crisis__footer">
          <button
            type="button"
            className="hud-crisis__dismiss"
            onClick={dismiss}
          >
            Understood
          </button>
        </div>
      </div>
    </div>
  );
}
