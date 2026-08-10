"use client";

import type { CSSProperties } from "react";
import {
  economyFromRciFallback,
  formatGoodName,
  formatGoodPrice,
  zoningHintForGood,
} from "@/lib/economy-goods";
import {
  HUD_COLORS,
  hudSlidePanel,
} from "@/lib/hud-theme";
import type { EconomySnapshot, SimResources } from "@/lib/sim-bridge";

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 24px",
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 8,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const fallbackNote: CSSProperties = {
  marginBottom: 14,
  padding: "8px 10px",
  borderRadius: 6,
  fontSize: 11,
  lineHeight: 1.4,
  color: HUD_COLORS.textMuted,
  background: HUD_COLORS.rowBg,
  border: `1px solid ${HUD_COLORS.borderSubtle}`,
};

/** v2 inter-city trade epic — co-op regional play, headless sim server. */
const SB3728_TRADE_EPIC_URL =
  "https://linear.app/softblaze/issue/SB-3728";

type EconomyPanelProps = {
  open: boolean;
  onClose: () => void;
  resources: SimResources | null;
};

function formatTreasury(cityFunds: number): string {
  const abs = Math.abs(cityFunds);
  if (abs >= 1_000_000) return `$${(cityFunds / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `$${(cityFunds / 1_000).toFixed(1)}K`;
  return `$${cityFunds.toLocaleString()}`;
}

function formatTaxRate(rate: number): string {
  const normalized = rate <= 1 ? rate * 100 : rate;
  return Number.isInteger(normalized)
    ? `${normalized}%`
    : `${normalized.toFixed(1)}%`;
}

function SummaryStat({
  label,
  value,
  valueClassName,
}: {
  label: string;
  value: string;
  valueClassName?: string;
}) {
  return (
    <div className="hud-economy-stat">
      <span className="hud-economy-stat__label">{label}</span>
      <span
        className={
          valueClassName
            ? `hud-economy-stat__value ${valueClassName}`
            : "hud-economy-stat__value"
        }
      >
        {value}
      </span>
    </div>
  );
}

function EconomySummary({ resources }: { resources: SimResources | null }) {
  if (!resources) {
    return (
      <section className="hud-economy-section" aria-label="City treasury">
        <div className="hud-economy-section-title">Summary</div>
        <p className="hud-economy-empty">Treasury unavailable — sim not loaded.</p>
      </section>
    );
  }

  const taxRate = resources.taxRate;

  return (
    <section className="hud-economy-section" aria-label="City treasury">
      <div className="hud-economy-section-title">Summary</div>
      <div className="hud-economy-summary">
        <SummaryStat
          label="Treasury"
          value={formatTreasury(resources.cityFunds)}
          valueClassName="hud-economy-stat__value--treasury"
        />
        {taxRate !== undefined ? (
          <SummaryStat label="Tax rate" value={formatTaxRate(taxRate)} />
        ) : null}
      </div>
    </section>
  );
}

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

type DisplayRow = {
  name: string;
  magnitude: number;
  price?: number;
  hint?: string;
};

function resolveEconomyView(resources: SimResources | null): {
  shortages: DisplayRow[];
  surpluses: DisplayRow[];
  fallbackLabel?: string;
} {
  const economy = resources?.economy;
  if (economy) {
    return {
      shortages: economy.shortages.map((row) => ({
        name: row.name,
        magnitude: row.magnitude,
        price: row.price,
        hint: zoningHintForGood(row.name),
      })),
      surpluses: economy.surpluses.map((row) => ({
        name: row.name,
        magnitude: row.magnitude,
        price: row.price,
        hint: zoningHintForGood(row.name),
      })),
    };
  }

  const fallback = economyFromRciFallback(resources);
  if (!fallback) return { shortages: [], surpluses: [] };

  return {
    fallbackLabel: fallback.label,
    shortages: fallback.shortages.map((row) => ({
      name: row.name,
      magnitude: row.magnitude,
      hint:
        row.name.startsWith("Residential")
          ? "Zone R for housing"
          : row.name.startsWith("Commercial")
            ? "Zone C for shops"
            : row.name.startsWith("Industrial")
              ? "Zone I for jobs"
              : undefined,
    })),
    surpluses: fallback.surpluses.map((row) => ({
      name: row.name,
      magnitude: row.magnitude,
    })),
  };
}

function formatMagnitude(value: number, isRciFallback: boolean): string {
  if (isRciFallback) return value >= 0 ? `+${(value * 100).toFixed(0)}` : `${(value * 100).toFixed(0)}`;
  if (value >= 100) return value.toFixed(0);
  if (value >= 10) return value.toFixed(1);
  return value.toFixed(2);
}

function ImbalanceList({
  title,
  rows,
  tone,
  isRciFallback,
}: {
  title: string;
  rows: DisplayRow[];
  tone: "shortage" | "surplus";
  isRciFallback: boolean;
}) {
  if (rows.length === 0) {
    return (
      <section className="hud-economy-section" aria-label={title}>
        <div
          style={{
            ...sectionTitle,
            color: tone === "shortage" ? "#ff9aa8" : "#7dffb2",
          }}
        >
          {title}
        </div>
        <p className="hud-economy-empty">None detected</p>
      </section>
    );
  }

  return (
    <section className="hud-economy-section" aria-label={title}>
      <div
        style={{
          ...sectionTitle,
          color: tone === "shortage" ? "#ff9aa8" : "#7dffb2",
        }}
      >
        {title}
      </div>
      <ul className="hud-economy-list">
        {rows.map((row) => (
          <li key={row.name} className={`hud-economy-list__item hud-economy-list__item--${tone}`}>
            <div className="hud-economy-list__row">
              <span className="hud-economy-list__name">
                {formatGoodName(row.name)}
              </span>
              <span className="hud-economy-list__metrics">
                <span className="hud-economy-list__magnitude">
                  {formatMagnitude(row.magnitude, isRciFallback)}
                </span>
                {row.price !== undefined ? (
                  <span className="hud-economy-list__price">
                    {formatGoodPrice(row.price)}
                  </span>
                ) : null}
              </span>
            </div>
            {row.hint ? (
              <div className="hud-economy-list__hint">{row.hint}</div>
            ) : null}
          </li>
        ))}
      </ul>
    </section>
  );
}

function hasFoundationMetrics(resources: SimResources | null): resources is SimResources {
  if (!resources) return false;
  return (
    resources.employmentRate !== undefined ||
    resources.interZoneTradeVolume !== undefined ||
    resources.marketZoneCount !== undefined ||
    resources.residentialDemand !== undefined ||
    resources.meanRentBurden !== undefined ||
    resources.residentialVacancy !== undefined ||
    hasMonthlyBudget(resources)
  );
}

function hasMonthlyBudget(resources: SimResources): boolean {
  return (
    resources.monthlyIncome !== undefined &&
    resources.monthlyExpenses !== undefined
  );
}

function formatDemand(demand: number): string {
  const pct = Math.round(demand * 100);
  return pct >= 0 ? `+${pct}%` : `${pct}%`;
}

function FoundationMetrics({ resources }: { resources: SimResources }) {
  const monthlyNet = hasMonthlyBudget(resources)
    ? resources.monthlyIncome! - resources.monthlyExpenses!
    : null;

  return (
    <section className="hud-economy-section" aria-label="Foundation economy">
      <div style={{ ...sectionTitle, color: "#9ecbff" }}>Foundation</div>
      <ul className="hud-economy-trade">
        {monthlyNet !== null ? (
          <li className="hud-economy-trade__row hud-economy-trade__row--balance">
            <span className="hud-economy-trade__label">Treasury flow</span>
            <span
              className={`hud-economy-trade__value ${
                monthlyNet >= 0
                  ? "hud-economy-trade__value--in"
                  : "hud-economy-trade__value--out"
              }`}
            >
              {formatTradeMoney(monthlyNet)}/mo
            </span>
          </li>
        ) : null}
        {resources.employmentRate !== undefined ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">Employment</span>
            <span className="hud-economy-trade__value">
              {Math.round(resources.employmentRate * 100)}%
            </span>
          </li>
        ) : null}
        {resources.interZoneTradeVolume !== undefined ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">Inter-zone</span>
            <span className="hud-economy-trade__value">
              {resources.interZoneTradeVolume.toLocaleString()}/day
              {resources.meanInterZoneFriction !== undefined
                ? ` · ×${resources.meanInterZoneFriction.toFixed(2)} friction`
                : ""}
            </span>
          </li>
        ) : null}
        {resources.marketZoneCount !== undefined && resources.marketZoneCount > 1 ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">Market zones</span>
            <span className="hud-economy-trade__value">
              {resources.marketZoneCount} partitions
            </span>
          </li>
        ) : null}
        {resources.residentialDemand !== undefined ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">RCI demand</span>
            <span className="hud-economy-trade__value">
              R {formatDemand(resources.residentialDemand)} · C{" "}
              {formatDemand(resources.commercialDemand ?? 0)} · I{" "}
              {formatDemand(resources.industrialDemand ?? 0)}
            </span>
          </li>
        ) : null}
        {resources.meanRentBurden !== undefined ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">Rent burden</span>
            <span className="hud-economy-trade__value">
              {Math.round(resources.meanRentBurden * 100)}% mean
            </span>
          </li>
        ) : null}
        {resources.residentialVacancy !== undefined ? (
          <li className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">Housing vacancy</span>
            <span className="hud-economy-trade__value">
              {Math.round(resources.residentialVacancy * 100)}%
            </span>
          </li>
        ) : null}
      </ul>
    </section>
  );
}

function ZonePriceSpreads({ economy }: { economy: EconomySnapshot }) {
  const spreads =
    economy.marketZonePrices?.filter((row) => row.maxPrice > row.minPrice * 1.05) ??
    [];
  if (spreads.length === 0) return null;

  return (
    <section className="hud-economy-section" aria-label="District price spread">
      <div style={{ ...sectionTitle, color: "#ffd27a" }}>District spreads</div>
      <ul className="hud-economy-trade">
        {spreads.slice(0, 4).map((row) => (
          <li key={row.name} className="hud-economy-trade__row">
            <span className="hud-economy-trade__label">
              {formatGoodName(row.name)}
            </span>
            <span className="hud-economy-trade__value">
              {formatGoodPrice(row.minPrice)} – {formatGoodPrice(row.maxPrice)}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function TradeRoutesStub() {
  return (
    <section className="hud-economy-section" aria-label="Trade routes">
      <div style={{ ...sectionTitle, color: "#c9a0ff" }}>Trade routes</div>
      <div className="hud-economy-routes-stub">
        <p className="hud-economy-routes-stub__text">
          Inter-city trade — partner routes, recurring contracts, and stable
          pricing between cities on a shared region — ships in v2.
        </p>
        <p className="hud-economy-routes-stub__status">No routes · read-only preview</p>
        <a
          className="hud-economy-routes-stub__link"
          href={SB3728_TRADE_EPIC_URL}
          target="_blank"
          rel="noopener noreferrer"
        >
          SB-3728 — v2 Trade & MP epic
        </a>
      </div>
    </section>
  );
}

export function EconomyPanel({ open, onClose, resources }: EconomyPanelProps) {
  if (!open) return null;

  const view = resolveEconomyView(resources);
  const isRciFallback = Boolean(view.fallbackLabel);

  return (
    <aside
      className="hud-economy-panel"
      aria-label="City economy"
      style={hudSlidePanel()}
    >
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 14 }}>Market</div>
          <div style={{ fontSize: 11, opacity: 0.65, marginTop: 2 }}>
            Leontief goods flow
          </div>
        </div>
        <button
          type="button"
          className="hud-economy-close"
          style={{
            padding: "4px 10px",
            fontFamily: "inherit",
            fontSize: 12,
            color: HUD_COLORS.text,
            background: "transparent",
            border: `1px solid ${HUD_COLORS.borderSubtle}`,
            borderRadius: 6,
            cursor: "pointer",
          }}
          onClick={onClose}
        >
          Close
        </button>
      </header>

      <div style={bodyStyle}>
        <EconomySummary resources={resources} />

        {hasFoundationMetrics(resources) ? (
          <FoundationMetrics resources={resources} />
        ) : null}

        {resources?.economy?.marketZonePrices?.length
          ? <ZonePriceSpreads economy={resources.economy} />
          : null}

        {hasTradeData(resources) ? (
          <section className="hud-economy-section" aria-label="Global trade">
            <div style={{ ...sectionTitle, color: "#9ecbff" }}>Global trade</div>
            <ul className="hud-economy-trade">
              <li className="hud-economy-trade__row">
                <span className="hud-economy-trade__label">Exports</span>
                <span className="hud-economy-trade__value hud-economy-trade__value--in">
                  {formatTradeMoney(resources.monthlyExportValue ?? 0)}
                </span>
              </li>
              <li className="hud-economy-trade__row">
                <span className="hud-economy-trade__label">Imports</span>
                <span className="hud-economy-trade__value hud-economy-trade__value--out">
                  {formatTradeMoney(-(resources.monthlyImportCost ?? 0))}
                </span>
              </li>
              <li className="hud-economy-trade__row hud-economy-trade__row--balance">
                <span className="hud-economy-trade__label">Balance</span>
                <span
                  className={`hud-economy-trade__value ${
                    (resources.tradeBalance ?? 0) >= 0
                      ? "hud-economy-trade__value--in"
                      : "hud-economy-trade__value--out"
                  }`}
                >
                  {formatTradeMoney(resources.tradeBalance ?? 0)}
                </span>
              </li>
            </ul>
            <p className="hud-economy-trade__note">
              Last month on the anonymous global market (PartnerCityId −1).
            </p>
          </section>
        ) : null}

        <TradeRoutesStub />

        {view.fallbackLabel ? (
          <p style={fallbackNote}>{view.fallbackLabel}</p>
        ) : null}

        <ImbalanceList
          title="Shortages"
          rows={view.shortages}
          tone="shortage"
          isRciFallback={isRciFallback}
        />

        <ImbalanceList
          title="Surpluses"
          rows={view.surpluses}
          tone="surplus"
          isRciFallback={isRciFallback}
        />
      </div>
    </aside>
  );
}
