using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Bottom-center bidirectional R/C/I meters — mirrors web DemandOverlay.tsx.</summary>
    public sealed class DemandOverlayController : MonoBehaviour
    {
        const string OverlayPath = "Assets/UI/DemandOverlay.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _rFill;
        VisualElement _cFill;
        VisualElement _iFill;
        Label _rValue;
        Label _cValue;
        Label _iValue;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            _sim = sim;
            EnsureUi();

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

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(OverlayPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing demand overlay at {OverlayPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 95;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _rFill = root.Q<VisualElement>("r-fill");
            _cFill = root.Q<VisualElement>("c-fill");
            _iFill = root.Q<VisualElement>("i-fill");
            _rValue = root.Q<Label>("r-value");
            _cValue = root.Q<Label>("c-value");
            _iValue = root.Q<Label>("i-value");
        }

        void OnStateChanged(CitySimState state) => ApplyState(state);

        void ApplyState(CitySimState state)
        {
            if (_rFill == null)
                BindElements();

            SetMeter(_rFill, _rValue, state.DemandResidential);
            SetMeter(_cFill, _cValue, state.DemandCommercial);
            SetMeter(_iFill, _iValue, state.DemandIndustrial);
        }

        static void SetMeter(VisualElement fill, Label valueLabel, float demand)
        {
            if (fill == null || valueLabel == null)
                return;

            var clamped = Mathf.Clamp(demand, -1f, 1f);
            var display = Mathf.RoundToInt(clamped * 100f);
            valueLabel.text = display > 0 ? $"+{display}" : display.ToString();

            var halfPct = Mathf.Abs(clamped) * 50f;
            fill.style.left = Length.Percent(clamped >= 0f ? 50f : 50f - halfPct);
            fill.style.width = Length.Percent(halfPct);
        }
    }
}
