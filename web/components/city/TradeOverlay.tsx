"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudPanel,
} from "@/lib/hud-theme";
import type { SimResources } from "@/lib/sim-bridge";

type TradeOverlayProps = {
  visible: boolean;
  resources: SimResources | null;
};

function formatTradeMoney(amount: number): string {
  const abs = Math.abs(amount);
  const sign = amount >= 0 ? "+" : "−";
  if (abs >= 1_000_000) return `${sign}$${(abs / 1_000_000).toFixed(2)}M`;
  if (abs >= 1_000) return `${sign}$${(abs / 1_000).toFixed(1)}K`;
  return `${sign}$${abs.toLocaleString()}`;
}

function hasTradeData(resources: SimResources | null): resources is SimResources {
  if (!resources) return false;
  return (
    resources.monthlyExportValue !== undefined ||
    resources.monthlyImportCost !== undefined ||
    resources.tradeBalance !== undefined
  );
}

const rowStyle: CSSProperties = {
  display: "flex",
  justifyContent: "space-between",
  gap: 12,
  fontSize: 11,
};

const labelStyle: CSSProperties = {
  color: HUD_COLORS.textMuted,
};

const valueInStyle: CSSProperties = {
  color: "#7dcea0",
  fontVariantNumeric: "tabular-nums",
};

const valueOutStyle: CSSProperties = {
  color: "#f5b7b1",
  fontVariantNumeric: "tabular-nums",
};

/**
 * Read-only global-market trade summary — mirrors TradeSystem auto import/export
 * (PartnerCityId = -1). Map heatmap and bilateral routes are v2 (SB-3728).
 */
export function TradeOverlay({ visible, resources }: TradeOverlayProps) {
  if (!visible) return null;

  const panelStyle = hudPanel({
    ...HUD_ZONE.bottomLeft,
    bottom: 96,
    display: "flex",
    flexDirection: "column",
    gap: 4,
    padding: "8px 12px",
    minWidth: 220,
    pointerEvents: "none",
  });

  if (!hasTradeData(resources)) {
    return (
      <div
        style={panelStyle}
        data-testid="trade-overlay"
        aria-label="Global market trade"
        title="TradeSystem — waiting for WASM monthly trade tick"
      >
        <div style={{ fontSize: 10, letterSpacing: "0.06em", opacity: 0.75 }}>
          GLOBAL MARKET
        </div>
        <div style={{ fontSize: 11, color: HUD_COLORS.textDim }}>
          No trade data yet — advance time for monthly settlement.
        </div>
      </div>
    );
  }

  const exports = resources.monthlyExportValue ?? 0;
  const imports = resources.monthlyImportCost ?? 0;
  const balance = resources.tradeBalance ?? exports - imports;

  return (
    <div
      style={panelStyle}
      data-testid="trade-overlay"
      aria-label="Global market trade"
      title="Auto export surpluses and import deficits against the global price index"
    >
      <div style={{ fontSize: 10, letterSpacing: "0.06em", opacity: 0.75 }}>
        GLOBAL MARKET
      </div>
      <div style={rowStyle}>
        <span style={labelStyle}>Exports</span>
        <span style={valueInStyle}>{formatTradeMoney(exports)}</span>
      </div>
      <div style={rowStyle}>
        <span style={labelStyle}>Imports</span>
        <span style={valueOutStyle}>{formatTradeMoney(-imports)}</span>
      </div>
      <div style={{ ...rowStyle, borderTop: `1px solid ${HUD_COLORS.borderSubtle}`, paddingTop: 4 }}>
        <span style={labelStyle}>Balance</span>
        <span
          style={
            balance >= 0
              ? valueInStyle
              : valueOutStyle
          }
        >
          {formatTradeMoney(balance)}
        </span>
      </div>
      <div style={{ fontSize: 10, color: HUD_COLORS.textDim, marginTop: 2 }}>
        Inter-city routes — v2 preview
      </div>
    </div>
  );
}
