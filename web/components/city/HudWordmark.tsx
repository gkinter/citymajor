"use client";

import { HUD_ZONE } from "@/lib/hud-theme";

export function HudWordmark() {
  return (
    <div style={HUD_ZONE.topCenter} className="hud-wordmark" aria-hidden="true">
      <h1 className="hud-wordmark__title">CityMajor</h1>
      <p className="hud-wordmark__subtitle">City Simulator</p>
    </div>
  );
}
