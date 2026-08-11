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
        public int HomeBuildingId;
        /// <summary>Workplace building id; 0 = unemployed.</summary>
        public int WorkBuildingId;
        /// <summary>Rent / income ratio (0–1+). Cathedral P4.4.</summary>
        public float RentBurden;
    }

    /// <summary>
    /// Aggregated home→work tile pair (mirrors WASM <c>CommuteOdSampleDto</c> / web Economy HUD).
    /// </summary>
    public struct CommuteOdSample
    {
        public int HomeTileX;
        public int HomeTileZ;
        public int WorkTileX;
        public int WorkTileZ;
        public int TripCount;
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

        /// <summary>Cathedral P3.4 — composite goods transport cost pressure (0–1).</summary>
        public float GoodsTransportCostIndex;

        /// <summary>Cathedral P3.5 — mean goods delivery delay (0 free-flow … 1 congested).</summary>
        public float MeanGoodsDeliveryDelay;

        /// <summary>Cathedral P3.5 — active bilateral trade routes (PartnerCityId ≥ 0).</summary>
        public int BilateralRouteCount;

        /// <summary>Cathedral P3.5 — monthly bilateral contract notional (Σ |qty| × price).</summary>
        public float BilateralTradeValue;

        /// <summary>Cathedral P3.5 — mean freight lag in months across bilateral routes.</summary>
        public float MeanFreightMonths;

        /// <summary>SB-3728 — NPC towns on the regional map catalog.</summary>
        public int NpcPartnerCount;

        public int BuildingCount;
        public int ConstructingBuildingCount;
        /// <summary>Cathedral P2.6 — abandoned buildings (visible with abandoned tint).</summary>
        public int AbandonedBuildingCount;
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

        /// <summary>Cathedral P5.3 — hydrant coverage over sampled zoned buildings (0–1).</summary>
        public float HydrantCoverageFraction;

        /// <summary>Cathedral P5.3 — buildings currently burning.</summary>
        public int ActiveFireCount;

        /// <summary>Cathedral P5.4 — mean EMS survival rate from response minutes (0–1).</summary>
        public float MeanEmsSurvivalRate;

        /// <summary>Tier-2 — hospital bed occupancy (occupied / capacity, 0–1).</summary>
        public float HospitalBedOccupancyFraction;

        /// <summary>Tier-2 — free hospital beds for EMS diversion.</summary>
        public int AvailableHospitalBeds;

        /// <summary>Tier-2 — drought / wildfire risk index (0–1).</summary>
        public float WildfireRiskIndex;

        /// <summary>Tier-2 — forest / park / ag tiles currently burning.</summary>
        public int ActiveWildfireTileCount;

        /// <summary>Tier-2 — mean arson risk from crime excess (0–1).</summary>
        public float ArsonRiskIndex;

        /// <summary>Tier-2 — high-crime building-fire cluster / arson ring.</summary>
        public bool ArsonRingActive;

        /// <summary>Tier-2 — active fire lookout towers.</summary>
        public int LookoutTowerCount;

        /// <summary>Tier-2 — aerial firefighting / water-bomber base available.</summary>
        public bool AerialFirefightingAvailable;

        /// <summary>Tier-2 — city fire safety rating 1–10 (insurance driver).</summary>
        public byte FireSafetyRating;

        /// <summary>Tier-2 — insurance premium mult from fire safety rating.</summary>
        public float FireInsurancePremiumMult;

        /// <summary>Tier-2 — mean household education level 0–3.</summary>
        public float MeanEducationLevel;

        /// <summary>Tier-2 — fraction of households with school coverage.</summary>
        public float EducationCoverageFraction;

        /// <summary>Tier-2 — mean household park / exercise access 0–1.</summary>
        public float MeanParkAccess;

        /// <summary>Tier-2 — fraction of households with park access.</summary>
        public float ParkAccessFraction;

        /// <summary>Tier-2 — fraction of households with hospital coverage.</summary>
        public float HealthCoverageFraction;

        /// <summary>Tier-2 — mean household HealthSatisfaction 0–1.</summary>
        public float MeanHealthSatisfaction;

        /// <summary>Tier-2 — fraction of households with police coverage.</summary>
        public float PoliceCoverageFraction;

        /// <summary>Tier-2 — mean tile crime at household homes 0–1.</summary>
        public float MeanCrimeRate;

        /// <summary>Tier-2 — mean household SafetySatisfaction 0–1.</summary>
        public float MeanSafetySatisfaction;

        /// <summary>Tier-2 — fraction of households with waste coverage.</summary>
        public float WasteCoverageFraction;

        /// <summary>Tier-2 — mean tile pollution at household homes 0–1.</summary>
        public float MeanPollution;

        /// <summary>Tier-2 — mean environment score 0–1 ≈ (1 − pollution).</summary>
        public float MeanEnvironmentScore;

        /// <summary>Tier-2 — fraction of households with sewage coverage.</summary>
        public float SewageCoverageFraction;

        /// <summary>Tier-2 — mean water contamination at household homes 0–1.</summary>
        public float MeanWaterContamination;

        /// <summary>Tier-2 — mean water quality 0–1 ≈ (1 − contamination).</summary>
        public float MeanWaterQuality;

        /// <summary>Manning/CSO — mean combined-sewer pipe utilization 0–1+.</summary>
        public float MeanPipeUtilization;

        /// <summary>Manning/CSO — combined-sewer overflow rate 0–1.</summary>
        public float CsoOverflowRate;

        /// <summary>Manning/CSO — mean storm runoff load entering combined sewers 0–1+.</summary>
        public float StormRunoffLoad;

        /// <summary>Tier-2 — fraction of households with internet tier ≥ copper.</summary>
        public float InternetCoverageFraction;

        /// <summary>Tier-2 — mean InternetConnection tier at household homes 0–3.</summary>
        public float MeanInternetTier;

        /// <summary>Tier-2 — mean telecom access 0–1 ≈ mean tier / 3.</summary>
        public float MeanTelecomAccess;

        /// <summary>Tier-2 — park attraction units for tourism.</summary>
        public int ParkAttractionCount;

        /// <summary>Tier-2 — landmark / monument attraction units for tourism.</summary>
        public int LandmarkAttractionCount;

        /// <summary>Tier-2 — park + landmark attraction units.</summary>
        public int TourismAttractionCount;

        /// <summary>Tier-2 — monthly tourism income including attractions.</summary>
        public float TourismIncome;

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

        /// <summary>
        /// Cathedral P4.1 — top home→work tile pairs (limit 16), same sample as WASM DTO.
        /// </summary>
        public CommuteOdSample[] CommuteOdSample;

        /// <summary>Cathedral P3 — city-average Food price (0 if economy unavailable).</summary>
        public float FoodAvgPrice;

        /// <summary>Cathedral P3 — city-average Water price.</summary>
        public float WaterAvgPrice;

        /// <summary>Cathedral P3 — city-average Steel price.</summary>
        public float SteelAvgPrice;

        /// <summary>True when anchor goods prices were sampled from EconomySystem.</summary>
        public bool HasGoodsPrices;

        /// <summary>Cathedral P3.3 — active Leontief market partitions (1–16).</summary>
        public int MarketZoneCount;

        /// <summary>
        /// Cathedral P3.3 — max/min price ratio across partitions for the widest anchor good (≥1).
        /// </summary>
        public float MaxPartitionPriceSpread;

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

        /// <summary>Cathedral P6.1 — traffic edge capacity multiplier from ordinances.</summary>
        public float LawTrafficCapacityMult;

        /// <summary>Cathedral P6.1 — baseline zone spawn demand multiplier.</summary>
        public float LawSpawnDemandMult;

        /// <summary>Cathedral P6.1 — residential spawn multiplier.</summary>
        public float LawResidentialSpawnMult;

        /// <summary>Cathedral P6.1 — industrial spawn multiplier.</summary>
        public float LawIndustrialSpawnMult;

        /// <summary>Cathedral P6.1 — commercial / office spawn multiplier.</summary>
        public float LawCommercialSpawnMult;

        /// <summary>Cathedral P6 — live EventSystem count for Herald / ticker.</summary>
        public int ActiveEventCount;

        /// <summary>Cathedral P6.2 / P7.5 — tax revenue multiplier from active events.</summary>
        public float EventTaxRevenueMult;

        /// <summary>Cathedral P6.2 / P7.5 — immigration multiplier from active events.</summary>
        public float EventImmigrationMult;

        /// <summary>Cathedral P6.2 / P7.5 — commercial spawn multiplier from active events.</summary>
        public float EventCommercialSpawnMult;

        /// <summary>Cathedral P6.2 / P7.5 — productivity / industrial spawn multiplier from active events.</summary>
        public float EventProductivityMult;

        /// <summary>Cathedral P6.2 / P7.5 — research rate multiplier from active events.</summary>
        public float EventResearchMult;

        /// <summary>Cathedral P6.2 / P7.5 — baseline spawn demand multiplier from active events.</summary>
        public float EventSpawnDemandMult;
    }
}
