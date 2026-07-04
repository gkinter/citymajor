using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Full city budget system: 11 revenue sources, 14 expense categories, loans, and tax management.
///
/// Called monthly by the simulation loop to collect taxes, pay expenses, and manage debt.
/// Tax rates are adjustable by the player and affect citizen happiness.
/// </summary>
public sealed class BudgetSystem
{
    // =========================================================================
    // Revenue sources (calculated monthly)
    // =========================================================================

    /// <summary>Property tax: land_value * property_tax_rate per tile with buildings.</summary>
    public float PropertyTax { get; private set; }

    /// <summary>Commercial tax: commercial building revenue * commercial_tax_rate.</summary>
    public float CommercialTax { get; private set; }

    /// <summary>Industrial tax: industrial building revenue * industrial_tax_rate.</summary>
    public float IndustrialTax { get; private set; }

    /// <summary>Income tax: employed citizens * average income * income_tax_rate.</summary>
    public float IncomeTax { get; private set; }

    /// <summary>Sales tax: commercial transaction volume * sales_tax_rate.</summary>
    public float SalesTax { get; private set; }

    /// <summary>Transit fares: riders * fare per transit trip.</summary>
    public float TransitFares { get; private set; }

    /// <summary>Parking fees: occupied parking spaces * hourly rate * hours.</summary>
    public float ParkingFees { get; private set; }

    /// <summary>Trade income: export value - import cost from inter-city trade.</summary>
    public float TradeIncome { get; private set; }

    /// <summary>Tourism income: tourists * average spending per tourist.</summary>
    public float TourismIncome { get; private set; }

    /// <summary>Utility sales: power + water sold to citizens and businesses.</summary>
    public float UtilitySales { get; private set; }

    /// <summary>Government grants: era-dependent bonuses and milestone rewards.</summary>
    public float GovernmentGrants { get; private set; }

    /// <summary>Total monthly revenue from all sources.</summary>
    public float TotalRevenue =>
        PropertyTax + CommercialTax + IndustrialTax + IncomeTax + SalesTax +
        TransitFares + ParkingFees + TradeIncome + TourismIncome +
        UtilitySales + GovernmentGrants;

    // =========================================================================
    // Expense categories (calculated monthly)
    // =========================================================================

    /// <summary>Road and bridge maintenance costs.</summary>
    public float RoadMaintenance { get; private set; }

    /// <summary>Transit system operating costs (buses, trains, etc.).</summary>
    public float TransitOperations { get; private set; }

    /// <summary>Power generation operating costs.</summary>
    public float PowerGeneration { get; private set; }

    /// <summary>Water treatment and sewage costs.</summary>
    public float WaterSewage { get; private set; }

    /// <summary>Fire department operating costs.</summary>
    public float FireDepartment { get; private set; }

    /// <summary>Police department operating costs.</summary>
    public float PoliceDepartment { get; private set; }

    /// <summary>Healthcare system operating costs.</summary>
    public float Healthcare { get; private set; }

    /// <summary>Education system operating costs.</summary>
    public float Education { get; private set; }

    /// <summary>Social services (welfare, housing assistance).</summary>
    public float SocialServices { get; private set; }

    /// <summary>Waste management and recycling costs.</summary>
    public float WasteManagement { get; private set; }

    /// <summary>Interest payments on outstanding loans.</summary>
    public float DebtInterest { get; private set; }

    /// <summary>City administration overhead (city hall staff, etc.).</summary>
    public float Administration { get; private set; }

    /// <summary>Research and development funding.</summary>
    public float ResearchFunding { get; private set; }

    /// <summary>Emergency reserve fund contributions.</summary>
    public float EmergencyFund { get; private set; }

    /// <summary>Total monthly expenses across all categories.</summary>
    public float TotalExpenses =>
        RoadMaintenance + TransitOperations + PowerGeneration + WaterSewage +
        FireDepartment + PoliceDepartment + Healthcare + Education +
        SocialServices + WasteManagement + DebtInterest + Administration +
        ResearchFunding + EmergencyFund;

    /// <summary>Net monthly balance (positive = surplus, negative = deficit).</summary>
    public float MonthlyBalance => TotalRevenue - TotalExpenses;

