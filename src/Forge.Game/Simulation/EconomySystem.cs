using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// The 45 tradeable goods in the game economy.
/// Organized into Primary (raw), Secondary (processed), and Tertiary (services/consumer).
/// </summary>
public enum Good : byte
{
    // Primary (raw materials) -- 0-17
    Wheat, Livestock, Fish, Timber, IronOre, Coal, Stone, Clay,
    CrudeOil, NaturalGas, Sand, Copper, Gold, Uranium, RareEarth, Lithium,
    Cotton, Rubber,
    // Secondary (processed) -- 18-34
    Flour, Meat, Lumber, Bricks, Steel, Cement, Fuel, Plastics,
    Chemicals, Textiles, Glass, Electronics, Vehicles, Medicine,
    Furniture, Paper, Batteries,
    // Tertiary (services/consumer) -- 35-44
    Food, ConsumerGoods, Clothing, Electricity, Water, Education,
    Healthcare, Entertainment, FinancialServices, DigitalServices,
    // Sentinel
    COUNT // 45
}

/// <summary>
/// Victoria 3-style snapshot economy with Leontief input-output pricing.
///
/// Each game day, the economy collects buy/sell orders from all production buildings
/// and households, calculates supply/demand ratios per market zone, and resolves
/// prices using a power-law elasticity model:
///
///   price = base_price * (demand / supply) ^ elasticity
///
/// Essential goods (food, water) are price-inelastic (elasticity 0.3),
/// luxury goods (gold, entertainment) are elastic (0.8-1.0).
///
/// Market zones divide the city into 8-16 regions based on transport connectivity.
/// Goods flow between zones with a transport cost penalty.
/// </summary>
public sealed class EconomySystem
{
    public const int GoodCount = (int)Good.COUNT;
    public const int MaxMarketZones = 16;

    /// <summary>Base transport cost as a fraction of source-zone price (5%).</summary>
    private const float BaseTransportCostFraction = 0.05f;

    /// <summary>Quantity lost in transit (5%).</summary>
    private const float TransportQuantityLoss = 0.05f;

    /// <summary>
    /// Precomputed zone-pair trade friction for a 4×4 market grid (16 zones).
    /// Same zone = 1.0; orthogonal neighbor ≈ 1.05; diagonal ≈ 1.15; farther pairs higher.
    /// </summary>
    public static readonly float[,] TradeCoefficients = BuildTradeCoefficients();

    private static float[,] BuildTradeCoefficients()
    {
        const int grid = 4;
        var matrix = new float[MaxMarketZones, MaxMarketZones];
        for (int za = 0; za < MaxMarketZones; za++)
        {
            int ax = za % grid;
            int ay = za / grid;
            for (int zb = 0; zb < MaxMarketZones; zb++)
            {
                int bx = zb % grid;
                int by = zb / grid;
                matrix[za, zb] = FrictionFromOffsets(Math.Abs(ax - bx), Math.Abs(ay - by));
            }
        }

        return matrix;
    }

    private static float FrictionFromOffsets(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return 1.0f;
        int manhattan = dx + dy;
        if (manhattan == 1) return 1.05f;
        if (dx == 1 && dy == 1) return 1.15f;
        return 1.15f + 0.08f * (manhattan - 2);
    }

    /// <summary>Trade friction between two zones on the active sqrt(N)×sqrt(N) grid.</summary>
    public static float GetTradeFriction(int zoneA, int zoneB, int divisions)
    {
        if (zoneA == zoneB) return 1.0f;
        if (zoneA < 0 || zoneA >= MaxMarketZones || zoneB < 0 || zoneB >= MaxMarketZones)
            return TradeCoefficients[0, 1];

        if (divisions == 4)
            return TradeCoefficients[zoneA, zoneB];

        int ax = zoneA % divisions;
        int ay = zoneA / divisions;
        int bx = zoneB % divisions;
        int by = zoneB / divisions;
        return FrictionFromOffsets(Math.Abs(ax - bx), Math.Abs(ay - by));
    }

    // =========================================================================
    // Orders
    // =========================================================================

    /// <summary>
    /// A buy or sell order posted by a building or household for one good.
    /// </summary>
    public struct Order
    {
        public Good GoodType;
        public float Quantity;
        public int BuildingId;
    }

    // =========================================================================
    // Market zones
    // =========================================================================

    /// <summary>
    /// A market zone representing a contiguous area of the city map.
    /// Each zone has independent supply/demand and prices.
    /// </summary>
    public sealed class MarketZone
    {
        public int ZoneId;
        public float[] Prices;
        public float[] Supply;
        public float[] Demand;
        public float[] SupplyDemandRatio;
        public float[] PreviousPrices;

        public MarketZone(int zoneId)
        {
            ZoneId = zoneId;
            Prices = new float[GoodCount];
            Supply = new float[GoodCount];
            Demand = new float[GoodCount];
            SupplyDemandRatio = new float[GoodCount];
            PreviousPrices = new float[GoodCount];

            // Initialize prices to base prices
            for (int g = 0; g < GoodCount; g++)
            {
                Prices[g] = BasePrices[g];
                PreviousPrices[g] = BasePrices[g];
            }
        }
    }

    /// <summary>Per-good imbalance row for WASM / web HUD export.</summary>
    public readonly record struct GoodImbalanceEntry(byte GoodId, string Name, float Magnitude);

