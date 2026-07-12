using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// RCI + mayor HUD (UI Toolkit). Mirrors web ResourcesHud.tsx layout (Pop, Funds, R/C/I bars).
    /// </summary>
    public sealed class ResourcesHudController : MonoBehaviour
    {
        const string HudAssetPath = "Assets/UI/ResourcesHud.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _popValue;
        Label _fundsValue;
        Label _timeValue;
        Label _rDemandText;
        Label _cDemandText;
        Label _iDemandText;
        VisualElement _rDemandFill;
        VisualElement _cDemandFill;
        VisualElement _iDemandFill;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            _sim = sim;
            EnsureUiDocument();

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                OnStateChanged(_sim.State);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;
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
            _fundsValue = root.Q<Label>("funds-value");
            _timeValue = root.Q<Label>("time-value");
            _rDemandText = root.Q<Label>("r-demand-text");
            _cDemandText = root.Q<Label>("c-demand-text");
            _iDemandText = root.Q<Label>("i-demand-text");
            _rDemandFill = root.Q<VisualElement>("r-demand-fill");
            _cDemandFill = root.Q<VisualElement>("c-demand-fill");
            _iDemandFill = root.Q<VisualElement>("i-demand-fill");
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
            SetDemandRow(_rDemandText, _rDemandFill, state.DemandResidential);
            SetDemandRow(_cDemandText, _cDemandFill, state.DemandCommercial);
            SetDemandRow(_iDemandText, _iDemandFill, state.DemandIndustrial);
        }

        static void SetDemandRow(Label text, VisualElement fill, float demand)
        {
            if (text == null || fill == null)
                return;

            text.text = demand.ToString("+0.00;-0.00;0.00");
            fill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(demand)) * 100f);
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
