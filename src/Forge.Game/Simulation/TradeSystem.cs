using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Inter-city trade system: import/export with other cities and the global market.
///
/// Goods with local surplus are auto-exported at global market prices.
/// Goods with local shortages are auto-imported (at a markup).
/// Trade routes can be established with partner cities for stable pricing.
/// Global price events (oil shocks, recessions) affect all trade.
/// </summary>
public sealed class TradeSystem
{
    /// <summary>
    /// Global market baseline prices for all 45 goods. These fluctuate with events
    /// and represent the "world price" that local prices converge toward through trade.
    /// </summary>
    public float[] GlobalPrices { get; }

    /// <summary>
    /// A trade agreement with another city or the global market.
    /// Cathedral P3.5 — bilateral routes use <see cref="PartnerCityId"/> ≥ 0 + <see cref="FreightMonths"/>.
    /// </summary>
    public struct TradeRoute
    {
        /// <summary>Partner city ID. -1 = global market (anonymous buyer/seller); ≥ 0 = bilateral NPC/partner.</summary>
        public int PartnerCityId;

        /// <summary>Which good is traded on this route.</summary>
        public Good GoodType;

        /// <summary>Monthly quantity traded (positive = export, negative = import).</summary>
        public float Quantity;

        /// <summary>Agreed price per unit for the duration of the contract.</summary>
        public float AgreedPrice;

        /// <summary>Total contract duration in months.</summary>
        public int DurationMonths;

        /// <summary>Months remaining on this contract.</summary>
        public int RemainingMonths;

        /// <summary>
        /// Freight lag in months (1 = settles fully each month). Bilateral routes stretch
        /// settlement by partner distance + local delivery congestion.
        /// </summary>
        public int FreightMonths;
    }

    private readonly List<TradeRoute> _routes = new();
    private readonly Random _rng = new(42);

    /// <summary>Active trade routes.</summary>
    public IReadOnlyList<TradeRoute> Routes => _routes;

    /// <summary>Total export value this month.</summary>
    public float MonthlyExportValue { get; private set; }

    /// <summary>Total import cost this month.</summary>
    public float MonthlyImportCost { get; private set; }

    /// <summary>Net trade balance (exports - imports).</summary>
    public float TradeBalance => MonthlyExportValue - MonthlyImportCost;

    /// <summary>Active bilateral routes (PartnerCityId ≥ 0) after last <see cref="ProcessTrade"/> / publish.</summary>
    public int BilateralRouteCount { get; private set; }

    /// <summary>Monthly contract notional for bilateral routes (Σ |qty| × price).</summary>
    public float BilateralTradeValue { get; private set; }

    /// <summary>Mean freight months across bilateral routes (0 when none).</summary>
    public float MeanFreightMonths { get; private set; }

    /// <summary>
    /// Seed monthly export/import totals from a save snapshot (until next <see cref="ProcessTrade"/>).
    /// Non-finite values are treated as 0; negatives clamp to 0.
    /// </summary>
    public void RestoreMonthlyTotals(float exportValue, float importCost)
    {
        MonthlyExportValue = float.IsFinite(exportValue) ? Math.Max(0f, exportValue) : 0f;
        MonthlyImportCost = float.IsFinite(importCost) ? Math.Max(0f, importCost) : 0f;
    }

    /// <summary>
    /// Cathedral P3.5 — freight lag from partner distance + local delivery delay.
    /// Global market (<paramref name="partnerCityId"/> &lt; 0) always settles in 1 month.
    /// </summary>
    public static int ComputeFreightMonths(int partnerCityId, float deliveryDelay)
    {
        if (partnerCityId < 0)
            return 1;

        // NPC towns: base 1–3 months by partner id; congestion adds 0–2.
        int partnerBase = 1 + (Math.Abs(partnerCityId) % 3);
        float delay = float.IsFinite(deliveryDelay) ? Math.Clamp(deliveryDelay, 0f, 1f) : 0f;
        int congestion = (int)MathF.Round(delay * 2f);
        return Math.Clamp(partnerBase + congestion, 1, 5);
    }

    // Import markup: importing goods costs 15% more than the global price
    private const float ImportMarkup = 1.15f;

    // Export discount: exporting goods earns 10% less than the global price
    private const float ExportDiscount = 0.90f;

    // Minimum surplus/deficit ratio to trigger auto-trade
    private const float AutoTradeThreshold = 0.20f;

    // Maximum auto-trade volume per good per month (prevents dumping/flooding)
    private const float MaxAutoTradeVolume = 200f;

    public TradeSystem()
    {
        GlobalPrices = new float[EconomySystem.GoodCount];
        ResetGlobalPrices();
    }

    /// <summary>
    /// Reset global prices to base equilibrium values.
    /// </summary>
    public void ResetGlobalPrices()
    {
        Array.Copy(EconomySystem.BasePrices, GlobalPrices, EconomySystem.GoodCount);
    }

    // =========================================================================
    // Monthly trade processing
    // =========================================================================