    /// <summary>
    /// Result of a "what-if" building placement prediction.
    /// </summary>
    public struct MarketImpact
    {
        public float[] PriceChanges;
        public float ProjectedRevenue;
        public float ProjectedCost;
        public float NetProfit;
        public int JobsCreated;
    }

    // =========================================================================
    // State
    // =========================================================================

    private readonly MarketZone[] _zones;
    private readonly List<Order> _sellOrders = new();
    private readonly List<Order> _buyOrders = new();
    private readonly ProductionChainRegistry _productionChains;
    private EventBus? _eventBus;

    /// <summary>Active market zones (up to 16).</summary>
    public int ActiveZoneCount { get; private set; } = 1;

    /// <summary>Read-only access to market zones for UI display.</summary>
    public MarketZone[] Zones => _zones;

    /// <summary>The production chain registry used by this economy.</summary>
    public ProductionChainRegistry ProductionChains => _productionChains;

    // =========================================================================
    // Base prices per good (in currency units per unit of goods)
    // =========================================================================

    /// <summary>
    /// Base equilibrium prices for all 45 goods. These represent the price when
    /// supply exactly equals demand. Real prices oscillate around these values.
    /// </summary>
    public static readonly float[] BasePrices = new float[GoodCount]
    {
        // Primary raw materials
        /* Wheat */       2.0f,
        /* Livestock */   8.0f,
        /* Fish */        5.0f,
        /* Timber */      3.0f,
        /* IronOre */     4.0f,
        /* Coal */        3.5f,
        /* Stone */       2.5f,
        /* Clay */        1.5f,
        /* CrudeOil */    6.0f,
        /* NaturalGas */  5.0f,
        /* Sand */        1.0f,
        /* Copper */      7.0f,
        /* Gold */       50.0f,
        /* Uranium */    40.0f,
        /* RareEarth */  35.0f,
        /* Lithium */    25.0f,
        /* Cotton */      3.0f,
        /* Rubber */      4.0f,

        // Secondary processed
        /* Flour */       4.0f,
        /* Meat */       12.0f,
        /* Lumber */      6.0f,
        /* Bricks */      5.0f,
        /* Steel */      10.0f,
        /* Cement */      7.0f,
        /* Fuel */        8.0f,
        /* Plastics */    9.0f,
        /* Chemicals */  11.0f,
        /* Textiles */    7.0f,
        /* Glass */       8.0f,
        /* Electronics */ 30.0f,
        /* Vehicles */   80.0f,
        /* Medicine */   25.0f,
        /* Furniture */  15.0f,
        /* Paper */       5.0f,
        /* Batteries */  20.0f,

        // Tertiary services/consumer
        /* Food */        6.0f,
        /* ConsumerGoods */ 12.0f,
        /* Clothing */   10.0f,
        /* Electricity */  3.0f,
        /* Water */        1.5f,
        /* Education */   15.0f,
        /* Healthcare */  20.0f,
        /* Entertainment */ 10.0f,
        /* FinancialSvc */ 18.0f,
        /* DigitalSvc */  14.0f,
    };

    /// <summary>
    /// Price elasticity per good. Lower values = price-inelastic (essential),
    /// higher values = price-elastic (luxury). Controls how steeply prices respond
    /// to supply/demand imbalances.
    /// </summary>
    public static readonly float[] Elasticity = new float[GoodCount]
    {
        // Primary
        /* Wheat */      0.3f,
        /* Livestock */  0.3f,
        /* Fish */       0.4f,
        /* Timber */     0.5f,
        /* IronOre */    0.5f,
        /* Coal */       0.4f,
        /* Stone */      0.5f,
        /* Clay */       0.5f,
        /* CrudeOil */   0.3f,
        /* NaturalGas */ 0.3f,
        /* Sand */       0.6f,
        /* Copper */     0.5f,
        /* Gold */       0.8f,
        /* Uranium */    0.4f,
        /* RareEarth */  0.5f,
        /* Lithium */    0.5f,
        /* Cotton */     0.5f,
        /* Rubber */     0.5f,

        // Secondary
        /* Flour */      0.3f,
        /* Meat */       0.3f,
        /* Lumber */     0.5f,
        /* Bricks */     0.5f,
        /* Steel */      0.5f,
        /* Cement */     0.5f,
        /* Fuel */       0.3f,
        /* Plastics */   0.5f,
        /* Chemicals */  0.4f,
        /* Textiles */   0.5f,
        /* Glass */      0.5f,
        /* Electronics */ 0.6f,
        /* Vehicles */   0.7f,
        /* Medicine */   0.3f,
        /* Furniture */  0.7f,
        /* Paper */      0.5f,
        /* Batteries */  0.5f,

        // Tertiary
        /* Food */       0.3f,
        /* ConsumerGoods */ 0.6f,
        /* Clothing */   0.5f,
        /* Electricity */ 0.2f,
        /* Water */      0.2f,
        /* Education */  0.4f,
        /* Healthcare */ 0.3f,
        /* Entertainment */ 0.8f,
        /* FinancialSvc */ 0.7f,
        /* DigitalSvc */ 1.0f,
    };

    // =========================================================================
    // RCI demand signals (used by zone growth system)
    // =========================================================================

    /// <summary>Residential demand signal (-1.0 to +1.0). Positive = need more housing.</summary>
    public float ResidentialDemand { get; private set; }

    /// <summary>Commercial demand signal (-1.0 to +1.0). Positive = need more shops.</summary>
    public float CommercialDemand { get; private set; }

