using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Tier-2 tourism attractions stub — BudgetSystem TourismIncome characterization.</summary>
public sealed class TourismAttractionsTests
{
    [Fact]
    public void TourismIncome_EmptyCity_ZeroWithoutAttractions()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = new WorldState(32);
        state.Population = 0;
        state.Happiness = 0f;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.Equal(0, budget.ParkAttractionCount);
        Assert.Equal(0, budget.LandmarkAttractionCount);
        Assert.Equal(0f, budget.TourismIncome, precision: 2);
        Assert.Equal(0, state.TourismAttractionCount);
        Assert.Equal(0f, state.TourismIncome, precision: 2);
    }

    [Fact]
    public void TourismIncome_ParksAndLandmarks_RaiseIncomeAbovePopHappinessOnly()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = new WorldState(32);
        state.Population = 1_000;
        state.Happiness = 0.5f;
        // CulturalDna[3] cosmopolitan left at 0 → base tourists = 1000*0.5*0.5*0.01 = 2.5
        // base income = 2.5 * 50 = 125

        budget.CalculateMonthlyBudget(state, economy);
        float baseOnly = budget.TourismIncome;
        Assert.Equal(125f, baseOnly, precision: 1);

        int park = state.Buildings.Allocate();
        state.Buildings.GridX[park] = 4;
        state.Buildings.GridY[park] = 4;
        state.Buildings.ServiceFlags[park] = TourismAttractions.ServicePark;

        int landmark = state.Buildings.Allocate();
        state.Buildings.GridX[landmark] = 8;
        state.Buildings.GridY[landmark] = 8;
        state.Buildings.ServiceFlags[landmark] = TourismAttractions.ServiceLandmark;

        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
            state.Tiles.ZoneType[state.Tiles.Index(x, y)] = TourismAttractions.ZonePark;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.Equal(2, budget.ParkAttractionCount); // 1 building + 1 zone site
        Assert.Equal(1, budget.LandmarkAttractionCount);
        Assert.Equal(3, budget.TourismAttractionCount);

        float expected = 125f + 120f * TourismAttractions.SpendPerTourist;
        Assert.Equal(expected, budget.TourismIncome, precision: 1);
        Assert.True(budget.TourismIncome > baseOnly);
        Assert.Equal(budget.ParkAttractionCount, state.ParkAttractionCount);
        Assert.Equal(budget.LandmarkAttractionCount, state.LandmarkAttractionCount);
        Assert.Equal(budget.TourismAttractionCount, state.TourismAttractionCount);
        Assert.Equal(budget.TourismIncome, state.TourismIncome, precision: 1);
    }

    [Fact]
    public void AttractionTourists_WeightsLandmarksHigherThanParks()
    {
        Assert.True(TourismAttractions.VisitorsPerLandmark > TourismAttractions.VisitorsPerParkAttraction);
        Assert.Equal(
            TourismAttractions.VisitorsPerParkAttraction + TourismAttractions.VisitorsPerLandmark,
            TourismAttractions.AttractionTourists(1, 1),
            precision: 2);
    }
}
