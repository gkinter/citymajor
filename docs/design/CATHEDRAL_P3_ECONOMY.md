# Cathedral P3 — Economy & Trade

**Status:** Spec (characterization tests pinned; core economy live)  
**Program:** WP-E cathedral stretch · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md)  
**Prerequisites:** P2 goods shortage index · P1 traffic / inter-zone friction  
**References:** [`AGENT_03_ECONOMY.md`](./AGENT_03_ECONOMY.md) · [`SIMULATION_ARCHITECTURE.md`](./SIMULATION_ARCHITECTURE.md) §5

---

## 1. Goal

Make the **goods economy legible** in HUD: top shortages/surpluses with prices, market-zone partitions on snapshot, and visible inter-zone friction. Tie delivery delays to traffic (P1). Keep **bilateral trade routes** explicitly out of v1.

---

## 2. Milestones (v1)

| ID | Deliverable | Current state |
|----|-------------|---------------|
| P3.1 | Goods panel data contract (top 5 + prices) | **Partial** — names + magnitude in `EconomySnapshotDto`; prices not exported |
| P3.2 | Market zone partition count on snapshot | **Partial** — `ActiveZoneCount` in `EconomySystem`; export stub `MarketZoneCount` |
| P3.3 | Per-zone price spread visible (partition prices differ) | **Done** — `GetAnchorZonePriceSpreads` + Unity Economy/Cathedral HUD; scale 1→4→9→16 |
| P3.4 | Inter-zone friction on snapshot + HUD | **Live** — `MeanInterZoneFriction`, `InterZoneTradeVolume`, `GoodsTransportCostIndex`, friction corridor overlay |
| P3.5 | Traffic delay → goods delivery lag | **Not implemented** — cross-link P1 |
| P3.6 | Bilateral trade routes | **v2 boundary** — `TradeSystem.CreateTradeRoute` exists; player-facing routes deferred |

---

## 3. Goods panel data contract

**Consumer:** Unity Budget/Herald panel · WASM `SimSnapshotDto.Economy` · web HUD (maintenance).

### 3.1 Top imbalances (live)

```typescript
interface GoodImbalanceRow {
  goodId: number;      // Good enum byte 0–44
  name: string;        // Good.ToString()
  magnitude: number;   // |demand − supply| daily units (city-wide sum)
  direction: "shortage" | "surplus";
}
```

Source: `EconomySystem.GetTopImbalances(5)` → `WorldState.TopShortageGoodIds` / `TopSurplusGoodIds`.

### 3.2 Prices (P3.1 extension)

```typescript
interface GoodPriceRow {
  goodId: number;
  name: string;
  cityAvgPrice: number;     // mean across active market zones
  basePrice: number;        // EconomySystem.BasePrices
  priceRatio: number;       // cityAvgPrice / basePrice
}
```

Panel shows **top 5 shortages** and **top 5 surpluses**, each row includes `cityAvgPrice` for that good.

**WASM JSON shape (target):**

```json
{
  "economy": {
    "shortages": [{ "name": "Food", "magnitude": 12.4, "price": 8.2, "goodId": 35 }],
    "surpluses": [{ "name": "Timber", "magnitude": 5.1, "price": 2.8, "goodId": 3 }],
    "shortageIndex": 0.31,
    "surplusIndex": 0.08
  }
}
```

### 3.3 Scalar indices (live)

| Field | Range | Meaning |
|-------|-------|---------|
| `GoodsShortageIndex` | 0–1 | Σ positive (demand−supply) / Σ activity |
| `GoodsSurplusIndex` | 0–1 | Σ positive (supply−demand) / Σ activity |
| `IndustrialDemand` | −1–1 | RCI industrial signal (food scarcity component) |

---

## 4. Market zone partition export

**Partition model** (`EconomySystem`):

- `ActiveZoneCount` scales with population: 1 → 4 → 9 → 16 zones.
- Tile assignment: `GetMarketZoneForTile(x,y,worldSize)` — sqrt(N) × sqrt(N) grid.
- Each zone holds independent `Supply[]`, `Demand[]`, `Prices[]`.

**Snapshot fields:**

| Field | Type | Source |
|-------|------|--------|
| `MarketZoneCount` | `int` | `EconomySystem.ActiveZoneCount` after daily tick |
| `MeanInterZoneFriction` | `float` | Weighted mean friction of executed transfers (≥1.0) |
| `InterZoneTradeVolume` | `float` | Daily units moved across zones |