    /// <summary>Industrial demand signal (-1.0 to +1.0). Positive = need more industry.</summary>
    public float IndustrialDemand { get; private set; }

    /// <summary>Daily inter-zone goods volume from the last economy tick.</summary>
    public float LastInterZoneTradeVolume { get; private set; }

    /// <summary>Weighted mean friction for inter-zone transfers in the last economy tick.</summary>
    public float LastMeanInterZoneFriction { get; private set; } = 1.0f;

    // =========================================================================
    // Constructor
    // =========================================================================

    public EconomySystem() : this(new ProductionChainRegistry()) { }

    public EconomySystem(ProductionChainRegistry productionChains)
    {
        _productionChains = productionChains;
        _productionChains.LoadChains();

        _zones = new MarketZone[MaxMarketZones];
        for (int i = 0; i < MaxMarketZones; i++)
        {
            _zones[i] = new MarketZone(i);
        }
    }

    /// <summary>
    /// Set the event bus for publishing budget events. Optional; if null, events are not published.
    /// </summary>
    public void SetEventBus(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    // =========================================================================
    // Market zone mapping
    // =========================================================================

    /// <summary>
    /// Determine which market zone a tile belongs to. Zones are a simple grid
    /// subdivision: the map is divided into a sqrt(ActiveZoneCount) x sqrt(ActiveZoneCount) grid.
    /// </summary>
    public int GetMarketZoneForTile(int tileX, int tileY, int worldSize)
    {
        if (ActiveZoneCount <= 1) return 0;

        int divisions = (int)MathF.Ceiling(MathF.Sqrt(ActiveZoneCount));
        int zoneSize = worldSize / divisions;
        if (zoneSize < 1) zoneSize = 1;

        int zx = Math.Min(tileX / zoneSize, divisions - 1);
        int zy = Math.Min(tileY / zoneSize, divisions - 1);
        int zoneId = zy * divisions + zx;
        return Math.Min(zoneId, ActiveZoneCount - 1);
    }

    /// <summary>
    /// Get current supply of a good in a zone. Used by ProductionChain.CalculateProductivity.
    /// </summary>
    public float GetZoneSupply(int zoneId, Good good)
    {
        if (zoneId < 0 || zoneId >= ActiveZoneCount) return 0f;
        return _zones[zoneId].Supply[(int)good];
    }

    /// <summary>
    /// Get current price of a good in a zone. Used by UI and trade system.
    /// </summary>
    public float GetPrice(int zoneId, Good good)
    {
        if (zoneId < 0 || zoneId >= ActiveZoneCount) return BasePrices[(int)good];
        return _zones[zoneId].Prices[(int)good];
    }

    /// <summary>
    /// Get average price across all zones. Used for global market comparisons.
    /// </summary>
    public float GetAveragePrice(Good good)
    {
        float sum = 0f;
        for (int z = 0; z < ActiveZoneCount; z++)
            sum += _zones[z].Prices[(int)good];
        return sum / ActiveZoneCount;
    }

    /// <summary>
    /// City-wide supply/demand imbalance per good for HUD export.
    /// Shortages rank by demand − supply; surpluses by supply − demand.
    /// </summary>
    public (GoodImbalanceEntry[] Shortages, GoodImbalanceEntry[] Surpluses) GetTopImbalances(int topN = 5)
    {
        const float MinActivity = 0.01f;
        const float MinDelta = 0.01f;
        topN = Math.Clamp(topN, 1, GoodCount);

        var shortages = new List<GoodImbalanceEntry>(topN);
        var surpluses = new List<GoodImbalanceEntry>(topN);

        for (int g = 0; g < GoodCount; g++)
        {
            float totalSupply = 0f;
            float totalDemand = 0f;
            for (int z = 0; z < ActiveZoneCount; z++)
            {
                totalSupply += _zones[z].Supply[g];
                totalDemand += _zones[z].Demand[g];
            }

            if (totalSupply + totalDemand < MinActivity) continue;

            float delta = totalDemand - totalSupply;
            var good = (Good)g;
            if (delta > MinDelta)
                shortages.Add(new GoodImbalanceEntry((byte)good, good.ToString(), delta));
            else if (delta < -MinDelta)
                surpluses.Add(new GoodImbalanceEntry((byte)good, good.ToString(), -delta));
        }

        shortages.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));
        surpluses.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));

        if (shortages.Count > topN) shortages.RemoveRange(topN, shortages.Count - topN);
        if (surpluses.Count > topN) surpluses.RemoveRange(topN, surpluses.Count - topN);

        return (shortages.ToArray(), surpluses.ToArray());
    }

    /// <summary>
    /// Aggregate shortage pressure across all active goods, normalized 0–1.
    /// Sum of positive (demand − supply) divided by total market activity.
    /// </summary>
    public float ComputeShortageIndex()
    {
        const float MinActivity = 0.01f;
        float totalShortage = 0f;
        float totalActivity = 0f;

        for (int g = 0; g < GoodCount; g++)
        {
            float totalSupply = 0f;
            float totalDemand = 0f;
            for (int z = 0; z < ActiveZoneCount; z++)
            {
                totalSupply += _zones[z].Supply[g];
                totalDemand += _zones[z].Demand[g];
            }

            float activity = totalSupply + totalDemand;
            if (activity < MinActivity) continue;

            totalActivity += activity;
            float delta = totalDemand - totalSupply;
            if (delta > 0f) totalShortage += delta;
        }

        if (totalActivity < MinActivity) return 0f;
        return Math.Clamp(totalShortage / totalActivity, 0f, 1f);
    }

    /// <summary>
    /// Aggregate surplus pressure across all active goods, normalized 0–1.
    /// Sum of positive (supply − demand) divided by total market activity.
    /// </summary>
    public float ComputeSurplusIndex()
    {
        const float MinActivity = 0.01f;
        float totalSurplus = 0f;
        float totalActivity = 0f;

        for (int g = 0; g < GoodCount; g++)
        {
            float totalSupply = 0f;
            float totalDemand = 0f;
            for (int z = 0; z < ActiveZoneCount; z++)
            {
                totalSupply += _zones[z].Supply[g];
                totalDemand += _zones[z].Demand[g];
            }

            float activity = totalSupply + totalDemand;
            if (activity < MinActivity) continue;

            totalActivity += activity;
            float delta = totalSupply - totalDemand;
            if (delta > 0f) totalSurplus += delta;
        }

        if (totalActivity < MinActivity) return 0f;
        return Math.Clamp(totalSurplus / totalActivity, 0f, 1f);
    }

    /// <summary>
    /// Persist top imbalances and scalar indices onto world state after a daily economy tick.
    /// </summary>
    public void PublishImbalancesTo(WorldState state, int topN = WorldState.MaxTopGoodImbalances)
    {
        topN = Math.Clamp(topN, 1, WorldState.MaxTopGoodImbalances);
        var (shortages, surpluses) = GetTopImbalances(topN);

        state.TopShortageCount = shortages.Length;
        for (int i = 0; i < shortages.Length; i++)
        {
            state.TopShortageGoodIds[i] = shortages[i].GoodId;
            state.TopShortageScores[i] = shortages[i].Magnitude;
        }

        for (int i = shortages.Length; i < WorldState.MaxTopGoodImbalances; i++)
        {
            state.TopShortageGoodIds[i] = 0;
            state.TopShortageScores[i] = 0f;
        }

        state.TopSurplusCount = surpluses.Length;
        for (int i = 0; i < surpluses.Length; i++)
        {
            state.TopSurplusGoodIds[i] = surpluses[i].GoodId;
            state.TopSurplusScores[i] = surpluses[i].Magnitude;
        }

        for (int i = surpluses.Length; i < WorldState.MaxTopGoodImbalances; i++)
        {
            state.TopSurplusGoodIds[i] = 0;
            state.TopSurplusScores[i] = 0f;
        }

        state.GoodsShortageIndex = ComputeShortageIndex();
        state.GoodsSurplusIndex = ComputeSurplusIndex();
        state.InterZoneTradeVolume = LastInterZoneTradeVolume;
        state.MeanInterZoneFriction = LastMeanInterZoneFriction;
    }

    // =========================================================================
    // Daily economy tick
    // =========================================================================

    /// <summary>
    /// Main economy simulation tick, called once per game day.
    ///
    /// Steps:
    /// 1. Collect sell orders from production buildings
    /// 2. Collect buy orders from production buildings and households
    /// 3. Calculate supply/demand ratio per zone per good
    /// 4. Price = base_price * (demand/supply)^elasticity
    /// 5. Resolve transactions (pay sellers, charge buyers)
    /// 6. Update household wealth
    /// 7. Calculate RCI demand signals
    /// 8. Publish BudgetChangedEvent
    /// </summary>
    public void DailyTick(WorldState state, double dt)
    {
        // Update zone count based on population thresholds
        UpdateZoneCount(state.Population);

        // Step 1-2: Collect orders
        CollectOrders(state);

        // Step 3: Aggregate orders into zone supply/demand
        AggregateOrdersIntoZones(state);

        // Step 4: Calculate prices using supply/demand elasticity model
        CalculatePrices();

        // Step 5: Resolve transactions
        ResolveTransactions(state);

        // Step 6: Update household wealth
        UpdateHouseholdWealth(state);

        // Step 7: Calculate RCI demand signals
        CalculateRCIDemand(state);

        // Step 8: Publish event
        _eventBus?.Publish(new BudgetChangedEvent { NewBalance = state.CityFunds });
    }

    /// <summary>
    /// Monthly economy tick: tax collection, loan interest, budget balancing.
    /// </summary>
    public void MonthlyTick(WorldState state, double dt)
    {
        // Handled by BudgetSystem if available; this is kept for backward compatibility
        // with IronAndOakGame wiring
    }

    // =========================================================================
    // Step 1-2: Collect orders
    // =========================================================================

    private void CollectOrders(WorldState state)
    {
        _sellOrders.Clear();
        _buyOrders.Clear();

        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != 1) continue; // Only operational buildings

            ushort typeId = buildings.TypeId[i];
            var defOpt = _productionChains.GetDef(typeId);
            if (defOpt == null) continue;

            var def = defOpt.Value;
            float productivity = _productionChains.CalculateProductivity(state, i, this);

            // Sell orders: output goods scaled by productivity
            for (int g = 0; g < def.OutputGoods.Length; g++)
            {
                float qty = def.OutputQuantities[g] * productivity / 30f; // monthly -> daily
                if (qty > 0f)
                {
                    _sellOrders.Add(new Order
                    {
                        GoodType = def.OutputGoods[g],
                        Quantity = qty,
                        BuildingId = i,
                    });
                }
            }

            // Buy orders: input goods (always full demand regardless of productivity,
            // so we model the unfulfilled demand driving prices up)
            for (int g = 0; g < def.InputGoods.Length; g++)
            {
                float qty = def.InputQuantities[g] / 30f; // monthly -> daily
                if (qty > 0f)
                {
                    _buyOrders.Add(new Order
                    {
                        GoodType = def.InputGoods[g],
                        Quantity = qty,
                        BuildingId = i,
                    });
                }
            }
        }

        // Household consumption: each household demands food, water, electricity,
        // and optionally consumer goods, clothing, entertainment based on wealth
        var households = state.Households;
        for (int i = 0; i < households.Capacity; i++)
        {
            if (!households.IsActive(i)) continue;

            int members = Math.Max(1, (int)households.MemberCount[i]);
            float dailyMembers = members / 30f; // monthly -> daily fraction

            // Essential goods: everyone needs food, water, electricity
            _buyOrders.Add(new Order { GoodType = Good.Food, Quantity = 2.0f * dailyMembers, BuildingId = -1 });
            _buyOrders.Add(new Order { GoodType = Good.Water, Quantity = 3.0f * dailyMembers, BuildingId = -1 });
            _buyOrders.Add(new Order { GoodType = Good.Electricity, Quantity = 1.5f * dailyMembers, BuildingId = -1 });

            // Wealth-dependent consumption
            byte wealth = households.WealthLevel[i];
            if (wealth >= 1) // lower class+
            {
                _buyOrders.Add(new Order { GoodType = Good.Clothing, Quantity = 0.5f * dailyMembers, BuildingId = -1 });
            }
            if (wealth >= 2) // middle class+
            {
                _buyOrders.Add(new Order { GoodType = Good.ConsumerGoods, Quantity = 0.8f * dailyMembers, BuildingId = -1 });
                _buyOrders.Add(new Order { GoodType = Good.Healthcare, Quantity = 0.3f * dailyMembers, BuildingId = -1 });
            }
            if (wealth >= 3) // upper class+
            {
                _buyOrders.Add(new Order { GoodType = Good.Entertainment, Quantity = 0.5f * dailyMembers, BuildingId = -1 });
                _buyOrders.Add(new Order { GoodType = Good.Education, Quantity = 0.4f * dailyMembers, BuildingId = -1 });
            }
            if (wealth >= 4) // wealthy
            {
                _buyOrders.Add(new Order { GoodType = Good.FinancialServices, Quantity = 0.3f * dailyMembers, BuildingId = -1 });
                _buyOrders.Add(new Order { GoodType = Good.DigitalServices, Quantity = 0.4f * dailyMembers, BuildingId = -1 });
                _buyOrders.Add(new Order { GoodType = Good.Vehicles, Quantity = 0.01f * dailyMembers, BuildingId = -1 });
            }
        }
    }

    // =========================================================================
    // Step 3: Aggregate into zones
    // =========================================================================

    private void AggregateOrdersIntoZones(WorldState state)
    {
        int worldSize = state.Tiles.Size;

        // Reset zone supply/demand
        for (int z = 0; z < ActiveZoneCount; z++)
        {
            Array.Clear(_zones[z].Supply, 0, GoodCount);
            Array.Clear(_zones[z].Demand, 0, GoodCount);
        }

        // Aggregate sell orders into zone supply
        foreach (var order in _sellOrders)
        {
            int zoneId;
            if (order.BuildingId >= 0 && order.BuildingId < state.Buildings.Capacity &&
                state.Buildings.IsActive(order.BuildingId))
            {
                int tx = state.Buildings.GridX[order.BuildingId];
                int ty = state.Buildings.GridY[order.BuildingId];
                zoneId = GetMarketZoneForTile(tx, ty, worldSize);
            }
            else
            {
                zoneId = 0;
            }
            _zones[zoneId].Supply[(int)order.GoodType] += order.Quantity;
        }

        // Aggregate buy orders into zone demand
        // Household orders go to zone 0 (simplification: households distributed evenly)
        foreach (var order in _buyOrders)
        {
            int zoneId;
            if (order.BuildingId >= 0 && order.BuildingId < state.Buildings.Capacity &&
                state.Buildings.IsActive(order.BuildingId))
            {
                int tx = state.Buildings.GridX[order.BuildingId];
                int ty = state.Buildings.GridY[order.BuildingId];
                zoneId = GetMarketZoneForTile(tx, ty, worldSize);
            }
            else
            {
                // Household orders: distribute across zones proportionally
                // For simplicity, add to zone 0
                zoneId = 0;
            }
            _zones[zoneId].Demand[(int)order.GoodType] += order.Quantity;
        }

        // Cross-zone trade: price-aware greedy matching before local price solve
        if (ActiveZoneCount > 1)
        {
            CrossZoneTrade();
        }
    }

    private void CrossZoneTrade()
    {
        int divisions = (int)MathF.Ceiling(MathF.Sqrt(ActiveZoneCount));
        float totalVolume = 0f;
        float frictionWeightedSum = 0f;

        var deltas = new float[ActiveZoneCount][];
        for (int z = 0; z < ActiveZoneCount; z++)
            deltas[z] = new float[GoodCount];

        var surplusZones = new List<int>(ActiveZoneCount);
        var deficitZones = new List<int>(ActiveZoneCount);
        var surplusAmounts = new float[ActiveZoneCount];
        var deficitAmounts = new float[ActiveZoneCount];
        var tradePairs = new List<(int surplusZone, int deficitZone, float margin)>(ActiveZoneCount * ActiveZoneCount);

        for (int g = 0; g < GoodCount; g++)
        {
            surplusZones.Clear();
            deficitZones.Clear();
            tradePairs.Clear();
            Array.Clear(surplusAmounts, 0, ActiveZoneCount);
            Array.Clear(deficitAmounts, 0, ActiveZoneCount);

            for (int z = 0; z < ActiveZoneCount; z++)
            {
                float imbalance = _zones[z].Supply[g] - _zones[z].Demand[g];
                if (imbalance > 0.001f)
                {
                    surplusZones.Add(z);
                    surplusAmounts[z] = imbalance;
                }
                else if (imbalance < -0.001f)
                {
                    deficitZones.Add(z);
                    deficitAmounts[z] = -imbalance;
                }
            }

            if (surplusZones.Count == 0 || deficitZones.Count == 0)
                continue;

            foreach (int deficitZone in deficitZones)
            {
                float priceB = _zones[deficitZone].Prices[g];
                foreach (int surplusZone in surplusZones)
                {
                    float priceA = _zones[surplusZone].Prices[g];
                    float friction = GetTradeFriction(surplusZone, deficitZone, divisions);
                    float transport = priceA * BaseTransportCostFraction * friction;
                    float margin = priceB - (priceA + transport);
                    if (margin > 0f)
                        tradePairs.Add((surplusZone, deficitZone, margin));
                }
            }

            if (tradePairs.Count == 0)
                continue;

            tradePairs.Sort((a, b) => b.margin.CompareTo(a.margin));

            foreach (var (surplusZone, deficitZone, _) in tradePairs)
            {
                float available = surplusAmounts[surplusZone];
                float needed = deficitAmounts[deficitZone];
                if (available <= 0.001f || needed <= 0.001f)
                    continue;

                float priceA = _zones[surplusZone].Prices[g];
                float priceB = _zones[deficitZone].Prices[g];
                float friction = GetTradeFriction(surplusZone, deficitZone, divisions);
                float transport = priceA * BaseTransportCostFraction * friction;
                if (priceA + transport >= priceB)
                    continue;

                float qty = MathF.Min(available, needed);
                float delivered = qty * (1f - TransportQuantityLoss);

                surplusAmounts[surplusZone] -= qty;
                deficitAmounts[deficitZone] -= qty;
                deltas[surplusZone][g] -= qty;
                deltas[deficitZone][g] += delivered;

                totalVolume += qty;
                frictionWeightedSum += friction * qty;
            }
        }

        for (int z = 0; z < ActiveZoneCount; z++)
        {
            for (int g = 0; g < GoodCount; g++)
            {
                if (MathF.Abs(deltas[z][g]) > 0.0001f)
                    _zones[z].Supply[g] = MathF.Max(0f, _zones[z].Supply[g] + deltas[z][g]);
            }
        }

        LastInterZoneTradeVolume = totalVolume;
        LastMeanInterZoneFriction = totalVolume > 0.001f ? frictionWeightedSum / totalVolume : 1.0f;
    }

    // =========================================================================
    // Step 4: Calculate prices
    // =========================================================================

    private void CalculatePrices()
    {
        for (int z = 0; z < ActiveZoneCount; z++)
        {
            var zone = _zones[z];

            // Save previous prices for smoothing
            Array.Copy(zone.Prices, zone.PreviousPrices, GoodCount);

            for (int g = 0; g < GoodCount; g++)
            {
                float supply = zone.Supply[g];
                float demand = zone.Demand[g];

                // Calculate supply/demand ratio (clamped to avoid infinity/NaN)
                float ratio;
                if (supply < 0.001f && demand < 0.001f)
                {
                    ratio = 1.0f; // No market activity: stable price
                }
                else if (supply < 0.001f)
                {
                    ratio = 100.0f; // Extreme scarcity: demand exists but no supply
                }
                else
                {
                    ratio = demand / supply;
                }
                zone.SupplyDemandRatio[g] = ratio;

                // Price = base_price * (demand/supply)^elasticity
                // When demand > supply, ratio > 1, price rises
                // When supply > demand, ratio < 1, price falls
                float elasticity = Elasticity[g];
                float priceMultiplier = MathF.Pow(ratio, elasticity);

                // Clamp multiplier to prevent extreme price swings (0.1x to 10x base)
                priceMultiplier = Math.Clamp(priceMultiplier, 0.1f, 10.0f);

                float targetPrice = BasePrices[g] * priceMultiplier;

                // Smooth price changes: blend 30% toward target each tick to prevent oscillation
                zone.Prices[g] = zone.PreviousPrices[g] * 0.7f + targetPrice * 0.3f;

                // Floor: prices never go below 10% of base
                zone.Prices[g] = MathF.Max(zone.Prices[g], BasePrices[g] * 0.1f);
            }
        }
    }

    // =========================================================================
    // Step 5: Resolve transactions
    // =========================================================================

    private void ResolveTransactions(WorldState state)
    {
        int worldSize = state.Tiles.Size;

        // Pay sellers: each sell order earns revenue at the local zone price
        foreach (var order in _sellOrders)
        {
            if (order.BuildingId < 0 || order.BuildingId >= state.Buildings.Capacity) continue;
            if (!state.Buildings.IsActive(order.BuildingId)) continue;

            int tx = state.Buildings.GridX[order.BuildingId];
            int ty = state.Buildings.GridY[order.BuildingId];
            int zoneId = GetMarketZoneForTile(tx, ty, worldSize);

            // Revenue = quantity * zone price, but only for the amount actually demanded
            float actualSold = MathF.Min(order.Quantity, _zones[zoneId].Demand[(int)order.GoodType]);
            float revenue = actualSold * _zones[zoneId].Prices[(int)order.GoodType];

            // Add to building revenue (accumulates monthly)
            state.Buildings.Revenue[order.BuildingId] += (int)revenue;
        }

        // Charge buyers (building costs): each buy order costs at local zone price
        foreach (var order in _buyOrders)
        {
            if (order.BuildingId < 0 || order.BuildingId >= state.Buildings.Capacity) continue;
            if (!state.Buildings.IsActive(order.BuildingId)) continue;

            int tx = state.Buildings.GridX[order.BuildingId];
            int ty = state.Buildings.GridY[order.BuildingId];
            int zoneId = GetMarketZoneForTile(tx, ty, worldSize);

            // Cost = quantity * zone price, but only for available supply
            float actualBought = MathF.Min(order.Quantity, _zones[zoneId].Supply[(int)order.GoodType]);
            float cost = actualBought * _zones[zoneId].Prices[(int)order.GoodType];

            // Subtract from building revenue (negative = operating cost)
            state.Buildings.Revenue[order.BuildingId] -= (int)cost;
        }
    }

    // =========================================================================
    // Step 6: Update household wealth
    // =========================================================================

    private void UpdateHouseholdWealth(WorldState state)
    {
        var households = state.Households;
        for (int i = 0; i < households.Capacity; i++)
        {
            if (!households.IsActive(i)) continue;

            int income = households.Income[i];
            int members = Math.Max(1, (int)households.MemberCount[i]);

            // Daily expenses: food + water + electricity at zone 0 prices
            float dailyExpenses =
                2.0f * members * _zones[0].Prices[(int)Good.Food] / 30f +
                3.0f * members * _zones[0].Prices[(int)Good.Water] / 30f +
                1.5f * members * _zones[0].Prices[(int)Good.Electricity] / 30f;

            // Daily income (monthly / 30)
            float dailyIncome = income / 30f;

            // Update savings
            float netDaily = dailyIncome - dailyExpenses;
            households.Savings[i] += (int)netDaily;

            // Update wealth level based on savings thresholds
            int savings = households.Savings[i];
            households.WealthLevel[i] = savings switch
            {
                < 0 => 0,       // poor (in debt)
                < 1000 => 1,    // lower class
                < 5000 => 2,    // middle class
                < 20000 => 3,   // upper class
                _ => 4,         // wealthy
            };

            // Happiness modifier from cost of living
            // If daily expenses > 80% of income, happiness decreases
            if (dailyIncome > 0f)
            {
                float costRatio = dailyExpenses / dailyIncome;
                if (costRatio > 0.8f)
                {
                    // Reduce happiness (0-255 scale)
                    int reduction = (int)((costRatio - 0.8f) * 50f);
                    households.Happiness[i] = (byte)Math.Max(0, households.Happiness[i] - reduction);
                }
                else if (costRatio < 0.4f)
                {
                    // Increase happiness (comfortable)
                    int increase = (int)((0.4f - costRatio) * 10f);
                    households.Happiness[i] = (byte)Math.Min(255, households.Happiness[i] + increase);
                }
            }
        }
    }

    // =========================================================================
    // Step 7: RCI demand signals
    // =========================================================================

    private void CalculateRCIDemand(WorldState state)
    {
        // Residential demand: driven by employment availability vs housing
        // If there are more jobs than houses, residential demand is positive
        int totalJobs = 0;
        int totalCommercial = 0;
        int totalIndustrial = 0;
        int totalResidential = 0;

        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            byte zone = tiles.ZoneType[i];
            switch (zone)
            {
                case 1: // residential low
                case 2: // residential high
                    totalResidential++;
                    break;
                case 3: // commercial
                    totalCommercial++;
                    totalJobs += 4; // rough estimate: 4 jobs per commercial tile
                    break;
                case 4: // industrial
                    totalIndustrial++;
                    totalJobs += 6; // rough estimate: 6 jobs per industrial tile
                    break;
                case 5: // office
                    totalCommercial++;
                    totalJobs += 8;
                    break;
            }
        }

        int population = state.Population;

        // R: need housing if population is close to residential capacity
        int housingCapacity = totalResidential * 4; // rough: 4 people per residential tile
        if (housingCapacity > 0)
            ResidentialDemand = Math.Clamp((float)population / housingCapacity - 0.7f, -1f, 1f);
        else
            ResidentialDemand = population > 0 ? 1.0f : 0.0f;

        // C: need commerce if population outpaces commercial
        if (totalCommercial > 0)
            CommercialDemand = Math.Clamp((float)population / (totalCommercial * 20) - 0.5f, -1f, 1f);
        else
            CommercialDemand = population > 10 ? 1.0f : 0.0f;

        // I: need industry if commercial demand exists but goods are scarce
        float avgFoodSupply = _zones[0].Supply[(int)Good.Food];
        float avgFoodDemand = _zones[0].Demand[(int)Good.Food];
        if (avgFoodDemand > 0.001f)
            IndustrialDemand = Math.Clamp(1.0f - avgFoodSupply / avgFoodDemand, -1f, 1f);
        else
            IndustrialDemand = totalIndustrial < 2 && population > 20 ? 0.5f : 0.0f;
    }

    // =========================================================================
    // Zone count management
    // =========================================================================

    private void UpdateZoneCount(int population)
    {
        // Scale market zones with population:
        // <500: 1 zone, 500-2000: 4 zones, 2000-10000: 9 zones, >10000: 16 zones
        int newCount = population switch
        {
            < 500 => 1,
            < 2000 => 4,
            < 10000 => 9,
            _ => 16,
        };

        if (newCount != ActiveZoneCount)
        {
            ActiveZoneCount = newCount;
        }
    }

    // =========================================================================
    // What-if prediction
    // =========================================================================

    /// <summary>
    /// Predict the market impact of placing a new building at (tileX, tileY).
    /// Creates a temporary copy of zone data, adds the building's orders,
    /// and calculates the resulting price changes.
    /// Does NOT modify actual game state.
    /// </summary>
    public MarketImpact PredictBuildingImpact(WorldState state, ushort buildingTypeId, int tileX, int tileY)
    {
        var defOpt = _productionChains.GetDef(buildingTypeId);
        if (defOpt == null)
        {
            return new MarketImpact
            {
                PriceChanges = new float[GoodCount],
                ProjectedRevenue = 0f,
                ProjectedCost = 0f,
                NetProfit = 0f,
                JobsCreated = 0,
            };
        }

        var def = defOpt.Value;
        int zoneId = GetMarketZoneForTile(tileX, tileY, state.Tiles.Size);
        var zone = _zones[zoneId];

        // Snapshot current prices
        float[] originalPrices = new float[GoodCount];
        Array.Copy(zone.Prices, originalPrices, GoodCount);

        // Calculate hypothetical supply/demand with the new building
        float[] hypotheticalSupply = new float[GoodCount];
        float[] hypotheticalDemand = new float[GoodCount];
        Array.Copy(zone.Supply, hypotheticalSupply, GoodCount);
        Array.Copy(zone.Demand, hypotheticalDemand, GoodCount);

        // Add new building's daily output to supply
        for (int g = 0; g < def.OutputGoods.Length; g++)
        {
            float dailyOutput = def.OutputQuantities[g] / 30f;
            hypotheticalSupply[(int)def.OutputGoods[g]] += dailyOutput;
        }

        // Add new building's daily input to demand
        for (int g = 0; g < def.InputGoods.Length; g++)
        {
            float dailyInput = def.InputQuantities[g] / 30f;
            hypotheticalDemand[(int)def.InputGoods[g]] += dailyInput;
        }

        // Calculate hypothetical prices
        float[] hypotheticalPrices = new float[GoodCount];
        for (int g = 0; g < GoodCount; g++)
        {
            float supply = hypotheticalSupply[g];
            float demand = hypotheticalDemand[g];
            float ratio;
            if (supply < 0.001f && demand < 0.001f) ratio = 1.0f;
            else if (supply < 0.001f) ratio = 100.0f;
            else ratio = demand / supply;

            float multiplier = MathF.Pow(ratio, Elasticity[g]);
            multiplier = Math.Clamp(multiplier, 0.1f, 10.0f);
            hypotheticalPrices[g] = BasePrices[g] * multiplier;
        }

        // Calculate impact
        float[] priceChanges = new float[GoodCount];
        for (int g = 0; g < GoodCount; g++)
        {
            priceChanges[g] = hypotheticalPrices[g] - originalPrices[g];
        }

        // Project monthly revenue and cost
        float monthlyRevenue = 0f;
        for (int g = 0; g < def.OutputGoods.Length; g++)
        {
            monthlyRevenue += def.OutputQuantities[g] * hypotheticalPrices[(int)def.OutputGoods[g]];
        }

        float monthlyCost = 0f;
        for (int g = 0; g < def.InputGoods.Length; g++)
        {
            monthlyCost += def.InputQuantities[g] * hypotheticalPrices[(int)def.InputGoods[g]];
        }

        return new MarketImpact
        {
            PriceChanges = priceChanges,
            ProjectedRevenue = monthlyRevenue,
            ProjectedCost = monthlyCost,
            NetProfit = monthlyRevenue - monthlyCost,
            JobsCreated = def.WorkersNeeded,
        };
    }

    // =========================================================================
    // Test/debug helpers
    // =========================================================================

    /// <summary>
    /// Directly set active market zone count. For tests and deterministic multi-zone setups.
    /// </summary>
    internal void SetActiveZoneCount(int count)
    {
        ActiveZoneCount = Math.Clamp(count, 1, MaxMarketZones);
    }

    /// <summary>
    /// Execute cross-zone trade using current zone supply/demand. For tests only.
    /// </summary>
    internal void RunCrossZoneTradeOnly()
    {
        if (ActiveZoneCount > 1)
            CrossZoneTrade();
    }

    /// <summary>
    /// Directly set supply for a good in a zone. For testing only.
    /// </summary>
    internal void SetZoneSupply(int zoneId, Good good, float amount)
    {
        if (zoneId >= 0 && zoneId < ActiveZoneCount)
            _zones[zoneId].Supply[(int)good] = amount;
    }

    /// <summary>
    /// Directly set demand for a good in a zone. For testing only.
    /// </summary>
    internal void SetZoneDemand(int zoneId, Good good, float amount)
    {
        if (zoneId >= 0 && zoneId < ActiveZoneCount)
            _zones[zoneId].Demand[(int)good] = amount;
    }

    /// <summary>
    /// Recalculate prices without collecting orders. For testing only.
    /// </summary>
    internal void RecalculatePrices()
    {
        CalculatePrices();
    }
}
