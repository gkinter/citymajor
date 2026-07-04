using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class EconomySystemTests
{
    private static WorldState CreateTestWorld(int size = 32)
    {
        return new WorldState(size);
    }

    // =========================================================================
    // Good enum
    // =========================================================================

    [Fact]
    public void GoodEnum_Has45Goods()
    {
        Assert.Equal(45, (int)Good.COUNT);
    }

    [Fact]
    public void GoodEnum_PrimaryGoodsAreFirst18()
    {
        Assert.Equal(0, (int)Good.Wheat);
        Assert.Equal(17, (int)Good.Rubber);
    }

    [Fact]
    public void GoodEnum_TertiaryGoodsStartAt35()
    {
        Assert.Equal(35, (int)Good.Food);
        Assert.Equal(44, (int)Good.DigitalServices);
    }

    // =========================================================================
    // Base prices and elasticity
    // =========================================================================

    [Fact]
    public void BasePrices_AllPositive()
    {
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            Assert.True(EconomySystem.BasePrices[g] > 0f,
                $"Base price for good {(Good)g} must be positive, was {EconomySystem.BasePrices[g]}");
        }
    }

    [Fact]
    public void Elasticity_AllInRange()
    {
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float e = EconomySystem.Elasticity[g];
            Assert.True(e >= 0.1f && e <= 1.0f,
                $"Elasticity for {(Good)g} should be 0.1-1.0, was {e}");
        }
    }

    [Fact]
    public void Elasticity_EssentialGoodsAreInelastic()
    {
        // Food, water, electricity should have low elasticity (< 0.4)
        Assert.True(EconomySystem.Elasticity[(int)Good.Food] <= 0.3f);
        Assert.True(EconomySystem.Elasticity[(int)Good.Water] <= 0.2f);
        Assert.True(EconomySystem.Elasticity[(int)Good.Electricity] <= 0.2f);
        Assert.True(EconomySystem.Elasticity[(int)Good.Medicine] <= 0.3f);
    }

    [Fact]
    public void Elasticity_LuxuryGoodsAreElastic()
    {
        // Entertainment, digital services should have high elasticity (> 0.6)
        Assert.True(EconomySystem.Elasticity[(int)Good.Entertainment] >= 0.7f);
        Assert.True(EconomySystem.Elasticity[(int)Good.DigitalServices] >= 0.8f);
    }

    // =========================================================================
    // Market zone mapping
    // =========================================================================

    [Fact]
    public void GetMarketZoneForTile_SingleZone_AlwaysReturnsZero()
    {
        var economy = new EconomySystem();
        // ActiveZoneCount defaults to 1
        Assert.Equal(0, economy.GetMarketZoneForTile(0, 0, 256));
        Assert.Equal(0, economy.GetMarketZoneForTile(128, 128, 256));
        Assert.Equal(0, economy.GetMarketZoneForTile(255, 255, 256));
    }

    // =========================================================================
    // Price calculation: supply/demand dynamics
    // =========================================================================

    [Fact]
    public void Prices_ScarcityRaisesPrices()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Set high demand, low supply for wheat in zone 0
        economy.SetZoneDemand(0, Good.Wheat, 100f);
        economy.SetZoneSupply(0, Good.Wheat, 10f);

        economy.RecalculatePrices();

        float wheatPrice = economy.GetPrice(0, Good.Wheat);
        float basePrice = EconomySystem.BasePrices[(int)Good.Wheat];

        // With demand/supply ratio of 10:1, price should be above base
        Assert.True(wheatPrice > basePrice,
            $"Scarcity should raise price. Got {wheatPrice}, base is {basePrice}");
    }

    [Fact]
    public void Prices_OversupplyCrashesPrices()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Set low demand, high supply for steel
        economy.SetZoneDemand(0, Good.Steel, 10f);
        economy.SetZoneSupply(0, Good.Steel, 100f);

        economy.RecalculatePrices();

        float steelPrice = economy.GetPrice(0, Good.Steel);
        float basePrice = EconomySystem.BasePrices[(int)Good.Steel];

        // With supply/demand ratio of 10:1, price should be below base
        Assert.True(steelPrice < basePrice,
            $"Oversupply should lower price. Got {steelPrice}, base is {basePrice}");
    }

    [Fact]
    public void Prices_BalancedMarket_StaysNearBase()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Equal supply and demand
        economy.SetZoneDemand(0, Good.Lumber, 50f);
        economy.SetZoneSupply(0, Good.Lumber, 50f);

        economy.RecalculatePrices();

        float lumberPrice = economy.GetPrice(0, Good.Lumber);
        float basePrice = EconomySystem.BasePrices[(int)Good.Lumber];

        // Price should be very close to base (within 30% due to smoothing)
        float ratio = lumberPrice / basePrice;
        Assert.True(ratio > 0.7f && ratio < 1.3f,
            $"Balanced market should keep price near base. Ratio = {ratio}");
    }

    [Fact]
    public void Prices_NoActivity_StaysAtBase()
    {
        var economy = new EconomySystem();

        // No supply, no demand -> price should remain at base
        economy.RecalculatePrices();

        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float price = economy.GetPrice(0, (Good)g);
            float basePrice = EconomySystem.BasePrices[g];
            // Should be at or very near base (smoothing blends toward base)
            float ratio = price / basePrice;
            Assert.True(ratio > 0.6f && ratio < 1.4f,
                $"No activity: price of {(Good)g} should be near base. Ratio = {ratio}");
        }
    }

    [Fact]
    public void Prices_InelasticGoods_SmallPriceSwings()
    {
        var economy = new EconomySystem();

        // Same 5:1 demand/supply ratio for elastic vs inelastic good
        economy.SetZoneDemand(0, Good.Food, 50f);     // elasticity 0.3
        economy.SetZoneSupply(0, Good.Food, 10f);

        economy.SetZoneDemand(0, Good.Entertainment, 50f); // elasticity 0.8
        economy.SetZoneSupply(0, Good.Entertainment, 10f);

        economy.RecalculatePrices();

        float foodPrice = economy.GetPrice(0, Good.Food);
        float foodBase = EconomySystem.BasePrices[(int)Good.Food];
        float foodRatio = foodPrice / foodBase;

        float entPrice = economy.GetPrice(0, Good.Entertainment);
        float entBase = EconomySystem.BasePrices[(int)Good.Entertainment];
        float entRatio = entPrice / entBase;

        // Entertainment (elastic) should swing more than food (inelastic)
        Assert.True(entRatio > foodRatio,
            $"Elastic good should swing more. Food ratio={foodRatio}, Entertainment ratio={entRatio}");
    }

    [Fact]
    public void Prices_FloorAt10PercentOfBase()
    {
        var economy = new EconomySystem();

        // Extreme oversupply
        economy.SetZoneDemand(0, Good.Gold, 0.001f);
        economy.SetZoneSupply(0, Good.Gold, 10000f);

        economy.RecalculatePrices();

        float goldPrice = economy.GetPrice(0, Good.Gold);
        float goldBase = EconomySystem.BasePrices[(int)Good.Gold];

        Assert.True(goldPrice >= goldBase * 0.1f,
            $"Price should not drop below 10% of base. Got {goldPrice}, floor = {goldBase * 0.1f}");
    }

    // =========================================================================
    // Daily tick integration
    // =========================================================================

    [Fact]
    public void DailyTick_WithNoBuildings_DoesNotThrow()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Should run without error even with empty world
        economy.DailyTick(state, 10.0);
    }

    [Fact]
    public void DailyTick_WithProductionBuilding_GeneratesSupply()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Place a wheat farm (type 100)
        int buildingId = state.Buildings.Allocate();
        Assert.True(buildingId >= 0);
        state.Buildings.TypeId[buildingId] = 100; // Wheat Farm
        state.Buildings.State[buildingId] = 1;     // Operational
        state.Buildings.Condition[buildingId] = 255;
        state.Buildings.Occupants[buildingId] = 8; // Fully staffed
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;

        // Mark tile as having power (wheat farm needs 0.1 MW)
        int tileIdx = state.Tiles.Index(5, 5);
        state.Tiles.PowerGrid[tileIdx] = 1;
        state.Tiles.BuildingId[tileIdx] = (ushort)buildingId;

        economy.DailyTick(state, 10.0);

        // Check zone 0 has wheat supply > 0
        float wheatSupply = economy.GetZoneSupply(0, Good.Wheat);
        Assert.True(wheatSupply > 0f,
            $"Wheat farm should produce wheat supply. Got {wheatSupply}");
    }

    [Fact]
    public void DailyTick_WithHouseholds_GeneratesDemand()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create a household
        int hhId = state.Households.Allocate();
        Assert.True(hhId >= 0);
        state.Households.MemberCount[hhId] = 4;
        state.Households.WealthLevel[hhId] = 2; // middle class
        state.Households.Income[hhId] = 3000;
        state.Population = 4;

        economy.DailyTick(state, 10.0);

        // Food demand should exist in zone 0
        float foodDemand = economy.Zones[0].Demand[(int)Good.Food];
        Assert.True(foodDemand > 0f,
            $"Households should generate food demand. Got {foodDemand}");
    }

    // =========================================================================
    // RCI demand signals
    // =========================================================================

    [Fact]
    public void RCIDemand_EmptyCity_ResidentialIsZero()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();
        state.Population = 0;

        economy.DailyTick(state, 10.0);

        // No population -> no residential demand (or very low)
        Assert.True(economy.ResidentialDemand >= -1f && economy.ResidentialDemand <= 1f);
    }

    // =========================================================================
    // Building impact prediction
    // =========================================================================

    [Fact]
    public void PredictBuildingImpact_SteelMill_ShowsInputCostsAndOutputRevenue()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Predict impact of a steel mill (type 122)
        var impact = economy.PredictBuildingImpact(state, 122, 16, 16);

        // Steel mill should have input costs (iron ore + coal)
        Assert.True(impact.ProjectedCost > 0f,
            $"Steel mill should have input costs. Got {impact.ProjectedCost}");

        // Steel mill should have output revenue (steel)
        Assert.True(impact.ProjectedRevenue > 0f,
            $"Steel mill should have output revenue. Got {impact.ProjectedRevenue}");

        // Steel mill needs 30 workers
        Assert.Equal(30, impact.JobsCreated);
    }

    [Fact]
    public void PredictBuildingImpact_UnknownType_ReturnsZeroImpact()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        var impact = economy.PredictBuildingImpact(state, 9999, 0, 0);

        Assert.Equal(0f, impact.ProjectedRevenue);
        Assert.Equal(0f, impact.ProjectedCost);
        Assert.Equal(0f, impact.NetProfit);
        Assert.Equal(0, impact.JobsCreated);
    }

    [Fact]
    public void PredictBuildingImpact_WheatFarm_NoInputCost()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        var impact = economy.PredictBuildingImpact(state, 100, 5, 5);

        // Wheat farm has no inputs
        Assert.Equal(0f, impact.ProjectedCost);

        // But should have revenue from wheat output
        Assert.True(impact.ProjectedRevenue > 0f);
        Assert.Equal(8, impact.JobsCreated);
    }

    // =========================================================================
    // Emergent behavior: supply chains
    // =========================================================================

    [Fact]
    public void EmergentPricing_WheatShortage_RaisesFlourAndFoodPrices()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create wheat shortage: flour mill demands wheat but no wheat farm exists
        int flourMill = state.Buildings.Allocate();
        state.Buildings.TypeId[flourMill] = 101; // Flour Mill
        state.Buildings.State[flourMill] = 1;
        state.Buildings.Condition[flourMill] = 255;
        state.Buildings.Occupants[flourMill] = 4;
        state.Buildings.GridX[flourMill] = 10;
        state.Buildings.GridY[flourMill] = 10;
        state.Tiles.PowerGrid[state.Tiles.Index(10, 10)] = 1;
        state.Tiles.BuildingId[state.Tiles.Index(10, 10)] = (ushort)flourMill;

        // Run economy for several days to let prices adjust
        for (int day = 0; day < 30; day++)
        {
            economy.DailyTick(state, 10.0);
        }

        // Wheat price should be above base due to unfulfilled demand
        float wheatPrice = economy.GetPrice(0, Good.Wheat);
        float wheatBase = EconomySystem.BasePrices[(int)Good.Wheat];

        Assert.True(wheatPrice > wheatBase,
            $"Wheat shortage should raise price. Got {wheatPrice}, base = {wheatBase}");
    }

    [Fact]
    public void EmergentPricing_OversupplyFromMultipleFarms_LowersPrice()
    {
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create 5 wheat farms with no consumers
        for (int f = 0; f < 5; f++)
        {
            int farmId = state.Buildings.Allocate();
            state.Buildings.TypeId[farmId] = 100;
            state.Buildings.State[farmId] = 1;
            state.Buildings.Condition[farmId] = 255;
            state.Buildings.Occupants[farmId] = 8;
            state.Buildings.GridX[farmId] = f * 4;
            state.Buildings.GridY[farmId] = 0;
            int tileIdx = state.Tiles.Index(f * 4, 0);
            state.Tiles.PowerGrid[tileIdx] = 1;
            state.Tiles.BuildingId[tileIdx] = (ushort)farmId;
        }

        // Run economy for several days
        for (int day = 0; day < 30; day++)
        {
            economy.DailyTick(state, 10.0);
        }

        // Wheat price should drop below base due to oversupply
        float wheatPrice = economy.GetPrice(0, Good.Wheat);
        float wheatBase = EconomySystem.BasePrices[(int)Good.Wheat];

        Assert.True(wheatPrice < wheatBase,
            $"Oversupply should lower wheat price. Got {wheatPrice}, base = {wheatBase}");
    }

    // =========================================================================
    // Average price
    // =========================================================================

    [Fact]
    public void GetAveragePrice_SingleZone_EqualsZonePrice()
    {
        var economy = new EconomySystem();

        float avg = economy.GetAveragePrice(Good.Steel);
        float zone0 = economy.GetPrice(0, Good.Steel);

        // With single zone, average = zone 0 price
        Assert.Equal(zone0, avg, 4);
    }
}