**P3.3 extension:** sparse `MarketZonePriceDto[]` / `ZonePriceSpread[]` for anchor goods when `MarketZoneCount > 1` — live on WASM DTO + Unity Economy panel.

---

## 5. Inter-zone friction visibility

**Friction matrix** (`EconomySystem.TradeCoefficients`, 16×16):

| Pair | Coefficient | Meaning |
|------|-------------|---------|
| Same zone | 1.00 | Local market |
| Orthogonal neighbor | 1.05 | +5% effective distance |
| Diagonal neighbor | 1.15 | +15% |

**HUD copy:** “Trade friction ×1.12 — goods move slowly between districts; improve roads or add warehouses.”

**Live metrics:** `WorldState.MeanInterZoneFriction`, `InterZoneTradeVolume`, `GoodsTransportCostIndex` — published in `PublishImbalancesTo`.

**Overlay:** sparse `FrictionCorridors[]` along market-zone boundaries (toggle on `/play`); heat scales with pair friction × mean inter-zone friction, boosted by road congestion.

**Test coverage:** `InterZoneTradeTests` (not Cathedral-prefixed) · `CathedralEconomyTests` friction / corridor exports.

---

## 6. Traffic delay → goods delivery (P1 × P3)

**Target model (P3.5):**

```
delivery_delay_z = mean_edge_delay_z / baseline_edge_delay    // from P1 traffic partition
effective_friction(z_a, z_b) = TradeCoefficients[z_a,z_b] * (1 + 0.5 * max(delay_z_a, delay_z_b))
```

Effects:

- High congestion reduces cross-zone trade volume in `CrossZoneTrade` greedy matcher.
- Same-zone supply consumed first; distant zones starve during rush hour.
- `MeanInterZoneFriction` rises when traffic delays rise (observable feedback loop).

**Acceptance:** characterization test in `CathedralEconomyTests` when P3.5 lands (not skipped yet — add with Skip when stubbing).

Cross-reference: P1 traffic partition observability · `WasmTrafficLite` · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md) WP-A.

---

## 7. v2 boundary — bilateral trade routes

**In codebase but not v1 player feature:**

- `TradeSystem.CreateTradeRoute(partnerCityId, good, qty, price, months)`
- `TradeRoute` contracts with partner city ID ≠ global market

**v1 behavior (keep):**

- `TradeSystem.AutoTrade` — surplus export / deficit import at `GlobalPrices` ± markup/discount.
- `TradeSystem.ApplyGlobalEvent` — oil shock, food crisis, etc. on global prices.
- Monthly `TradeBalance`, `MonthlyExportValue`, `MonthlyImportCost` on snapshot.

**Explicit non-goals for v1:**

- UI to negotiate routes with neighbor cities.
- Multi-city save linking.
- Route cancellation penalties / diplomacy.

Document in UI: “International trade is automatic; trade agreements — coming in regional update.”

---

## 8. Acceptance tests (characterization)

| Test | File | Status |
|------|------|--------|
| Goods shortage index correlates with industrial/commercial imbalance | `CathedralEconomyTests` | **Implement** (may pass today) |
| Market zone prices differ across partitions | `CathedralEconomyTests` | **Green** (incl. trade-shock + 9/16 scale) |
| `MarketZoneCount` on snapshot matches economy | `CathedralEconomyTests` | After export stub |
| Traffic delay increases mean inter-zone friction | `CathedralEconomyTests` | Pending P3.5 |
| Goods panel JSON includes prices per top imbalance | Integration | Pending P3.1 |

```bash
dotnet test tests/Forge.SimCore.Tests --filter "FullyQualifiedName~Cathedral"
```

---

## 9. Implementation order (recommended)

1. **P3.2** `MarketZoneCount` snapshot export (small, enables HUD scale indicator).  
2. **P3.1** Add `price` + `goodId` to `GoodImbalanceDto` / `EconomySnapshotDto`.  
3. **P3.3** Per-zone price spread test + optional sparse zone price export.  
4. **P3.5** Wire traffic partition delays into `CrossZoneTrade` friction multiplier.  
5. **P3.6** Document v2 trade-route UI only — no sim changes in v1.

**Dependency:** P3.5 requires P1 traffic partition delay export stable on snapshot (`MeanTrafficDensity` per partition or edge delay rollup).
