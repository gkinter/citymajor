using CityMajor.Sim;
using Forge.SimCore;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Pop / funds / hour / utilities / EMS Services HUD (top-left).
    /// RCI demand lives in <see cref="DemandOverlayController"/>.
    /// </summary>
    public sealed class ResourcesHudController : MonoBehaviour
    {
        const string HudAssetPath = "Assets/UI/ResourcesHud.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _popValue;
        Label _popGrowth;
        Label _fundsValue;
        Label _timeValue;
        VisualElement _utilitiesRow;
        Label _utilitiesValue;
        Label _emsValue;
        Label _fireValue;
        Label _hospitalValue;
        Label _policeValue;
        Label _wasteValue;
        Label _sewageValue;
        Label _netValue;
        Label _eduValue;
        Label _parkValue;
        Label _cranesLabel;
        readonly PopulationGrowthTracker _growthTracker = new();

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
            {
                _sim.OnStateChanged -= OnStateChanged;
                _sim.OnSnapshotChanged -= OnSnapshot;
            }

            _sim = sim;
            EnsureUiDocument();

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                _sim.OnSnapshotChanged += OnSnapshot;
                OnStateChanged(_sim.State);
                if (_sim.LatestSnapshot != null)
                    OnSnapshot(_sim.LatestSnapshot);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnStateChanged -= OnStateChanged;
                _sim.OnSnapshotChanged -= OnSnapshot;
            }
        }

        void EnsureUiDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = LoadHudAsset();
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing HUD asset at {HudAssetPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 100;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _popValue = root.Q<Label>("pop-value");
            _popGrowth = root.Q<Label>("pop-growth");
            _fundsValue = root.Q<Label>("funds-value");
            _timeValue = root.Q<Label>("time-value");
            _utilitiesRow = root.Q<VisualElement>("utilities-row");
            _utilitiesValue = root.Q<Label>("utilities-value");
            _emsValue = root.Q<Label>("ems-value");
            _fireValue = root.Q<Label>("fire-value");
            _hospitalValue = root.Q<Label>("hospital-value");
            _policeValue = root.Q<Label>("police-value");
            _wasteValue = root.Q<Label>("waste-value");
            _sewageValue = root.Q<Label>("sewage-value");
            _netValue = root.Q<Label>("net-value");
            _eduValue = root.Q<Label>("edu-value");
            _parkValue = root.Q<Label>("park-value");
            _cranesLabel = root.Q<Label>("cranes-label");
        }

        void OnSnapshot(SimSnapshot snapshot)
        {
            _growthTracker.Push(snapshot.TickCount, snapshot.Population);
            ApplyGrowthLabel();
        }

        void OnStateChanged(CitySimState state) => ApplyState(state);

        void ApplyState(CitySimState state)
        {
            if (_popValue == null)
                BindElements();

            if (_popValue == null)
                return;

            _popValue.text = state.Population.ToString("N0");
            _fundsValue.text = FormatFunds(state.Funds);
            if (_timeValue != null)
            {
                var hour = Mathf.FloorToInt(state.TimeOfDay % 24f);
                var min = Mathf.FloorToInt((state.TimeOfDay % 1f) * 60f);
                _timeValue.text = $"{hour:D2}:{min:D2} ×{state.RushMultiplier:F1}";
            }

            ApplyGrowthLabel();
            ApplyUtilities(state);
            ApplyServices(state);
            ApplyCranes(state);
        }

        void ApplyUtilities(CitySimState state)
        {
            if (_utilitiesValue == null)
                return;

            var powerPct = Mathf.RoundToInt(state.PowerCoverageFraction * 100f);
            var waterPct = Mathf.RoundToInt(state.WaterCoverageFraction * 100f);
            _utilitiesValue.text = $"⚡ {powerPct}% · 💧 {waterPct}%";

            var stressClass = UtilityStressClass(state.UtilityStressIndex);
            _utilitiesValue.EnableInClassList("hud-utilities-value--ok", stressClass == "ok");
            _utilitiesValue.EnableInClassList("hud-utilities-value--warn", stressClass == "warn");
            _utilitiesValue.EnableInClassList("hud-utilities-value--stress", stressClass == "stress");

            if (_utilitiesRow != null)
            {
                _utilitiesRow.EnableInClassList("hud-utilities-row--ok", stressClass == "ok");
                _utilitiesRow.EnableInClassList("hud-utilities-row--warn", stressClass == "warn");
                _utilitiesRow.EnableInClassList("hud-utilities-row--stress", stressClass == "stress");
            }
        }

        void ApplyServices(CitySimState state)
        {
            if (_emsValue != null)
            {
                var minutes = FormatEmergencyResponseMinutes(state.MeanEmergencyResponseMinutes);
                var survivalPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanEmsSurvivalRate) * 100f);
                _emsValue.text = $"{minutes} · {survivalPct}%";
                _emsValue.tooltip =
                    "Mean fire/EMS response minutes over sampled zoned tiles (road distance + traffic; 2× without hydrant) and EMS survival rate from response + hospital transport (P5.4 / Tier-2 capacity).";
            }

            if (_fireValue != null)
            {
                var hydrantPct = Mathf.RoundToInt(Mathf.Clamp01(state.HydrantCoverageFraction) * 100f);
                var fires = Mathf.Max(0, state.ActiveFireCount);
                var wildfire = Mathf.Max(0, state.ActiveWildfireTileCount);
                var droughtPct = Mathf.RoundToInt(Mathf.Clamp01(state.WildfireRiskIndex) * 100f);
                var rating = Mathf.Clamp(state.FireSafetyRating, 1, 10);

                var text = fires > 0
                    ? $"🚰 {hydrantPct}% · 🔥 {fires}"
                    : $"🚰 {hydrantPct}%";
                if (wildfire > 0)
                    text += $" · 🌲 {wildfire}";
                else if (droughtPct >= 70)
                    text += $" · 🌲 {droughtPct}%";
                if (state.ArsonRingActive)
                    text += " · 🕵️";
                else if (state.ArsonRiskIndex >= 0.25f)
                    text += $" · 🕵️ {Mathf.RoundToInt(Mathf.Clamp01(state.ArsonRiskIndex) * 100f)}%";
                if (state.LookoutTowerCount > 0)
                    text += $" · 🔭 {state.LookoutTowerCount}";
                if (state.AerialFirefightingAvailable)
                    text += " · ✈️";
                text += $" · ⭐ {rating}";

                _fireValue.text = text;
                _fireValue.tooltip =
                    "Hydrant coverage; active building fires (P5.3); wildfire / drought and arson (Tier-2); lookout towers / aerial firefighting; fire safety rating 1–10 driving insurance premium (MISSING_SYSTEMS §1.1).";
            }

            if (_hospitalValue != null)
            {
                var freeBeds = Mathf.Max(0, state.AvailableHospitalBeds);
                var occPct = Mathf.RoundToInt(Mathf.Clamp01(state.HospitalBedOccupancyFraction) * 100f);
                var healthCovPct = Mathf.RoundToInt(Mathf.Clamp01(state.HealthCoverageFraction) * 100f);
                var healthPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanHealthSatisfaction) * 100f);
                _hospitalValue.text = freeBeds > 0 || occPct > 0 || healthCovPct > 0
                    ? $"🏥 {freeBeds} free · {occPct}% · ❤ {healthPct}% · {healthCovPct}%"
                    : "🏥 —";
                _hospitalValue.tooltip =
                    "Free beds / occupancy; mean household HealthSatisfaction and fraction under hospital coverage. Coverage raises HealthSatisfaction over time (Tier-2 health progression → P4 sat / migration).";
            }

            if (_policeValue != null)
            {
                var safetyPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanSafetySatisfaction) * 100f);
                var crimePct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanCrimeRate) * 100f);
                var covPct = Mathf.RoundToInt(Mathf.Clamp01(state.PoliceCoverageFraction) * 100f);
                _policeValue.text = covPct > 0 || safetyPct > 0 || crimePct > 0
                    ? $"👮 {safetyPct}% · crime {crimePct}% · {covPct}%"
                    : "👮 —";
                _policeValue.tooltip =
                    "Mean household SafetySatisfaction, mean home-tile crime, and fraction under police coverage. Station quality deepens crime suppression; safety feeds P4 satisfaction / immigration (Tier-2 police → crime → outcomes; arson already reads tile crime).";
            }

            if (_wasteValue != null)
            {
                var envPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanEnvironmentScore) * 100f);
                var pollutionPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanPollution) * 100f);
                var covPct = Mathf.RoundToInt(Mathf.Clamp01(state.WasteCoverageFraction) * 100f);
                _wasteValue.text = covPct > 0 || envPct > 0 || pollutionPct > 0
                    ? $"🗑️ {envPct}% · pol {pollutionPct}% · {covPct}%"
                    : "🗑️ —";
                _wasteValue.tooltip =
                    "Mean environment score (1 − pollution), mean home-tile pollution, and fraction under waste coverage. Depots abate residential/commercial waste; uncovered zones accumulate pollution that feeds environment satisfaction / health / immigration (Tier-2 waste → pollution → outcomes).";
            }

            if (_sewageValue != null)
            {
                var qualityPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanWaterQuality) * 100f);
                var csoPct = Mathf.RoundToInt(Mathf.Clamp01(state.CsoOverflowRate) * 100f);
                var utilPct = Mathf.RoundToInt(Mathf.Clamp(state.MeanPipeUtilization, 0f, 2f) * 100f);
                var covPct = Mathf.RoundToInt(Mathf.Clamp01(state.SewageCoverageFraction) * 100f);
                _sewageValue.text = covPct > 0 || qualityPct > 0 || csoPct > 0 || utilPct > 0
                    ? $"💧 {qualityPct}% · cso {csoPct}% · util {utilPct}%"
                    : "💧 —";
                _sewageValue.tooltip =
                    "Mean water quality, combined-sewer overflow (CSO) rate, and Manning pipe utilization. Storm runoff (rational method) + dry sewage vs pipe capacity; overflow dirties water that feeds health / environment / immigration. Coverage " +
                    $"{covPct}% · runoff {Mathf.RoundToInt(Mathf.Clamp(state.StormRunoffLoad, 0f, 1.5f) * 100f)}% (Manning/CSO extends Tier-2 sewage).";
            }

            if (_netValue != null)
            {
                var accessPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanTelecomAccess) * 100f);
                var tier = Mathf.Clamp(state.MeanInternetTier, 0f, 3f);
                var covPct = Mathf.RoundToInt(Mathf.Clamp01(state.InternetCoverageFraction) * 100f);
                _netValue.text = covPct > 0 || accessPct > 0 || tier > 0.05f
                    ? $"📡 {accessPct}% · tier {tier:0.0}/3 · {covPct}%"
                    : "📡 —";
                _netValue.tooltip =
                    "Mean telecom access (tier / 3), mean InternetConnection tier (0=none … 3=5G), and fraction with copper+. " +
                    "Telecom hubs deepen TileData.InternetConnection; fiber needs T044 Internet Infrastructure, 5G needs T045 5G/6G Networks. " +
                    "Coverage feeds services satisfaction / immigration (Tier-2 internet / telecom → outcomes).";
            }

            if (_eduValue != null)
            {
                var mean = Mathf.Clamp(state.MeanEducationLevel, 0f, 3f);
                var covPct = Mathf.RoundToInt(Mathf.Clamp01(state.EducationCoverageFraction) * 100f);
                _eduValue.text = $"🎓 {mean:0.0}/3 · {covPct}%";
                _eduValue.tooltip =
                    "Mean household education level (0–3) and fraction of households under school coverage. Coverage raises education over time; thin coverage slowly decays (Tier-2 / Cathedral P5 education depth).";
            }

            if (_parkValue != null)
            {
                var accessPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanParkAccess) * 100f);
                var healthPct = Mathf.RoundToInt(Mathf.Clamp01(state.MeanHealthSatisfaction) * 100f);
                _parkValue.text = $"🌳 {accessPct}% · ❤️ {healthPct}%";
                _parkValue.tooltip =
                    "Mean park / exercise access and mean household health satisfaction. Painted parks and hospitals raise HealthSatisfaction, which feeds P4 happiness / immigration / emigration (Tier-2 park amenity → population outcomes).";
            }
        }

        /// <summary>Compact HUD readout, e.g. <c>4.3m</c> / <c>30m</c> (mirrors web emergency-response.ts).</summary>
        internal static string FormatEmergencyResponseMinutes(float minutes)
        {
            if (float.IsNaN(minutes) || float.IsInfinity(minutes) || minutes < 0f)
                return "—";
            if (minutes >= 100f)
                return $"{Mathf.RoundToInt(minutes)}m";
            if (minutes >= 10f)
                return $"{minutes:0}m";
            return $"{minutes:0.0}m";
        }

        void ApplyCranes(CitySimState state)
        {
            if (_cranesLabel == null)
                return;

            if (state.ConstructingBuildingCount <= 0)
            {
                _cranesLabel.style.display = DisplayStyle.None;
                _cranesLabel.text = "";
                return;
            }

            _cranesLabel.style.display = DisplayStyle.Flex;
            var noun = state.ConstructingBuildingCount == 1 ? "building" : "buildings";
            _cranesLabel.text = $"🚧 {state.ConstructingBuildingCount} {noun}";
        }

        static string UtilityStressClass(float stress)
        {
            if (stress < 0.2f)
                return "ok";
            if (stress <= 0.5f)
                return "warn";
            return "stress";
        }

        void ApplyGrowthLabel()
        {
            if (_popGrowth == null)
                return;

            var rate = _growthTracker.EstimatePerMonth();
            if (rate == null)
            {
                _popGrowth.text = "";
                _popGrowth.style.display = DisplayStyle.None;
                return;
            }

            _popGrowth.style.display = DisplayStyle.Flex;
            _popGrowth.text = $"({PopulationGrowthFormat.FormatGrowthPerMonth(rate.Value)})";
            _popGrowth.EnableInClassList("hud-sub--good", rate.Value > 0);
            _popGrowth.EnableInClassList("hud-sub--bad", rate.Value < 0);
        }

        static string FormatFunds(int cityFunds)
        {
            var abs = Mathf.Abs(cityFunds);
            if (abs >= 1_000_000)
                return $"${cityFunds / 1_000_000f:0.0}M";
            if (abs >= 1_000)
                return $"${cityFunds / 1_000f:0.0}K";
            return $"${cityFunds:N0}";
        }

        static VisualTreeAsset LoadHudAsset() => UiAssetLoader.LoadUxml(HudAssetPath);
    }
}
