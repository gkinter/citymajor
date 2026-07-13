using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Top-right mayor approval — mirrors web ApprovalMeter.tsx.</summary>
    public sealed class ApprovalMeterController : MonoBehaviour
    {
        const string MeterPath = "Assets/UI/ApprovalMeter.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _value;
        Label _status;
        Label _happiness;
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
                    Debug.LogError($"[CityMajor] Missing approval meter at {MeterPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 99;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _value = root.Q<Label>("approval-value");
            _status = root.Q<Label>("approval-status");
            _happiness = root.Q<Label>("approval-happiness");
            _fill = root.Q<VisualElement>("approval-fill");
        }

        void OnStateChanged(CitySimState state) => ApplyState(state);

        void ApplyState(CitySimState state)
        {
            if (_value == null)
                BindElements();

            if (_value == null)
                return;

            var pct = Mathf.Clamp(state.Approval * 100f, 0f, 100f);
            _value.text = $"{pct:F0}%";
            _value.style.color = ApprovalColor(pct);

            if (_status != null)
            {
                _status.text = ApprovalStatusLabel(pct);
                _status.style.color = ApprovalColor(pct);
            }

            if (_fill != null)
            {
                _fill.style.width = Length.Percent(pct);
                _fill.style.backgroundColor = ApprovalColor(pct);
            }

            if (_happiness != null)
                _happiness.text = $"Happiness {(Mathf.Clamp01(state.Happiness) * 100f):F0}%";
        }

        static string ApprovalStatusLabel(float approvalPercent)
        {
            if (approvalPercent >= 70f) return "Beloved";
            if (approvalPercent >= 50f) return "Stable";
            if (approvalPercent >= 35f) return "Uneasy";
            if (approvalPercent >= 20f) return "Restless";
            return "Critical";
        }

        static Color ApprovalColor(float approvalPercent)
        {
            if (approvalPercent >= 70f) return new Color(0.49f, 1f, 0.7f);
            if (approvalPercent >= 50f) return new Color(0.72f, 0.91f, 0.42f);
            if (approvalPercent >= 35f) return new Color(0.94f, 0.88f, 0.38f);
            if (approvalPercent >= 20f) return new Color(1f, 0.69f, 0.44f);
            return new Color(1f, 0.44f, 0.44f);
        }
    }
}
