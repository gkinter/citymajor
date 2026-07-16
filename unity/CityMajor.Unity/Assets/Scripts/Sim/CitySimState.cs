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
        public int BuildingCount;
        public int ZonedTiles;

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
    }
}
