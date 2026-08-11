namespace CityMajor.Sim
{
    /// <summary>Household row for citizen panel + pedestrian tint (mirrors web population-l2).</summary>
    public struct HouseholdPreview
    {
        public string Id;
        public int TileX;
        public int TileZ;
        public float Happiness;
        public float CommuteMin;
    }

    /// <summary>
    /// Lightweight sim snapshot for HUD + life layers. Published from Forge.SimCore via CitySimBridge.
    /// </summary>
    public struct CitySimState
    {
        public int Population;
        public int Funds;
        public int MonthlyIncome;
        public int MonthlyExpense;
        public float Approval;
        public float Happiness;
        public float DemandResidential;
        public float DemandCommercial;
        public float DemandIndustrial;
        public float GoodsShortageIndex;
        public float GoodsSurplusIndex;
        public float EmploymentRate;
        public float TradeBalance;
        public float MonthlyExportValue;
        public float MonthlyImportCost;
        public float MeanTrafficDensity;
        public float InterZoneTradeVolume;
        public float MeanInterZoneFriction;
        public int BuildingCount;
        public int ConstructingBuildingCount;
        public int ZonedTiles;
        public float PowerCoverageFraction;
        public float WaterCoverageFraction;
        public float UtilityStressIndex;
        public float BlackoutFraction;
        public float WaterShortageFraction;

        /// <summary>
        /// Cathedral P5.2 — mean fire/EMS response minutes over sampled zoned tiles.
        /// </summary>
        public float MeanEmergencyResponseMinutes;

        /// <summary>Cathedral P2 — mean rent / income burden (0–1+).</summary>
        public float MeanRentBurden;

        /// <summary>Cathedral P2 — residential vacancy fraction (1 = fully vacant).</summary>
        public float ResidentialVacancy;

        /// <summary>Cathedral — 1 − EmploymentRate (0–1).</summary>
        public float UnemploymentRate;

        /// <summary>Cathedral P4 — city-wide car mode share (0–1).</summary>
        public float CarModeShare;

        /// <summary>Cathedral P4 — transit mode share (0–1).</summary>
        public float TransitModeShare;

        /// <summary>Cathedral P4 — walk mode share (0–1).</summary>
        public float WalkModeShare;

        /// <summary>Cathedral P4 / U3.5 — mean home→work commute minutes.</summary>
        public float MeanCommuteMinutes;

        /// <summary>Cathedral P4.1 / U3.5 — fraction of working commuters with valid home+work IDs.</summary>
        public float CommuterCoverage;

        /// <summary>Cathedral P4.2 / U3.5 — mean commute satisfaction (0–1).</summary>
        public float MeanCommuteSatisfaction;

        /// <summary>Cathedral P3 — city-average Food price (0 if economy unavailable).</summary>
        public float FoodAvgPrice;

        /// <summary>Cathedral P3 — city-average Water price.</summary>
        public float WaterAvgPrice;

        /// <summary>Cathedral P3 — city-average Steel price.</summary>
        public float SteelAvgPrice;

        /// <summary>True when anchor goods prices were sampled from EconomySystem.</summary>
        public bool HasGoodsPrices;

        /// <summary>Sim hour 0–24 (from SimSnapshot.TimeOfDay).</summary>
        public float TimeOfDay;

        /// <summary>Cosmetic traffic / audio multiplier (rush curve).</summary>
        public float RushMultiplier;

        public int HouseholdCount;
        public HouseholdPreview[] Households;

        public int LawDefinitionCount;
        public int ActiveLawCount;
        public string SampleLawId;
        public string SampleLawName;
        public bool SampleLawActive;

        /// <summary>Cathedral P6 — live EventSystem count for Herald / ticker.</summary>
        public int ActiveEventCount;
    }
}