    /// <summary>
    /// Process all trade for the current month:
    /// 1. Execute existing trade route contracts
    /// 2. Auto-export surplus goods
    /// 3. Auto-import deficit goods
    /// 4. Apply random global price drift
    /// 5. Decrement route durations, remove expired routes
    /// </summary>
    public void ProcessTrade(WorldState state, EconomySystem economy)
    {
        MonthlyExportValue = 0f;
        MonthlyImportCost = 0f;

        // Step 1: Execute trade routes
        ExecuteTradeRoutes(state);

        // Step 2-3: Auto-trade based on surplus/deficit
        AutoTrade(state, economy);

        // Step 4: Global price drift (small random walk)
        ApplyPriceDrift();

        // Step 5: Clean up expired routes
        TickRoutes();

        // Step 6: Cathedral P3.5 — publish bilateral route metrics for snapshot / HUD
        PublishBilateralMetrics(state);
    }

    // =========================================================================
    // Trade route management
    // =========================================================================

    /// <summary>
    /// Establish a new trade route with a partner city or the global market.
    /// Returns true if the route was created successfully.
    /// <paramref name="deliveryDelay"/> scales bilateral <see cref="TradeRoute.FreightMonths"/> (0 free-flow … 1 congested).
    /// </summary>
    public bool CreateTradeRoute(int partnerCityId, Good goodType, float monthlyQuantity,
                                  float price, int durationMonths, float deliveryDelay = 0f)
    {
        if (durationMonths <= 0) return false;
        if (monthlyQuantity == 0f) return false;

        _routes.Add(new TradeRoute
        {
            PartnerCityId = partnerCityId,
            GoodType = goodType,
            Quantity = monthlyQuantity,
            AgreedPrice = price,
            DurationMonths = durationMonths,
            RemainingMonths = durationMonths,
            FreightMonths = ComputeFreightMonths(partnerCityId, deliveryDelay),
        });
        return true;
    }

    /// <summary>
    /// Writes bilateral route aggregates onto <see cref="WorldState"/> (and local properties).
    /// </summary>
    public void PublishBilateralMetrics(WorldState state)
    {
        int count = 0;
        float value = 0f;
        float freightSum = 0f;
        foreach (var route in _routes)
        {
            if (route.PartnerCityId < 0) continue;
            count++;
            value += Math.Abs(route.Quantity) * Math.Max(0f, route.AgreedPrice);
            freightSum += Math.Max(1, route.FreightMonths);
        }

        BilateralRouteCount = count;
        BilateralTradeValue = value;
        MeanFreightMonths = count > 0 ? freightSum / count : 0f;

        state.BilateralRouteCount = BilateralRouteCount;
        state.BilateralTradeValue = BilateralTradeValue;
        state.MeanFreightMonths = MeanFreightMonths;
    }

    /// <summary>
    /// Cancel a trade route by index. Returns true if successfully cancelled.
    /// </summary>
    public bool CancelTradeRoute(int routeIndex)
    {
        if (routeIndex < 0 || routeIndex >= _routes.Count) return false;
        _routes.RemoveAt(routeIndex);
        return true;
    }

    // =========================================================================
    // Global price events
    // =========================================================================

