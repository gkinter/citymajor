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
                    "Mean fire/EMS response minutes over sampled zoned tiles (road distance + traffic; 2× without hydrant) and EMS survival rate from response time (P5.4).";
            }

            if (_fireValue != null)
            {
                var hydrantPct = Mathf.RoundToInt(Mathf.Clamp01(state.HydrantCoverageFraction) * 100f);
                var fires = Mathf.Max(0, state.ActiveFireCount);
                _fireValue.text = fires > 0
                    ? $"🚰 {hydrantPct}% · 🔥 {fires}"
                    : $"🚰 {hydrantPct}%";
                _fireValue.tooltip =
                    "Hydrant coverage over zoned buildings; active fire count when any building is burning (P5.3).";
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