    // =========================================================================
    // Loan state
    // =========================================================================

    /// <summary>Outstanding loan balance. Positive = debt owed.</summary>
    public float LoanBalance { get; set; }

    /// <summary>Current loan interest rate. Increases with debt-to-income ratio.</summary>
    public float LoanInterestRate { get; private set; } = 0.05f;

    /// <summary>True if the city is bankrupt (negative funds and maxed out loans).</summary>
    public bool IsBankrupt { get; private set; }

    // =========================================================================
    // Tax rates (adjustable by player, 0.0-0.20)
    // =========================================================================

    private float _propertyTaxRate = 0.09f;
    private float _commercialTaxRate = 0.10f;
    private float _industrialTaxRate = 0.12f;
    private float _incomeTaxRate = 0.08f;
    private float _salesTaxRate = 0.05f;

    public float PropertyTaxRate
    {
        get => _propertyTaxRate;
        set => _propertyTaxRate = Math.Clamp(value, 0f, 0.20f);
    }

    public float CommercialTaxRate
    {
        get => _commercialTaxRate;
        set => _commercialTaxRate = Math.Clamp(value, 0f, 0.20f);
    }

    public float IndustrialTaxRate
    {
        get => _industrialTaxRate;
        set => _industrialTaxRate = Math.Clamp(value, 0f, 0.20f);
    }

    public float IncomeTaxRate
    {
        get => _incomeTaxRate;
        set => _incomeTaxRate = Math.Clamp(value, 0f, 0.20f);
    }

    public float SalesTaxRate
    {
        get => _salesTaxRate;
        set => _salesTaxRate = Math.Clamp(value, 0f, 0.20f);
    }

    // =========================================================================
    // Event bus
    // =========================================================================

    private EventBus? _eventBus;