    /// <summary>
    /// Apply a global economic event that shifts prices for specific goods.
    /// Events model real-world shocks (oil crises, recessions, tech booms, etc.).
    /// </summary>
    public void ApplyGlobalEvent(string eventType)
    {
        switch (eventType.ToLowerInvariant())
        {
            case "oil_shock":
                // Oil-related goods spike 50-80%
                MultiplyGlobalPrice(Good.CrudeOil, 1.7f);
                MultiplyGlobalPrice(Good.Fuel, 1.6f);
                MultiplyGlobalPrice(Good.NaturalGas, 1.4f);
                MultiplyGlobalPrice(Good.Plastics, 1.3f);
                MultiplyGlobalPrice(Good.Chemicals, 1.2f);
                break;

            case "recession":
                // All goods drop 10-30%, luxury goods hit hardest
                for (int g = 0; g < EconomySystem.GoodCount; g++)
                {
                    float factor = EconomySystem.Elasticity[g] > 0.6f ? 0.70f : 0.85f;
                    GlobalPrices[g] *= factor;
                }
                break;

            case "boom":
                // All goods rise 10-20%
                for (int g = 0; g < EconomySystem.GoodCount; g++)
                {
                    GlobalPrices[g] *= 1.15f;
                }
                break;

            case "tech_boom":
                // Electronics and digital goods spike
                MultiplyGlobalPrice(Good.Electronics, 1.5f);
                MultiplyGlobalPrice(Good.DigitalServices, 1.4f);
                MultiplyGlobalPrice(Good.Batteries, 1.3f);
                MultiplyGlobalPrice(Good.RareEarth, 1.4f);
                break;

            case "food_crisis":
                // Agricultural goods spike
                MultiplyGlobalPrice(Good.Wheat, 2.0f);
                MultiplyGlobalPrice(Good.Livestock, 1.8f);
                MultiplyGlobalPrice(Good.Fish, 1.5f);
                MultiplyGlobalPrice(Good.Food, 1.7f);
                MultiplyGlobalPrice(Good.Flour, 1.6f);
                MultiplyGlobalPrice(Good.Meat, 1.5f);
                break;

            case "housing_boom":
                // Construction materials spike
                MultiplyGlobalPrice(Good.Steel, 1.4f);
                MultiplyGlobalPrice(Good.Lumber, 1.5f);
                MultiplyGlobalPrice(Good.Cement, 1.4f);
                MultiplyGlobalPrice(Good.Bricks, 1.3f);
                MultiplyGlobalPrice(Good.Glass, 1.2f);
                break;

            case "pandemic":
                // Medicine spikes, entertainment crashes
                MultiplyGlobalPrice(Good.Medicine, 2.0f);
                MultiplyGlobalPrice(Good.Healthcare, 1.8f);
                MultiplyGlobalPrice(Good.Chemicals, 1.3f);
                MultiplyGlobalPrice(Good.Entertainment, 0.5f);
                MultiplyGlobalPrice(Good.FinancialServices, 0.8f);
                break;

            case "recovery":
                // Normalize toward base prices (50% of the way)
                for (int g = 0; g < EconomySystem.GoodCount; g++)
                {
                    GlobalPrices[g] = GlobalPrices[g] * 0.5f + EconomySystem.BasePrices[g] * 0.5f;
                }
                break;
        }

        // Clamp all global prices to 10%-1000% of base
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float basePrice = EconomySystem.BasePrices[g];
            GlobalPrices[g] = Math.Clamp(GlobalPrices[g], basePrice * 0.1f, basePrice * 10f);
        }
    }

    // =========================================================================
    // Internal processing
    // =========================================================================

    private void ExecuteTradeRoutes(WorldState state)
    {
        foreach (var route in _routes)
        {
            if (route.RemainingMonths <= 0) continue;

            // Freight stretches monthly settlement (1 = full; 5 = 20% this month).
            int freight = Math.Max(1, route.FreightMonths);
            float fulfillment = 1f / freight;
            float value = Math.Abs(route.Quantity) * route.AgreedPrice * fulfillment;

            if (route.Quantity > 0) // Export
            {
                MonthlyExportValue += value;
                state.CityFunds += (long)value;
            }
            else // Import
            {
                MonthlyImportCost += value;
                state.CityFunds -= (long)value;
            }
        }
    }

    private void AutoTrade(WorldState state, EconomySystem economy)
    {
        // Check each good across all zones for surplus/deficit
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float totalSupply = 0f;
            float totalDemand = 0f;

            for (int z = 0; z < economy.ActiveZoneCount; z++)
            {
                totalSupply += economy.Zones[z].Supply[g];
                totalDemand += economy.Zones[z].Demand[g];
            }

            // Scale to monthly (supply/demand are per-day, trade is per-month)
            totalSupply *= 30f;
            totalDemand *= 30f;

            if (totalSupply < 0.001f && totalDemand < 0.001f) continue;

            float surplus = totalSupply - totalDemand;
            float threshold = Math.Max(totalSupply, totalDemand) * AutoTradeThreshold;

            if (surplus > threshold)
            {
                // Export surplus at discount
                float exportQty = MathF.Min(surplus * 0.5f, MaxAutoTradeVolume);
                float revenue = exportQty * GlobalPrices[g] * ExportDiscount;
                MonthlyExportValue += revenue;
                state.CityFunds += (long)revenue;
            }
            else if (-surplus > threshold)
            {
                // Import to cover deficit at markup
                float importQty = MathF.Min(-surplus * 0.5f, MaxAutoTradeVolume);
                float cost = importQty * GlobalPrices[g] * ImportMarkup;
                MonthlyImportCost += cost;
                state.CityFunds -= (long)cost;

                // Add imported goods to zone 0 supply so the economy can use them
                economy.SetZoneSupply(0, (Good)g,
                    economy.GetZoneSupply(0, (Good)g) + importQty / 30f);
            }
        }
    }

    private void ApplyPriceDrift()
    {
        // Small random walk: each good drifts -2% to +2% per month
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float drift = ((float)_rng.NextDouble() - 0.5f) * 0.04f; // -2% to +2%
            GlobalPrices[g] *= (1f + drift);

            // Mean-revert toward base price: 5% pull per month
            GlobalPrices[g] = GlobalPrices[g] * 0.95f + EconomySystem.BasePrices[g] * 0.05f;

            // Clamp
            float basePrice = EconomySystem.BasePrices[g];
            GlobalPrices[g] = Math.Clamp(GlobalPrices[g], basePrice * 0.1f, basePrice * 10f);
        }
    }

    private void TickRoutes()
    {
        for (int i = _routes.Count - 1; i >= 0; i--)
        {
            var route = _routes[i];
            route.RemainingMonths--;
            if (route.RemainingMonths <= 0)
            {
                _routes.RemoveAt(i);
            }
            else
            {
                _routes[i] = route;
            }
        }
    }

    private void MultiplyGlobalPrice(Good good, float factor)
    {
        GlobalPrices[(int)good] *= factor;
    }
}
