using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Left-stack citizen happiness — mirrors web HappinessMeter.tsx.</summary>
    public sealed class HappinessMeterController : MonoBehaviour
    {
        const string MeterPath = "Assets/UI/HappinessMeter.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _value;
        VisualElement _fill;

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
                var asset = UiAssetLoader.LoadUxml(MeterPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing happiness meter at {MeterPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 98;
            var root = _document.rootVisualElement;
            _value = root?.Q<Label>("happiness-value");
            _fill = root?.Q<VisualElement>("happiness-fill");
        }

        void OnStateChanged(CitySimState state) => ApplyState(state);

        void ApplyState(CitySimState state)
        {
            if (_value == null)
                EnsureUi();

            if (_value == null)
                return;

            var pct = Mathf.Clamp01(state.Happiness) * 100f;
            _value.text = $"{pct:F0}%";
            if (_fill != null)
            {
                _fill.style.width = Length.Percent(pct);
                _fill.style.backgroundColor = HappinessColor(state.Happiness);
            }
        }

        static Color HappinessColor(float happiness)
        {
            var pct = happiness * 100f;
            if (pct > 70f) return new Color(0.49f, 1f, 0.7f);
            if (pct >= 40f) return new Color(0.91f, 0.83f, 0.29f);
            return new Color(1f, 0.35f, 0.35f);
        }
    }
}