    public void SetEventBus(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    // =========================================================================
    // Monthly budget calculation
    // =========================================================================

    /// <summary>
    /// Calculate the complete monthly budget: all revenue sources, all expense categories,
    /// loan interest, and update the city treasury.
    ///
    /// Call this once per game month from the simulation loop.
    /// </summary>
    public void CalculateMonthlyBudget(WorldState state, EconomySystem economy)
    {
        CalculateRevenue(state, economy);
        CalculateExpenses(state);
        CalculateLoans(state);

        // Apply net balance to city funds
        float net = TotalRevenue - TotalExpenses;
        state.CityFunds += (long)net;

        // Update WorldState ledgers for backward compatibility
        UpdateLedgers(state);

        // Sync tax rates from WorldState (player may have changed them via UI)
        state.PropertyTaxRate = _propertyTaxRate;
        state.CommercialTaxRate = _commercialTaxRate;
        state.IndustrialTaxRate = _industrialTaxRate;

        // Check bankruptcy
        IsBankrupt = state.CityFunds < -100_000 && LoanBalance > 500_000;

        // Publish budget event
        _eventBus?.Publish(new BudgetChangedEvent { NewBalance = state.CityFunds });
    }

    // =========================================================================
    // Revenue calculation
    // =========================================================================

    private void CalculateRevenue(WorldState state, EconomySystem economy)
    {
        var tiles = state.Tiles;
        var buildings = state.Buildings;
        var households = state.Households;

        // --- Property Tax ---
        // Sum land value of all tiles with buildings, multiply by tax rate
        float landValueSum = 0f;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] != 0)
                landValueSum += tiles.LandValue[i];
        }
        PropertyTax = landValueSum * _propertyTaxRate * 100f; // scale factor for game balance

        // --- Commercial Tax ---
        float commercialRevenue = 0f;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            byte zoneType = GetBuildingZoneType(tiles, buildings, i);
            if (zoneType == 3 || zoneType == 5) // commercial or office
            {
                commercialRevenue += Math.Max(0, buildings.Revenue[i]);
            }
        }
        CommercialTax = commercialRevenue * _commercialTaxRate;

        // --- Industrial Tax ---
        float industrialRevenue = 0f;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            byte zoneType = GetBuildingZoneType(tiles, buildings, i);
            if (zoneType == 4) // industrial
            {
                industrialRevenue += Math.Max(0, buildings.Revenue[i]);
            }
        }
        IndustrialTax = industrialRevenue * _industrialTaxRate;

        // --- Income Tax ---
        float totalIncome = 0f;
        int employed = 0;
        for (int i = 0; i < households.Capacity; i++)
        {
            if (!households.IsActive(i)) continue;
            if (households.WorkBuildingId[i] != 0) // employed
            {
                totalIncome += households.Income[i];
                employed++;
            }
        }
        IncomeTax = totalIncome * _incomeTaxRate;

        // --- Sales Tax ---
        // Approximate commercial transaction volume from commercial building revenue
        SalesTax = commercialRevenue * _salesTaxRate;

        // --- Transit Fares ---
        // Rough estimate: 10% of employed population uses transit, fare = 2.5 per trip, 40 trips/month
        TransitFares = employed * 0.10f * 2.5f * 40f;

        // --- Parking Fees ---
        // Rough estimate: commercial tiles * 0.5 parking spaces * $3/day * 30 days * occupancy 60%
        int commercialTiles = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.ZoneType[i] == 3) commercialTiles++;
        }
        ParkingFees = commercialTiles * 0.5f * 3f * 30f * 0.6f;

        // --- Trade Income ---
        // Calculated by TradeSystem if present; default to 0
        TradeIncome = 0f;

        // --- Tourism Income ---
        // Rough: happiness and cultural DNA drive tourism
        // tourists = population * happiness * cultural_cosmopolitan_factor * 0.01
        float cosmopolitan = state.CulturalDna.Length > 3 ? Math.Max(0, state.CulturalDna[3]) : 0f;
        float tourists = state.Population * state.Happiness * (0.5f + cosmopolitan) * 0.01f;
        TourismIncome = tourists * 50f; // $50 average spending per tourist

        // --- Utility Sales ---
        // Power and water sold: estimate from population
        UtilitySales = state.Population *
            (economy.GetAveragePrice(Good.Electricity) * 1.5f +
             economy.GetAveragePrice(Good.Water) * 3.0f) / 30f * 30f; // monthly

        // --- Government Grants ---
        // Era-dependent base grant + population milestones
        float eraGrant = state.Era * 500f;
        float populationGrant = (state.Population / 1000) * 200f;
        GovernmentGrants = eraGrant + populationGrant;
    }

    // =========================================================================
    // Expense calculation
    // =========================================================================

    private void CalculateExpenses(WorldState state)
    {
        var tiles = state.Tiles;
        var buildings = state.Buildings;

        // --- Road Maintenance ---
        // Count road tiles, maintenance per tile depends on road level
        int roadTiles = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.RoadFlags[i] != 0) roadTiles++;
        }
        RoadMaintenance = roadTiles * 5f; // $5 per road tile per month

        // --- Transit Operations ---
        TransitOperations = state.Vehicles.Count * 50f; // $50 per vehicle per month

        // --- Power Generation ---
        int powerBuildings = CountBuildingsWithService(buildings, 0x01); // bit 0 = power
        PowerGeneration = powerBuildings * 200f;

        // --- Water & Sewage ---
        int waterBuildings = CountBuildingsWithService(buildings, 0x02); // bit 1 = water
        WaterSewage = waterBuildings * 150f;

        // --- Fire Department ---
        int fireStations = CountBuildingsWithService(buildings, 0x08); // bit 3 = fire
        FireDepartment = fireStations * 500f;

        // --- Police Department ---
        int policeStations = CountBuildingsWithService(buildings, 0x04); // bit 2 = police
        PoliceDepartment = policeStations * 600f;

        // --- Healthcare ---
        int hospitals = CountBuildingsWithService(buildings, 0x10); // bit 4 = health
        Healthcare = hospitals * 800f;

        // --- Education ---
        int schools = CountBuildingsWithService(buildings, 0x20); // bit 5 = education
        Education = schools * 400f;

        // --- Social Services ---
        // Scales with population, higher with lower happiness
        float unhappinessFactor = 1.0f + (1.0f - state.Happiness) * 2.0f;
        SocialServices = state.Population * 0.5f * unhappinessFactor;

        // --- Waste Management ---
        WasteManagement = state.Population * 1.0f; // $1 per citizen per month

        // --- Debt Interest ---
        DebtInterest = LoanBalance * (LoanInterestRate / 12f); // monthly interest

        // --- Administration ---
        // Base cost + per-citizen cost
        Administration = 1000f + state.Population * 0.2f;

        // --- Research Funding ---
        ResearchFunding = state.ResearchRate * 10f;

        // --- Emergency Fund ---
        // 2% of total revenue contributed to emergency reserves
        float revenueExDebt = TotalRevenue; // approximate (doesn't include this field yet)
        EmergencyFund = revenueExDebt * 0.02f;
    }

    // =========================================================================
    // Loan management
    // =========================================================================

    private void CalculateLoans(WorldState state)
    {
        // Sync loan state with WorldState
        LoanBalance = state.LoanBalance;

        // Dynamic interest rate: higher debt-to-income = higher rate
        float annualRevenue = TotalRevenue * 12f;
        float debtRatio = annualRevenue > 0 ? LoanBalance / annualRevenue : 10f;

        LoanInterestRate = debtRatio switch
        {
            < 0.5f => 0.03f,   // Low debt: 3%
            < 1.0f => 0.05f,   // Moderate: 5%
            < 2.0f => 0.08f,   // High: 8%
            < 3.0f => 0.12f,   // Very high: 12%
            _ => 0.18f,        // Extreme: 18% (debt spiral)
        };

        state.LoanInterestRate = LoanInterestRate;
    }

    // =========================================================================
    // Tax happiness modifier
    // =========================================================================

    /// <summary>
    /// Calculate the happiness modifier from current tax rates.
    /// Returns a value between -0.3 (oppressive taxes) and +0.1 (low taxes).
    /// Applied to citizen happiness during population simulation.
    /// </summary>
    public float GetTaxHappinessModifier()
    {
        // Average tax burden across all tax types
        float avgTaxRate = (_propertyTaxRate + _commercialTaxRate + _industrialTaxRate +
                           _incomeTaxRate + _salesTaxRate) / 5f;

        // Sweet spot is around 8-10%. Below = bonus, above = penalty
        // The penalty scales quadratically to punish extreme rates
        if (avgTaxRate <= 0.08f)
        {
            return 0.1f * (1.0f - avgTaxRate / 0.08f); // max +0.1 at 0% taxes
        }
        else if (avgTaxRate <= 0.12f)
        {
            return 0f; // neutral zone
        }
        else
        {
            float excess = avgTaxRate - 0.12f;
            return -excess * excess * 100f; // quadratic penalty, max around -0.3
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static byte GetBuildingZoneType(TileData tiles, BuildingData buildings, int buildingId)
    {
        int x = buildings.GridX[buildingId];
        int y = buildings.GridY[buildingId];
        if (!tiles.InBounds(x, y)) return 0;
        return tiles.ZoneType[tiles.Index(x, y)];
    }

    private static int CountBuildingsWithService(BuildingData buildings, uint serviceFlag)
    {
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & serviceFlag) != 0)
                count++;
        }
        return count;
    }

    private void UpdateLedgers(WorldState state)
    {
        // Map budget categories to the WorldState BudgetLedger for backward compatibility
        var income = state.Income;
        income.Reset();
        income.ResidentialTax = (long)PropertyTax;
        income.CommercialTax = (long)CommercialTax;
        income.IndustrialTax = (long)IndustrialTax;
        income.TransportFees = (long)TransitFares;
        income.ServiceFees = (long)(TourismIncome + GovernmentGrants);
        income.UtilityFees = (long)UtilitySales;

        var expenses = state.Expenses;
        expenses.Reset();
        expenses.PoliceExpense = (long)PoliceDepartment;
        expenses.FireExpense = (long)FireDepartment;
        expenses.HealthExpense = (long)Healthcare;
        expenses.EducationExpense = (long)Education;
        expenses.TransportExpense = (long)(RoadMaintenance + TransitOperations);
        expenses.UtilityExpense = (long)(PowerGeneration + WaterSewage);
        expenses.InfrastructureMaintenance = (long)WasteManagement;
        expenses.LoanPayments = (long)DebtInterest;
        expenses.Miscellaneous = (long)(Administration + SocialServices + ResearchFunding + EmergencyFund);
    }
}
