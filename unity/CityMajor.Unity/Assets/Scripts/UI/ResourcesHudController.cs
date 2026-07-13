using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Pop / funds / hour HUD (top-left). RCI demand lives in <see cref="DemandOverlayController"/>.
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
            _popGrowth.text = $"({PopulationGrowthFormat.PerMonth(rate.Value)})";
            _popGrowth.style.color = rate.Value > 0
                ? new Color(0.49f, 1f, 0.7f)
                : rate.Value < 0
                    ? new Color(1f, 0.44f, 0.44f)
                    : new Color(0.78f, 0.82f, 0.88f);
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
