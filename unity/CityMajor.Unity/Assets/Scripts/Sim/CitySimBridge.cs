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

        public bool Paused { get; set; }
        public float TimeScale { get; set; } = 1f;

        int _mapSize;
        float _accumulator;
        ZoneGrid _grid;
        SimHost _simHost;
        bool _simCoreReady;

        public bool UsesForgeSimCore => _simCoreReady;

        public void Configure(int mapSize)
        {
            _mapSize = mapSize;
            _simCoreReady = TryInitSimHost(mapSize);

            if (!_simCoreReady)
            {
                State = CreatePlaceholderState();
                Publish();
            }
        }

        public void BindGrid(ZoneGrid grid) => _grid = grid;

        /// <summary>Engine zone type bytes — mirrors web/lib/zone-tiers.ts.</summary>
        public static byte EngineZoneType(ZonePaintTool.ZoneKind kind) => kind switch
        {
            ZonePaintTool.ZoneKind.Residential => 1,
            ZonePaintTool.ZoneKind.Commercial => 3,
            ZonePaintTool.ZoneKind.Industrial => 4,
            _ => 0,
        };

        public void PaintZone(int tileX, int tileY, ZonePaintTool.ZoneKind kind)
        {
            if (_grid != null)
                _grid.SetZone(tileX, tileY, kind);

            if (!_simCoreReady)
            {
                if (_grid != null)
                    NotifyZonesChanged();
                return;
            }

            _simHost.PaintZone(tileX, tileY, EngineZoneType(kind));
            PublishFromSimHost();
        }

        public void PlaceRoad(int tileX, int tileY)
        {
            if (!_simCoreReady)
                return;

            _simHost.PlaceRoad(tileX, tileY);
            PublishFromSimHost();
        }

        /// <summary>Clears zone density on tile. Full road/building removal awaits SimHost API parity.</summary>
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
                _simHost.Init(mapSize, new SimHostInitOptions
                {
                    UnityModernProfile = true,
                    SkipStarterCity = true,
                    DataPaths = dataPaths,
                });
                PublishFromSimHost();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CityMajor] Forge.SimCore unavailable — using placeholder sim ({ex.Message})");
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
                BuildingCount = snap.BuildingCount,
                ZonedTiles = zoned,
                TimeOfDay = timeOfDay,
                RushMultiplier = LifeSimMath.RushHourMultiplier(timeOfDay),
                HouseholdCount = households.Length,
                Households = households,
                LawDefinitionCount = laws?.DefinitionCount ?? 0,
                ActiveLawCount = laws?.ActiveLawCount ?? 0,
                SampleLawId = sampleLaw?.Id ?? "",
                SampleLawName = sampleLaw?.Name ?? "",
                SampleLawActive = sampleLaw?.Active ?? false,
            };

            LatestServiceCoverage = _simHost.GetServiceCoverageSample(step: 8);

            LatestSnapshot = snap;
            SyncGridFromSnapshot(snap);
            OnSnapshotChanged?.Invoke(snap);
            Publish();
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
