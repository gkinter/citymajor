using System;
using System.IO;
using CityMajor.Input;
using CityMajor.Net;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Sim
{
    /// <summary>
    /// In-process sim tick (8 Hz). Uses Forge.SimCore.SimHost when the plugin DLL is present.
    /// </summary>
    public sealed class CitySimBridge : MonoBehaviour
    {
        public const float SimTickHz = 8f;
        public const float SimTickSeconds = 1f / SimTickHz;

        public event Action<CitySimState> OnStateChanged;
        public event Action<SimSnapshot> OnSnapshotChanged;
        /// <summary>Fired after a CMJR blob is loaded and the scene state is republished.</summary>
        public event Action OnSaveLoaded;

        public const string DefaultSaveFileName = "citymajor.cmjr";
        public string SaveFilePath => Path.Combine(Application.persistentDataPath, DefaultSaveFileName);

        public CitySimState State { get; private set; }
        public SimSnapshot LatestSnapshot { get; private set; }
        public ServiceCoverageDto[] LatestServiceCoverage { get; private set; } = Array.Empty<ServiceCoverageDto>();
        public UtilityCoverageDto[] LatestUtilityCoverage { get; private set; } = Array.Empty<UtilityCoverageDto>();
        /// <summary>
        /// Cathedral P3.4 / U3.3 — sparse market-zone boundary friction heat for GL overlay.
        /// Empty when economy has ≤1 active market zone or sim core is offline.
        /// </summary>
        public FrictionCorridorDto[] LatestFrictionCorridors { get; private set; } = Array.Empty<FrictionCorridorDto>();
        /// <summary>Active sim/Herald events for EventTicker (housing_crisis, housing_shortage, approval_unrest, …).</summary>
        public ActiveEventDto[] LatestActiveEvents { get; private set; } = Array.Empty<ActiveEventDto>();
        /// <summary>
        /// P4.2 road graph + edge volumes / travel times from <see cref="SimHost.GetTrafficEdgeExport"/>.
        /// Empty when sim core is offline or the graph has no edges.
        /// </summary>
        public RoadGraphSnapshotDto LatestRoadGraph { get; private set; } = new();

        public bool Paused { get; set; }
        public float TimeScale { get; set; } = 1f;

        int _mapSize;
        float _accumulator;
        ZoneGrid _grid;
        SimHost _simHost;
        bool _simCoreReady;

        public bool UsesForgeSimCore => _simCoreReady;

        /// <summary>Top goods shortages/surpluses from live EconomySystem (Cathedral P3).</summary>
        public bool TryGetGoodImbalances(
            out EconomySystem.GoodImbalanceEntry[] shortages,
            out EconomySystem.GoodImbalanceEntry[] surpluses,
            int topN = 5)
        {
            shortages = Array.Empty<EconomySystem.GoodImbalanceEntry>();
            surpluses = Array.Empty<EconomySystem.GoodImbalanceEntry>();
            if (!_simCoreReady || _simHost?.Economy == null)
                return false;

            var (shortRows, surplusRows) = _simHost.Economy.GetTopImbalances(topN);
            shortages = shortRows ?? Array.Empty<EconomySystem.GoodImbalanceEntry>();
            surpluses = surplusRows ?? Array.Empty<EconomySystem.GoodImbalanceEntry>();
            return true;
        }

        /// <summary>Top production vs demand flows for the economy panel (Cathedral P3.2).</summary>
        public EconomySystem.GoodFlowEntry[] GetTopGoodFlows(int topN = 8)
        {
            if (!_simCoreReady || _simHost?.Economy == null)
                return Array.Empty<EconomySystem.GoodFlowEntry>();

            return _simHost.Economy.GetTopGoodFlows(topN)
                   ?? Array.Empty<EconomySystem.GoodFlowEntry>();
        }

        /// <summary>Anchor good min/max prices across market partitions (Cathedral P3.3).</summary>
        public EconomySystem.ZonePriceSpread[] GetAnchorZonePriceSpreads()
        {
            if (!_simCoreReady || _simHost?.Economy == null)
                return Array.Empty<EconomySystem.ZonePriceSpread>();

            return _simHost.Economy.GetAnchorZonePriceSpreads()
                   ?? Array.Empty<EconomySystem.ZonePriceSpread>();
        }

        /// <summary>Council seat faction ids from the latest snapshot (9 seats).</summary>
        public byte[] GetCouncilSeats()
        {
            var seats = LatestSnapshot.CouncilSeats;
            if (seats != null && seats.Length > 0)
                return seats;

            if (_simCoreReady && _simHost?.Politics?.CouncilSeats != null)
                return (byte[])_simHost.Politics.CouncilSeats.Clone();

            return Array.Empty<byte>();
        }

        public void Configure(int mapSize)
        {
            _mapSize = mapSize;
            _simCoreReady = TryInitSimHost(mapSize);

            if (!_simCoreReady)
            {
                State = CreatePlaceholderState();
                Publish();
                Debug.LogError(
                    "[CityMajor] CitySimBridge running placeholder-only — HUD/zones will not match SimHost. " +
                    "Play gate: CityMajor → Run Play Gate Batch Checks (U3.6) or ./scripts/verify-unity-play-gate.sh");
            }
            else
            {
                Debug.Log($"[CityMajor] SimHost ready (map {mapSize}) — Forge.SimCore live");
            }
        }

        public void BindGrid(ZoneGrid grid) => _grid = grid;

        /// <summary>Engine zone type bytes — mirrors web/lib/zone-tiers.ts + Park (8).</summary>
        public static byte EngineZoneType(ZonePaintTool.ZoneKind kind) => kind switch
        {
            ZonePaintTool.ZoneKind.Residential => 1,
            ZonePaintTool.ZoneKind.Commercial => 3,
            ZonePaintTool.ZoneKind.Industrial => 4,
            ZonePaintTool.ZoneKind.Office => 5,
            ZonePaintTool.ZoneKind.Mixed => 6,
            ZonePaintTool.ZoneKind.Agricultural => 7,
            ZonePaintTool.ZoneKind.Park => 8,
            _ => 0,
        };

        /// <param name="density">0 = default low; 1–3 = low / medium / high (SimHost.PaintZone).</param>
        public void PaintZone(int tileX, int tileY, ZonePaintTool.ZoneKind kind, byte density = 0)
        {
            if (_grid != null)
                _grid.SetZone(tileX, tileY, kind);

            if (!_simCoreReady)
            {
                if (_grid != null)
                    NotifyZonesChanged();
                return;
            }

            _simHost.PaintZone(tileX, tileY, EngineZoneType(kind), density);
            PublishFromSimHost();
        }

        /// <param name="bridge">Cathedral P1.3 — mark tile as bridge (graph cost ×1.15).</param>
        /// <param name="tunnel">Cathedral P1.3 — mark tile as tunnel (graph cost ×1.25).</param>
        /// <param name="ramp">Cathedral P1.5 — highway ramp connector (RoadFlags.Ramp).</param>
        public bool PlaceRoad(int tileX, int tileY, byte tier = 1, bool bridge = false, bool tunnel = false, bool ramp = false)
        {
            if (!_simCoreReady)
                return false;

            var ok = _simHost.PlaceRoad(tileX, tileY, tier, bridge, tunnel, ramp);
            if (ok)
                PublishFromSimHost();
            return ok;
        }

        /// <summary>Clears zone, buildings, and roads on tile (Forge desktop bulldoze parity).</summary>
        public void Bulldoze(int tileX, int tileY)
        {
            if (_grid != null)
                _grid.SetZone(tileX, tileY, ZonePaintTool.ZoneKind.None);

            if (!_simCoreReady)
            {
                if (_grid != null)
                    NotifyZonesChanged();
                return;
            }

            _simHost.Bulldoze(tileX, tileY);
            PublishFromSimHost();
        }

        public void NotifyZonesChanged()
        {
            if (_simCoreReady)
            {
                PublishFromSimHost();
                return;
            }

            if (_grid == null)
                return;

            var zoned = 0;
            for (var y = 0; y < _grid.MapSize; y++)
            for (var x = 0; x < _grid.MapSize; x++)
            {
                if (_grid.GetZone(x, y) != ZonePaintTool.ZoneKind.None)
                    zoned++;
            }

            var state = State;
            state.ZonedTiles = zoned;
            state.Population = Mathf.RoundToInt(zoned * 0.8f);
            state.BuildingCount = Mathf.RoundToInt(zoned * 0.12f);
            State = state;
            Publish();
        }

        void Update()
        {
            if (Paused)
                return;

            _accumulator += Time.deltaTime * Mathf.Max(0.25f, TimeScale);
            while (_accumulator >= SimTickSeconds)
            {
                _accumulator -= SimTickSeconds;
                TickSimulation(SimTickSeconds);
            }
        }

        void TickSimulation(float dt)
        {
            if (_simCoreReady)
            {
                _simHost.Tick(dt);
                PublishFromSimHost();
                return;
            }

            // Placeholder pulse when Forge.SimCore.dll is missing.
            var state = State;
            state.Funds += Mathf.RoundToInt((state.MonthlyIncome - state.MonthlyExpense) * dt / 30f);
            State = state;
            Publish();
        }

        bool TryInitSimHost(int mapSize)
        {
            try
            {
                var contentRoot = FindContentRoot();
                var dataPaths = SimDataPaths.FromContentRoot(contentRoot);
                _simHost = new SimHost();
                var useFullTraffic = WasmConfig.UseFullTrafficUnityDefault
                    || string.Equals(
                        Environment.GetEnvironmentVariable("CITYMAJOR_FULL_TRAFFIC"),
                        "1",
                        StringComparison.Ordinal);
                _simHost.Init(mapSize, new SimHostInitOptions
                {
                    UnityModernProfile = true,
                    SkipStarterCity = true,
                    UseFullTraffic = useFullTraffic,
                    TrafficLiteZoneCount = WasmConfig.TrafficLiteZoneCountUnity,
                    DataPaths = dataPaths,
                });
                PublishFromSimHost();
                return true;
            }
            catch (Exception ex)
            {
                // U3.6 fail-fast: placeholder is not a Play gate. Agents/humans must rebuild DLL.
                Debug.LogError(
                    "[CityMajor] Forge.SimCore unavailable — placeholder sim is NOT valid for Play gate / SB-4176. " +
                    "Run ./scripts/build-simcore-for-unity.sh then domain-reload. " +
                    $"Editor: CityMajor → Run Play Gate Batch Checks (U3.6). Detail: {ex.Message}");
                return false;
            }
        }

        static string FindContentRoot()
        {
            // Assets → CityMajor.Unity → unity → repo root
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        static CitySimState CreatePlaceholderState() => new()
        {
            Funds = 250_000,
            MonthlyIncome = 12_000,
            MonthlyExpense = 8_500,
            Approval = 0.72f,
            Happiness = 0.68f,
            DemandResidential = 0.35f,
            DemandCommercial = 0.22f,
            DemandIndustrial = -0.15f,
            EmploymentRate = 0.86f,
            TradeBalance = 4_200f,
            MonthlyExportValue = 15_500f,
            MonthlyImportCost = 11_300f,
            MeanTrafficDensity = 0.34f,
            InterZoneTradeVolume = 1_240f,
            MeanInterZoneFriction = 1.18f,
            GoodsTransportCostIndex = 0.28f,
            MeanGoodsDeliveryDelay = 0.34f,
            ConstructingBuildingCount = 3,
            AbandonedBuildingCount = 1,
            PowerCoverageFraction = 0.93f,
            WaterCoverageFraction = 0.89f,
            UtilityStressIndex = 0.07f,
            BlackoutFraction = 0.02f,
            WaterShortageFraction = 0.03f,
            MeanEmergencyResponseMinutes = 4.2f,
            HydrantCoverageFraction = 0.72f,
            ActiveFireCount = 0,
            MeanEmsSurvivalRate = 0.90f,
            HospitalBedOccupancyFraction = 0.40f,
            AvailableHospitalBeds = 30,
            WildfireRiskIndex = 0.05f,
            ActiveWildfireTileCount = 0,
            ArsonRiskIndex = 0f,
            ArsonRingActive = false,
            LookoutTowerCount = 0,
            AerialFirefightingAvailable = false,
            FireSafetyRating = 5,
            FireInsurancePremiumMult = 1f,
            MeanEducationLevel = 1.2f,
            EducationCoverageFraction = 0.55f,
            MeanParkAccess = 0.42f,
            ParkAccessFraction = 0.38f,
            HealthCoverageFraction = 0.48f,
            MeanHealthSatisfaction = 0.61f,
            ParkAttractionCount = 2,
            LandmarkAttractionCount = 1,
            TourismAttractionCount = 3,
            TourismIncome = 4_500f,
            MeanRentBurden = 0.28f,
            ResidentialVacancy = 0.12f,
            UnemploymentRate = 0.14f,
            CarModeShare = 0.65f,
            TransitModeShare = 0.20f,
            WalkModeShare = 0.15f,
            MeanCommuteMinutes = 14f,
            CommuterCoverage = 0.92f,
            MeanCommuteSatisfaction = 0.74f,
            CommuteOdSample = new[]
            {
                new CommuteOdSample
                {
                    HomeTileX = 48, HomeTileZ = 52, WorkTileX = 72, WorkTileZ = 61, TripCount = 14,
                },
                new CommuteOdSample
                {
                    HomeTileX = 55, HomeTileZ = 40, WorkTileX = 80, WorkTileZ = 44, TripCount = 9,
                },
            },
            FoodAvgPrice = 1.0f,
            WaterAvgPrice = 0.8f,
            SteelAvgPrice = 2.4f,
            HasGoodsPrices = true,
            MarketZoneCount = 4,
            MaxPartitionPriceSpread = 1.35f,
            EventTaxRevenueMult = 0.85f,
            EventImmigrationMult = 0.90f,
            EventCommercialSpawnMult = 0.92f,
            EventProductivityMult = 0.95f,
            EventResearchMult = 1f,
            EventSpawnDemandMult = 0.95f,
            ActiveEventCount = 1,
        };

        void PublishFromSimHost()
        {
            SimSnapshot snap;
            try
            {
                snap = _simHost.GetSnapshot();
            }
            catch
            {
                return;
            }

            var economy = _simHost.Economy;
            var zoned = CountZonedTiles(snap);
            var popL2 = _simHost.GetPopulationL2();
            var households = CopyHouseholds(popL2);
            var timeOfDay = snap.TimeOfDay;
            var sampleLaw = _simHost.GetSampleLawPreview();
            var laws = _simHost.Laws;
            var (carShare, transitShare, walkShare) = _simHost.CollectModeShares();
            var (meanCommuteMin, commuterCoverage, meanCommuteSat) = _simHost.CollectCommuteHudMetrics();
            var commuteOdSample = CopyCommuteOdSample(_simHost.CollectCommuteOdSample(limit: 16));
            var employment = snap.EmploymentRate;
            var unemployment = Mathf.Clamp01(1f - employment);
            var hasGoodsPrices = economy != null;
            var foodPrice = hasGoodsPrices ? economy.GetAveragePrice(Good.Food) : 0f;
            var waterPrice = hasGoodsPrices ? economy.GetAveragePrice(Good.Water) : 0f;
            var steelPrice = hasGoodsPrices ? economy.GetAveragePrice(Good.Steel) : 0f;
            var marketZoneCount = economy?.ActiveZoneCount
                ?? (snap.MarketZoneCount > 0 ? snap.MarketZoneCount : 1);
            var maxPartitionSpread = 1f;
            if (economy != null && marketZoneCount > 1)
            {
                var spreads = economy.GetAnchorZonePriceSpreads();
                if (spreads != null)
                {
                    for (var i = 0; i < spreads.Length; i++)
                    {
                        var row = spreads[i];
                        if (row.MinPrice <= 0f)
                            continue;
                        var ratio = row.MaxPrice / row.MinPrice;
                        if (ratio > maxPartitionSpread)
                            maxPartitionSpread = ratio;
                    }
                }
            }

            State = new CitySimState
            {
                Population = snap.Population,
                Funds = (int)Mathf.Clamp(snap.CityFunds, int.MinValue, int.MaxValue),
                MonthlyIncome = (int)Mathf.Clamp(snap.MonthlyIncome, int.MinValue, int.MaxValue),
                MonthlyExpense = (int)Mathf.Clamp(snap.MonthlyExpenses, int.MinValue, int.MaxValue),
                Approval = snap.ApprovalRating,
                Happiness = snap.Happiness,
                DemandResidential = economy?.ResidentialDemand ?? 0f,
                DemandCommercial = economy?.CommercialDemand ?? 0f,
                DemandIndustrial = economy?.IndustrialDemand ?? 0f,
                GoodsShortageIndex = snap.GoodsShortageIndex,
                GoodsSurplusIndex = snap.GoodsSurplusIndex,
                EmploymentRate = employment,
                TradeBalance = snap.TradeBalance,
                MonthlyExportValue = snap.MonthlyExportValue,
                MonthlyImportCost = snap.MonthlyImportCost,
                MeanTrafficDensity = snap.MeanTrafficDensity,
                InterZoneTradeVolume = snap.InterZoneTradeVolume,
                MeanInterZoneFriction = snap.MeanInterZoneFriction,
                GoodsTransportCostIndex = snap.GoodsTransportCostIndex,
                MeanGoodsDeliveryDelay = snap.MeanGoodsDeliveryDelay,
                BuildingCount = snap.BuildingCount,
                ConstructingBuildingCount = snap.ConstructingBuildingCount,
                AbandonedBuildingCount = snap.AbandonedBuildingCount,
                ZonedTiles = zoned,
                PowerCoverageFraction = snap.PowerCoverageFraction,
                WaterCoverageFraction = snap.WaterCoverageFraction,
                UtilityStressIndex = snap.UtilityStressIndex,
                BlackoutFraction = snap.BlackoutFraction,
                WaterShortageFraction = snap.WaterShortageFraction,
                MeanEmergencyResponseMinutes = snap.MeanEmergencyResponseMinutes,
                HydrantCoverageFraction = snap.HydrantCoverageFraction,
                ActiveFireCount = snap.ActiveFireCount,
                MeanEmsSurvivalRate = snap.MeanEmsSurvivalRate,
                HospitalBedOccupancyFraction = snap.HospitalBedOccupancyFraction,
                AvailableHospitalBeds = snap.AvailableHospitalBeds,
                WildfireRiskIndex = snap.WildfireRiskIndex,
                ActiveWildfireTileCount = snap.ActiveWildfireTileCount,
                ArsonRiskIndex = snap.ArsonRiskIndex,
                ArsonRingActive = snap.ArsonRingActive,
                LookoutTowerCount = snap.LookoutTowerCount,
                AerialFirefightingAvailable = snap.AerialFirefightingAvailable,
                FireSafetyRating = snap.FireSafetyRating,
                FireInsurancePremiumMult = snap.FireInsurancePremiumMult,
                MeanEducationLevel = snap.MeanEducationLevel,
                EducationCoverageFraction = snap.EducationCoverageFraction,
                MeanParkAccess = snap.MeanParkAccess,
                ParkAccessFraction = snap.ParkAccessFraction,
                HealthCoverageFraction = snap.HealthCoverageFraction,
                MeanHealthSatisfaction = snap.MeanHealthSatisfaction,
                ParkAttractionCount = snap.ParkAttractionCount,
                LandmarkAttractionCount = snap.LandmarkAttractionCount,
                TourismAttractionCount = snap.TourismAttractionCount,
                TourismIncome = snap.TourismIncome,
                MeanRentBurden = snap.MeanRentBurden,
                ResidentialVacancy = snap.ResidentialVacancy,
                UnemploymentRate = unemployment,
                CarModeShare = carShare,
                TransitModeShare = transitShare,
                WalkModeShare = walkShare,
                MeanCommuteMinutes = meanCommuteMin,
                CommuterCoverage = commuterCoverage,
                MeanCommuteSatisfaction = meanCommuteSat,
                CommuteOdSample = commuteOdSample,
                FoodAvgPrice = foodPrice,
                WaterAvgPrice = waterPrice,
                SteelAvgPrice = steelPrice,
                HasGoodsPrices = hasGoodsPrices,
                MarketZoneCount = marketZoneCount,
                MaxPartitionPriceSpread = maxPartitionSpread,
                TimeOfDay = timeOfDay,
                RushMultiplier = LifeSimMath.RushHourMultiplier(timeOfDay),
                HouseholdCount = households.Length,
                Households = households,
                LawDefinitionCount = laws?.DefinitionCount ?? 0,
                ActiveLawCount = laws?.ActiveLawCount ?? 0,
                SampleLawId = sampleLaw?.Id ?? "",
                SampleLawName = sampleLaw?.Name ?? "",
                SampleLawActive = sampleLaw?.Active ?? false,
                LawTrafficCapacityMult = snap.LawTrafficCapacityMult,
                LawSpawnDemandMult = snap.LawSpawnDemandMult,
                LawResidentialSpawnMult = snap.LawResidentialSpawnMult,
                LawIndustrialSpawnMult = snap.LawIndustrialSpawnMult,
                LawCommercialSpawnMult = snap.LawCommercialSpawnMult,
                EventTaxRevenueMult = snap.EventTaxRevenueMult,
                EventImmigrationMult = snap.EventImmigrationMult,
                EventCommercialSpawnMult = snap.EventCommercialSpawnMult,
                EventProductivityMult = snap.EventProductivityMult,
                EventResearchMult = snap.EventResearchMult,
                EventSpawnDemandMult = snap.EventSpawnDemandMult,
            };

            LatestServiceCoverage = _simHost.GetServiceCoverageSample(step: 8);
            LatestUtilityCoverage = _simHost.GetUtilityCoverageSample(step: 8);
            LatestFrictionCorridors = BuildLatestFrictionCorridors(snap);
            LatestRoadGraph = BuildLatestRoadGraph();
            LatestActiveEvents = _simHost.GetActiveEvents();
            // Keep count on CitySimState for UGUI / badge consumers.
            var published = State;
            published.ActiveEventCount = LatestActiveEvents?.Length ?? 0;
            State = published;

            LatestSnapshot = snap;
            SyncGridFromSnapshot(snap);
            OnSnapshotChanged?.Invoke(snap);
            Publish();
        }

        RoadGraphSnapshotDto BuildLatestRoadGraph()
        {
            if (!_simCoreReady || _simHost?.State?.Roads == null)
                return new RoadGraphSnapshotDto();

            var (edgeVolumes, edgeTravelTimes) = _simHost.GetTrafficEdgeExport();
            // Prefer WorldState.RoadEdgeTravelTimes when the traffic system has published
            // them (same source commute satisfaction uses); fall back to export arrays.
            var travelTimes = _simHost.State.RoadEdgeTravelTimes is { Length: > 0 } times
                ? times
                : edgeTravelTimes;
            return RoadGraphSnapshotDto.From(_simHost.State.Roads, edgeVolumes, travelTimes);
        }

        FrictionCorridorDto[] BuildLatestFrictionCorridors(SimSnapshot snap)
        {
            if (!_simCoreReady || _simHost?.Economy == null || _simHost.State == null)
                return Array.Empty<FrictionCorridorDto>();

            var economy = _simHost.Economy;
            if (economy.ActiveZoneCount <= 1)
                return Array.Empty<FrictionCorridorDto>();

            var worldSize = _simHost.State.Tiles?.Size > 0
                ? _simHost.State.Tiles.Size
                : (snap.WorldSize > 0 ? snap.WorldSize : _mapSize);
            if (worldSize <= 1)
                return Array.Empty<FrictionCorridorDto>();

            var samples = economy.CollectFrictionCorridors(
                worldSize,
                _simHost.State.MeanInterZoneFriction,
                _simHost.State.Tiles?.Traffic,
                stride: 2);
            if (samples == null || samples.Length == 0)
                return Array.Empty<FrictionCorridorDto>();

            var list = new FrictionCorridorDto[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                list[i] = new FrictionCorridorDto
                {
                    TileX = samples[i].TileX,
                    TileZ = samples[i].TileZ,
                    Friction = samples[i].Friction,
                };
            }

            return list;
        }

        void SyncGridFromSnapshot(SimSnapshot snap)
        {
            if (_grid == null || snap.TileZoneTypes == null || snap.TileZoneTypes.Length == 0)
                return;

            _grid.SyncFromEngineZones(snap.TileZoneTypes);
        }

        public static ZonePaintTool.ZoneKind EngineZoneToKind(byte zoneType) => zoneType switch
        {
            1 or 2 => ZonePaintTool.ZoneKind.Residential,
            3 => ZonePaintTool.ZoneKind.Commercial,
            4 => ZonePaintTool.ZoneKind.Industrial,
            5 => ZonePaintTool.ZoneKind.Office,
            6 => ZonePaintTool.ZoneKind.Mixed,
            7 => ZonePaintTool.ZoneKind.Agricultural,
            8 => ZonePaintTool.ZoneKind.Park,
            _ => ZonePaintTool.ZoneKind.None,
        };

        static int CountZonedTiles(SimSnapshot snap)
        {
            if (snap.TileZoneTypes == null || snap.TileZoneTypes.Length == 0)
                return 0;

            var count = 0;
            for (var i = 0; i < snap.TileZoneTypes.Length; i++)
            {
                if (snap.TileZoneTypes[i] != 0)
                    count++;
            }

            return count;
        }

        static HouseholdPreview[] CopyHouseholds(PopulationL2Dto popL2)
        {
            var src = popL2.Households;
            if (src == null || src.Length == 0)
                return Array.Empty<HouseholdPreview>();

            var dst = new HouseholdPreview[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                var h = src[i];
                dst[i] = new HouseholdPreview
                {
                    Id = h.Id ?? "",
                    TileX = h.TileX,
                    TileZ = h.TileZ,
                    Happiness = h.Happiness,
                    CommuteMin = h.CommuteMin,
                    HomeBuildingId = h.HomeBuildingId,
                    WorkBuildingId = h.WorkBuildingId,
                    RentBurden = h.RentBurden,
                };
            }

            return dst;
        }

        static CommuteOdSample[] CopyCommuteOdSample(
            Forge.Game.Simulation.PopulationSystem.CommuteOdSampleRow[] rows)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<CommuteOdSample>();

            var dst = new CommuteOdSample[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                dst[i] = new CommuteOdSample
                {
                    HomeTileX = row.HomeTileX,
                    HomeTileZ = row.HomeTileZ,
                    WorkTileX = row.WorkTileX,
                    WorkTileZ = row.WorkTileZ,
                    TripCount = row.TripCount,
                };
            }

            return dst;
        }

        void Publish() => OnStateChanged?.Invoke(State);

        public bool EnqueueResearch(int techId)
        {
            if (!_simCoreReady)
                return false;

            var ok = _simHost.EnqueueResearch(techId);
            if (ok)
                PublishFromSimHost();
            return ok;
        }

        public bool IsTechUnlocked(int techId)
        {
            if (!_simCoreReady || techId < 0)
                return false;

            return _simHost.State.IsTechUnlocked(techId);
        }

        public bool ArePrerequisitesMet(int techId)
        {
            if (!_simCoreReady || techId < 0)
                return false;

            var tech = _simHost.Research.Technologies[techId];
            if (tech?.Prerequisites == null)
                return true;

            foreach (var prereq in tech.Prerequisites)
            {
                if (!_simHost.State.IsTechUnlocked(prereq))
                    return false;
            }

            return true;
        }

        public int CountUnlockedTechs()
        {
            if (!_simCoreReady)
                return 0;

            return ResearchSystem.CountUnlockedTechs(_simHost.State);
        }

        public int ResearchTechCount =>
            _simCoreReady ? _simHost.Research.TechCount : 0;

        public string TechName(int techId)
        {
            if (!_simCoreReady || techId < 0 || techId >= _simHost.Research.Technologies.Length)
                return $"Tech #{techId}";

            return _simHost.Research.Technologies[techId]?.Name ?? $"Tech #{techId}";
        }

        public float TechCost(int techId)
        {
            if (!_simCoreReady || techId < 0 || techId >= _simHost.Research.Technologies.Length)
                return 0f;

            return _simHost.Research.Technologies[techId]?.Cost ?? 0f;
        }

        public void CollectResearchTechs(System.Collections.Generic.List<ResearchSystem.TechDefinition> output)
        {
            output.Clear();
            if (!_simCoreReady)
                return;

            var research = _simHost.Research;
            for (var i = 0; i < research.TechCount; i++)
            {
                var tech = research.Technologies[i];
                if (tech != null)
                    output.Add(tech);
            }
        }

        public bool ResolveHeraldEvent(int eventId)
        {
            if (!_simCoreReady || eventId < 0)
                return false;

            var ok = _simHost.ResolveHeraldEvent(eventId);
            if (ok)
                PublishFromSimHost();
            return ok;
        }

        public void ApplyHeraldOption(string optionId, int eventId = -1)
        {
            if (!_simCoreReady || string.IsNullOrEmpty(optionId))
                return;

            HeraldOptionEffects.Apply(_simHost, optionId, eventId);
            PublishFromSimHost();
        }

        public byte[] ExportSave(string cityName = "CityMajor")
        {
            if (!_simCoreReady)
                return Array.Empty<byte>();

            return _simHost.ExportCmjrBytes(cityName);
        }

        public bool LoadSave(byte[] data)
        {
            if (!_simCoreReady || data == null || data.Length == 0)
                return false;

            var ok = _simHost.LoadFromCmjrBytes(data);
            if (!ok)
                return false;

            PublishFromSimHost();
            OnSaveLoaded?.Invoke();
            return true;
        }

        public bool PlaceBuilding(int tileX, int tileY, int typeId)
        {
            if (!_simCoreReady || typeId <= 0)
                return false;

            var ok = _simHost.PlaceBuilding(tileX, tileY, typeId);
            if (ok)
                PublishFromSimHost();
            return ok;
        }

        public bool SetLawActive(string lawId, bool active)
        {
            if (!_simCoreReady || string.IsNullOrWhiteSpace(lawId))
                return false;

            var ok = _simHost.SetLawActive(lawId, active);
            if (ok)
                PublishFromSimHost();
            return ok;
        }
    }
}
