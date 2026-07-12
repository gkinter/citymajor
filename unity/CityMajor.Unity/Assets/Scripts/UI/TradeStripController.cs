using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Read-only trade routes strip (v2 scaffold). Toggle with E.</summary>
    public sealed class TradeStripController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/TradeStrip.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        Label _balance;
        Button _closeBtn;
        bool _open;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            _sim.OnStateChanged += OnState;
            OnState(_sim.State);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(PanelPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing trade strip at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 111;
            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("trade-root");
            _balance = docRoot?.Q<Label>("trade-balance");
            _closeBtn = docRoot?.Q<Button>("trade-close");
            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
                OnState(_sim.State);
        }

        void OnState(CitySimState state)
        {
            if (_balance == null)
                return;

            var net = state.MonthlyIncome - state.MonthlyExpense;
            _balance.text = $"Monthly net: {net:N0} · Global market only (SB-3728 routes pending)";
        }
    }
}
