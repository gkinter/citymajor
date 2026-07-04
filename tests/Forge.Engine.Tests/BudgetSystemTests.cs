using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class BudgetSystemTests
{
    private static WorldState CreateTestWorld(int size = 32)
    {
        return new WorldState(size);
    }

    // =========================================================================
    // Tax rate clamping
    // =========================================================================

    [Fact]
    public void TaxRates_ClampedTo0Through20Percent()
    {
        var budget = new BudgetSystem();

        budget.PropertyTaxRate = -0.5f;
        Assert.Equal(0f, budget.PropertyTaxRate);

        budget.PropertyTaxRate = 0.5f;
        Assert.Equal(0.20f, budget.PropertyTaxRate);

        budget.CommercialTaxRate = 0.15f;
        Assert.Equal(0.15f, budget.CommercialTaxRate);
    }

    [Fact]
    public void TaxRates_DefaultsAreReasonable()
    {
        var budget = new BudgetSystem();

        Assert.True(budget.PropertyTaxRate > 0f && budget.PropertyTaxRate <= 0.20f);
        Assert.True(budget.CommercialTaxRate > 0f && budget.CommercialTaxRate <= 0.20f);
        Assert.True(budget.IndustrialTaxRate > 0f && budget.IndustrialTaxRate <= 0.20f);
        Assert.True(budget.IncomeTaxRate > 0f && budget.IncomeTaxRate <= 0.20f);
        Assert.True(budget.SalesTaxRate > 0f && budget.SalesTaxRate <= 0.20f);
    }

    // =========================================================================
    // Tax happiness modifier
    // =========================================================================

    [Fact]
    public void TaxHappiness_LowTaxes_PositiveModifier()
    {
        var budget = new BudgetSystem();
        budget.PropertyTaxRate = 0.02f;
        budget.CommercialTaxRate = 0.02f;
        budget.IndustrialTaxRate = 0.02f;
        budget.IncomeTaxRate = 0.02f;
        budget.SalesTaxRate = 0.02f;

        float modifier = budget.GetTaxHappinessModifier();
        Assert.True(modifier > 0f, $"Low taxes should give positive modifier, got {modifier}");
    }

    [Fact]
    public void TaxHappiness_ModerateTaxes_NeutralModifier()
    {
        var budget = new BudgetSystem();
        budget.PropertyTaxRate = 0.10f;
        budget.CommercialTaxRate = 0.10f;
        budget.IndustrialTaxRate = 0.10f;
        budget.IncomeTaxRate = 0.10f;
        budget.SalesTaxRate = 0.10f;

        float modifier = budget.GetTaxHappinessModifier();
        Assert.True(modifier >= -0.05f && modifier <= 0.05f,
            $"Moderate taxes should be near-neutral, got {modifier}");
    }

    [Fact]
    public void TaxHappiness_HighTaxes_NegativeModifier()
    {
        var budget = new BudgetSystem();
        budget.PropertyTaxRate = 0.20f;
        budget.CommercialTaxRate = 0.20f;
        budget.IndustrialTaxRate = 0.20f;
        budget.IncomeTaxRate = 0.20f;
        budget.SalesTaxRate = 0.20f;

        float modifier = budget.GetTaxHappinessModifier();
        Assert.True(modifier < 0f, $"High taxes should give negative modifier, got {modifier}");
    }

    // =========================================================================
    // Monthly budget calculation
    // =========================================================================

    [Fact]
    public void CalculateMonthlyBudget_EmptyCity_DoesNotThrow()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        budget.CalculateMonthlyBudget(state, economy);

        // Empty city should have minimal revenue and expenses
        Assert.True(budget.TotalRevenue >= 0f);
        Assert.True(budget.TotalExpenses >= 0f);
    }

    [Fact]
    public void CalculateMonthlyBudget_WithPopulation_GeneratesIncomeTax()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Create employed households
        for (int i = 0; i < 10; i++)
        {
            int hhId = state.Households.Allocate();
            state.Households.MemberCount[hhId] = 3;
            state.Households.Income[hhId] = 3000;
            state.Households.WorkBuildingId[hhId] = 1; // employed (non-zero)
        }
        state.Population = 30;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.True(budget.IncomeTax > 0f,
            $"Employed citizens should generate income tax. Got {budget.IncomeTax}");
    }

    [Fact]
    public void CalculateMonthlyBudget_WithBuildings_GeneratesPropertyTax()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        // Place buildings -- first allocation returns 0 which is the "empty" sentinel,
        // so allocate a dummy first, then the real building
        int dummy = state.Buildings.Allocate();
        int buildingId = state.Buildings.Allocate();
        Assert.True(buildingId > 0, "Building ID must be > 0 for tile lookup to work");
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        state.Buildings.State[buildingId] = 1;

        int tileIdx = state.Tiles.Index(5, 5);
        state.Tiles.BuildingId[tileIdx] = (ushort)buildingId;
        state.Tiles.LandValue[tileIdx] = 0.8f;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.True(budget.PropertyTax > 0f,
            $"Buildings with land value should generate property tax. Got {budget.PropertyTax}");
    }

    [Fact]
    public void CalculateMonthlyBudget_UpdatesCityFunds()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        long initialFunds = state.CityFunds;

        budget.CalculateMonthlyBudget(state, economy);

        float net = budget.TotalRevenue - budget.TotalExpenses;
        long expectedFunds = initialFunds + (long)net;
        Assert.Equal(expectedFunds, state.CityFunds);
    }

    // =========================================================================
    // Loan interest rates
    // =========================================================================

    [Fact]
    public void LoanInterest_LowDebt_LowRate()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        state.LoanBalance = 1000;
        state.Population = 100;

        // Add some income to avoid divide-by-zero
        for (int i = 0; i < 5; i++)
        {
            int hhId = state.Households.Allocate();
            state.Households.MemberCount[hhId] = 3;
            state.Households.Income[hhId] = 5000;
            state.Households.WorkBuildingId[hhId] = 1;
        }

        budget.CalculateMonthlyBudget(state, economy);

        Assert.True(budget.LoanInterestRate <= 0.08f,
            $"Low debt should have low interest. Rate = {budget.LoanInterestRate}");
    }

    // =========================================================================
    // Bankruptcy
    // =========================================================================

    [Fact]
    public void Bankruptcy_NegativeFundsAndHighDebt_IsBankrupt()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        state.CityFunds = -200_000;
        state.LoanBalance = 600_000;
        budget.LoanBalance = 600_000;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.True(budget.IsBankrupt,
            "City with deeply negative funds and high loan balance should be bankrupt");
    }

    [Fact]
    public void Bankruptcy_PositiveFunds_NotBankrupt()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        state.CityFunds = 50_000;
        state.LoanBalance = 0;

        budget.CalculateMonthlyBudget(state, economy);

        Assert.False(budget.IsBankrupt);
    }

    // =========================================================================
    // Revenue totals
    // =========================================================================

    [Fact]
    public void TotalRevenue_SumsAllSources()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        budget.CalculateMonthlyBudget(state, economy);

        float manualSum = budget.PropertyTax + budget.CommercialTax + budget.IndustrialTax +
            budget.IncomeTax + budget.SalesTax + budget.TransitFares + budget.ParkingFees +
            budget.TradeIncome + budget.TourismIncome + budget.UtilitySales + budget.GovernmentGrants;

        Assert.Equal(manualSum, budget.TotalRevenue, 1);
    }

    [Fact]
    public void TotalExpenses_SumsAllCategories()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        budget.CalculateMonthlyBudget(state, economy);

        float manualSum = budget.RoadMaintenance + budget.TransitOperations +
            budget.PowerGeneration + budget.WaterSewage + budget.FireDepartment +
            budget.PoliceDepartment + budget.Healthcare + budget.Education +
            budget.SocialServices + budget.WasteManagement + budget.DebtInterest +
            budget.Administration + budget.ResearchFunding + budget.EmergencyFund;

        Assert.Equal(manualSum, budget.TotalExpenses, 1);
    }

    [Fact]
    public void MonthlyBalance_EqualsRevenueMinusExpenses()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = CreateTestWorld();

        budget.CalculateMonthlyBudget(state, economy);

        float expected = budget.TotalRevenue - budget.TotalExpenses;
        Assert.Equal(expected, budget.MonthlyBalance, 1);
    }
}
