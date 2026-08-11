using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class TradeSystemTests
{
    private static WorldState CreateTestWorld(int size = 32)
    {
        return new WorldState(size);
    }

    // =========================================================================
    // Global prices initialization
    // =========================================================================

    [Fact]
    public void GlobalPrices_InitializedToBasePrices()
    {
        var trade = new TradeSystem();

        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            Assert.Equal(EconomySystem.BasePrices[g], trade.GlobalPrices[g], 4);
        }
    }

    [Fact]
    public void ResetGlobalPrices_RestoresToBase()
    {
        var trade = new TradeSystem();

        // Modify prices
        trade.GlobalPrices[0] = 999f;
        trade.ResetGlobalPrices();

        Assert.Equal(EconomySystem.BasePrices[0], trade.GlobalPrices[0], 4);
    }

    // =========================================================================
    // Global events
    // =========================================================================

    [Fact]
    public void ApplyGlobalEvent_OilShock_RaisesOilPrices()
    {
        var trade = new TradeSystem();

        float oilBefore = trade.GlobalPrices[(int)Good.CrudeOil];
        trade.ApplyGlobalEvent("oil_shock");
        float oilAfter = trade.GlobalPrices[(int)Good.CrudeOil];

        Assert.True(oilAfter > oilBefore,
            $"Oil shock should raise crude oil price. Before={oilBefore}, After={oilAfter}");
    }

    [Fact]
    public void ApplyGlobalEvent_OilShock_AlsoRaisesFuel()
    {
        var trade = new TradeSystem();

        float fuelBefore = trade.GlobalPrices[(int)Good.Fuel];
        trade.ApplyGlobalEvent("oil_shock");
        float fuelAfter = trade.GlobalPrices[(int)Good.Fuel];

        Assert.True(fuelAfter > fuelBefore,
            $"Oil shock should raise fuel price. Before={fuelBefore}, After={fuelAfter}");
    }

    [Fact]
    public void ApplyGlobalEvent_Recession_LowersMostPrices()
    {
        var trade = new TradeSystem();

        float[] before = new float[EconomySystem.GoodCount];
        global::System.Array.Copy(trade.GlobalPrices, before, EconomySystem.GoodCount);

        trade.ApplyGlobalEvent("recession");

        int lowered = 0;
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            if (trade.GlobalPrices[g] < before[g]) lowered++;
        }

        Assert.True(lowered > EconomySystem.GoodCount / 2,
            $"Recession should lower most prices. Only {lowered} out of {EconomySystem.GoodCount} were lowered");
    }

    [Fact]
    public void ApplyGlobalEvent_FoodCrisis_RaisesFoodPrices()
    {
        var trade = new TradeSystem();

        float wheatBefore = trade.GlobalPrices[(int)Good.Wheat];
        float foodBefore = trade.GlobalPrices[(int)Good.Food];

        trade.ApplyGlobalEvent("food_crisis");

        Assert.True(trade.GlobalPrices[(int)Good.Wheat] > wheatBefore);
        Assert.True(trade.GlobalPrices[(int)Good.Food] > foodBefore);
    }

    [Fact]
    public void ApplyGlobalEvent_Pandemic_SpikesMedicineCrashesEntertainment()
    {
        var trade = new TradeSystem();

        float medBefore = trade.GlobalPrices[(int)Good.Medicine];
        float entBefore = trade.GlobalPrices[(int)Good.Entertainment];

        trade.ApplyGlobalEvent("pandemic");

        Assert.True(trade.GlobalPrices[(int)Good.Medicine] > medBefore,
            "Pandemic should spike medicine prices");
        Assert.True(trade.GlobalPrices[(int)Good.Entertainment] < entBefore,
            "Pandemic should crash entertainment prices");
    }

    [Fact]
    public void ApplyGlobalEvent_Recovery_NormalizesTowardBase()
    {
        var trade = new TradeSystem();

        // First apply a shock
        trade.ApplyGlobalEvent("oil_shock");
        float oilShocked = trade.GlobalPrices[(int)Good.CrudeOil];

        // Then recover
        trade.ApplyGlobalEvent("recovery");
        float oilRecovered = trade.GlobalPrices[(int)Good.CrudeOil];

        float basePrice = EconomySystem.BasePrices[(int)Good.CrudeOil];

        // After recovery, price should be closer to base than the shocked price
        float distanceBeforeRecovery = global::System.Math.Abs(oilShocked - basePrice);
        float distanceAfterRecovery = global::System.Math.Abs(oilRecovered - basePrice);

        Assert.True(distanceAfterRecovery < distanceBeforeRecovery,
            $"Recovery should move prices toward base. Before={distanceBeforeRecovery}, After={distanceAfterRecovery}");
    }

    [Fact]
    public void ApplyGlobalEvent_PricesClampedToRange()
    {
        var trade = new TradeSystem();

        // Apply multiple booms to try to push prices very high
        for (int i = 0; i < 10; i++)
            trade.ApplyGlobalEvent("tech_boom");

        // All prices should stay within 0.1x - 10x of base
        for (int g = 0; g < EconomySystem.GoodCount; g++)
        {
            float basePrice = EconomySystem.BasePrices[g];
            Assert.True(trade.GlobalPrices[g] >= basePrice * 0.1f,
                $"Price for {(Good)g} below floor: {trade.GlobalPrices[g]}");
            Assert.True(trade.GlobalPrices[g] <= basePrice * 10f,
                $"Price for {(Good)g} above ceiling: {trade.GlobalPrices[g]}");
        }
    }

    // =========================================================================
    // Trade routes
    // =========================================================================

    [Fact]
    public void CreateTradeRoute_ValidParams_CreatesRoute()
    {
        var trade = new TradeSystem();

        bool created = trade.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 12);
        Assert.True(created);
        Assert.Single(trade.Routes);

        var route = trade.Routes[0];
        Assert.Equal(-1, route.PartnerCityId);
        Assert.Equal(Good.Steel, route.GoodType);
        Assert.Equal(50f, route.Quantity);
        Assert.Equal(10f, route.AgreedPrice);
        Assert.Equal(12, route.DurationMonths);
        Assert.Equal(1, route.FreightMonths); // global market
    }

    [Fact]
    public void CreateTradeRoute_BilateralPartner_SetsFreightMonths()
    {
        var trade = new TradeSystem();
        bool created = trade.CreateTradeRoute(1, Good.IronOre, 20f, 8f, 6, deliveryDelay: 1f);
        Assert.True(created);
        Assert.Equal(4, trade.Routes[0].FreightMonths);
    }

    [Fact]
    public void CreateTradeRoute_ZeroQuantity_ReturnsFalse()
    {
        var trade = new TradeSystem();
        bool created = trade.CreateTradeRoute(-1, Good.Steel, 0f, 10f, 12);
        Assert.False(created);
        Assert.Empty(trade.Routes);
    }

    [Fact]
    public void CreateTradeRoute_ZeroDuration_ReturnsFalse()
    {
        var trade = new TradeSystem();
        bool created = trade.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 0);
        Assert.False(created);
    }

    [Fact]
    public void CancelTradeRoute_ValidIndex_Removes()
    {
        var trade = new TradeSystem();
        trade.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 12);

        bool cancelled = trade.CancelTradeRoute(0);
        Assert.True(cancelled);
        Assert.Empty(trade.Routes);
    }

    [Fact]
    public void CancelTradeRoute_InvalidIndex_ReturnsFalse()
    {
        var trade = new TradeSystem();
        Assert.False(trade.CancelTradeRoute(0));
        Assert.False(trade.CancelTradeRoute(-1));
    }

    // =========================================================================
    // Trade processing
    // =========================================================================

    [Fact]
    public void ProcessTrade_EmptyCity_DoesNotThrow()
    {
        var trade = new TradeSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        trade.ProcessTrade(state, economy);
    }

    [Fact]
    public void ProcessTrade_ExportRoute_IncreasesExportValue()
    {
        var trade = new TradeSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create an export route: 50 steel at $10 per unit
        trade.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 6);

        trade.ProcessTrade(state, economy);

        Assert.True(trade.MonthlyExportValue > 0f,
            $"Export route should generate export value. Got {trade.MonthlyExportValue}");
    }

    [Fact]
    public void ProcessTrade_ImportRoute_IncreasesImportCost()
    {
        var trade = new TradeSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create an import route: -50 steel (negative = import) at $10 per unit
        trade.CreateTradeRoute(-1, Good.Steel, -50f, 10f, 6);

        long fundsBefore = state.CityFunds;
        trade.ProcessTrade(state, economy);

        Assert.True(trade.MonthlyImportCost > 0f,
            $"Import route should have import cost. Got {trade.MonthlyImportCost}");
        Assert.True(state.CityFunds < fundsBefore,
            "Importing should reduce city funds");
    }

    [Fact]
    public void ProcessTrade_ExportRoute_ExpiredRoutesRemoved()
    {
        var trade = new TradeSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create a 1-month route
        trade.CreateTradeRoute(-1, Good.Wheat, 100f, 2f, 1);
        Assert.Single(trade.Routes);

        // Process trade -- should execute then remove the expired route
        trade.ProcessTrade(state, economy);

        Assert.Empty(trade.Routes);
    }

    // =========================================================================
    // Trade balance
    // =========================================================================

    [Fact]
    public void TradeBalance_ExportsMinusImports()
    {
        var trade = new TradeSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        trade.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 6);   // export
        trade.CreateTradeRoute(-1, Good.Wheat, -30f, 2f, 6);   // import

        trade.ProcessTrade(state, economy);

        float expected = trade.MonthlyExportValue - trade.MonthlyImportCost;
        Assert.Equal(expected, trade.TradeBalance, 1);
    }
}
