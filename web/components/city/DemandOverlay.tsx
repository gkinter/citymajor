"use client";

import type { CSSProperties } from "react";
import { HUD_ZONE, hudPanel } from "@/lib/hud-theme";
import type { RciDemand } from "@/lib/sim-bridge";

function clampDemand(value: number): number {
  return Math.max(-1, Math.min(1, value));
}

function demandToDisplay(normalized: number): number {
  return Math.round(clampDemand(normalized) * 100);
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}

type UtilityStressTone = "ok" | "warn" | "stress";

function utilityStressTone(stress: number): UtilityStressTone {
  if (stress < 0.2) return "ok";
  if (stress <= 0.5) return "warn";
  return "stress";
}

const GOODS_SHORTAGE_WARN_PCT = 35;

type RciMeterProps = {
  label: "R" | "C" | "I";
  demand: number;
};

function RciMeter({ label, demand }: RciMeterProps) {
  const clamped = clampDemand(demand);
  const display = demandToDisplay(clamped);
  const halfPct = Math.abs(clamped) * 50;
  const fillStyle: CSSProperties =
    clamped >= 0
      ? { left: "50%", width: `${halfPct}%` }
      : { left: `${50 - halfPct}%`, width: `${halfPct}%` };

  return (
    <div className="hud-rci__row">
      <span className={`hud-rci__label hud-rci__label--${label.toLowerCase()}`}>
        {label}
      </span>
      <div className="hud-rci__track" aria-hidden>
        <div
          className={`hud-rci__fill hud-rci__fill--${label.toLowerCase()}`}
          style={fillStyle}
        />
      </div>
      <span className="hud-rci__value">
        {display > 0 ? `+${display}` : display}
      </span>
    </div>
  );
}

type GoodsShortageReadoutProps = {
  shortageIndex: number;
};

function GoodsShortageReadout({ shortageIndex }: GoodsShortageReadoutProps) {
  const clamped = clamp01(shortageIndex);
  const pct = Math.round(clamped * 100);
  const warn = pct >= GOODS_SHORTAGE_WARN_PCT;

  return (
    <div
      className={`hud-rci__foundation hud-rci__goods${warn ? " hud-rci__goods--warn" : ""}`}
      title={`City-wide goods shortage pressure — ${pct}%`}
    >
      <span className="hud-rci__foundation-label">Goods</span>
      <div className="hud-rci__goods-track" aria-hidden>
        <div
          className="hud-rci__goods-fill"
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="hud-rci__goods-value">{pct}%</span>
    </div>
  );
}

type UtilityStressReadoutProps = {
  powerCoverageFraction?: number;
  waterCoverageFraction?: number;
  utilityStressIndex?: number;
};

function UtilityStressReadout({
  powerCoverageFraction,
  waterCoverageFraction,
  utilityStressIndex = 0,
}: UtilityStressReadoutProps) {
  const tone = utilityStressTone(utilityStressIndex);
  const powerPct = Math.round((powerCoverageFraction ?? 0) * 100);
  const waterPct = Math.round((waterCoverageFraction ?? 0) * 100);

  return (
    <div
      className={`hud-rci__foundation hud-resources__utilities hud-resources__utilities--${tone}`}
      title={`Utility coverage — power ${powerPct}%, water ${waterPct}%${
        utilityStressIndex !== undefined
          ? `; stress ${Math.round(utilityStressIndex * 100)}%`
          : ""
      }`}
    >
      <span className="hud-rci__foundation-label">Util</span>
      <span
        className={`hud-resources__utilities-value hud-resources__utilities-value--${tone}`}
      >
        ⚡ {powerPct}% · 💧 {waterPct}%
      </span>
    </div>
  );
}

export type DemandOverlayProps = {
  rci: RciDemand | null;
  goodsShortageIndex?: number;
  utilityStressIndex?: number;
  powerCoverageFraction?: number;
  waterCoverageFraction?: number;
};

function hasUtilityData(
  powerCoverageFraction?: number,
  waterCoverageFraction?: number,
): boolean {
  return (
    powerCoverageFraction !== undefined || waterCoverageFraction !== undefined
  );
}

/** Bottom-center R/C/I demand bars — classic SimCity-style meters. */
export function DemandOverlay({
  rci,
  goodsShortageIndex,
  utilityStressIndex,
  powerCoverageFraction,
  waterCoverageFraction,
}: DemandOverlayProps) {
  const showGoods = goodsShortageIndex !== undefined;
  const showUtilities = hasUtilityData(
    powerCoverageFraction,
    waterCoverageFraction,
  );

  if (!rci && !showGoods && !showUtilities) return null;

  const panelStyle = hudPanel({
    ...HUD_ZONE.bottomCenter,
    bottom: 96,
    display: "flex",
    flexDirection: "column",
    gap: 3,
    padding: "6px 12px",
    pointerEvents: "none",
  });

  return (
    <div
      style={panelStyle}
      className="hud-rci hud-rci--panel"
      aria-label="RCI demand and economy pressure"
      title="R/C/I demand — high values mean zone that type to grow"
      data-onboarding-target="demand"
    >
      {rci ? (
        <>
          <RciMeter label="R" demand={rci.residential} />
          <RciMeter label="C" demand={rci.commercial} />
          <RciMeter label="I" demand={rci.industrial} />
        </>
      ) : null}
      {showGoods ? (
        <GoodsShortageReadout shortageIndex={goodsShortageIndex} />
      ) : null}
      {showUtilities ? (
        <UtilityStressReadout
          powerCoverageFraction={powerCoverageFraction}
          waterCoverageFraction={waterCoverageFraction}
          utilityStressIndex={utilityStressIndex}
        />
      ) : null}
    </div>
  );
}
