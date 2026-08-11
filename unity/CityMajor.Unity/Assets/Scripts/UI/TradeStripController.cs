using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Trade routes strip — global market totals + Cathedral P3.5 bilateral freight HUD.
    /// Keyboard toggle moved to <see cref="EconomyPanelController"/> (E).
    /// Open via UI close/open API if wired; Esc still closes while open.
    /// </summary>
    public sealed class TradeStripController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/TradeStrip.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        Label _subtitle;
        Label _status;
        Label _employment;
        Label _income;
        Label _expense;
        Label _balance;
        Label _flow;
        Label _interzone;
        Label _demand;
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
            _subtitle = docRoot?.Q<Label>("trade-subtitle");
            _status = docRoot?.Q<Label>("trade-status");
            _employment = docRoot?.Q<Label>("trade-employment");
            _income = docRoot?.Q<Label>("trade-income");
            _expense = docRoot?.Q<Label>("trade-expense");
            _balance = docRoot?.Q<Label>("trade-balance");
            _flow = docRoot?.Q<Label>("trade-flow");
            _interzone = docRoot?.Q<Label>("trade-interzone");
            _demand = docRoot?.Q<Label>("trade-demand");
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

            var routes = Mathf.Max(0, state.BilateralRouteCount);
            var freight = Mathf.Max(0f, state.MeanFreightMonths);
            if (_subtitle != null)
            {
                _subtitle.text = routes > 0
                    ? $"Global market · {routes} bilateral route{(routes == 1 ? "" : "s")} · freight {freight:0.#} mo"
                    : "Global market · bilateral contracts ready (P3.5)";
            }

            if (_status != null)
            {
                _status.text = routes > 0
                    ? $"Bilateral: {routes} route{(routes == 1 ? "" : "s")} · " +
                      $"notional {state.BilateralTradeValue:N0}/mo · mean freight {freight:0.#} mo"
                    : "No bilateral routes yet. Create partner contracts in sim; auto-trade fills global gaps.";
            }

            var net = state.MonthlyIncome - state.MonthlyExpense;
            if (_employment != null)
            {
                var empPct = Mathf.RoundToInt(state.EmploymentRate * 100f);
                _employment.text = $"Employment: {empPct}%";
            }

            if (_income != null)
                _income.text = $"Income: +{state.MonthlyIncome:N0}/mo";
            if (_expense != null)
                _expense.text = $"Expenses: −{state.MonthlyExpense:N0}/mo";
            _balance.text = $"Net treasury flow: {(net >= 0 ? "+" : "")}{net:N0}/mo";

            if (_flow != null)
            {
                var tradeSign = state.TradeBalance >= 0 ? "+" : "";
                _flow.text =
                    $"Trade balance: {tradeSign}{state.TradeBalance:N0}/mo · " +
                    $"Exports +{state.MonthlyExportValue:N0} · Imports −{state.MonthlyImportCost:N0}";
            }

            if (_interzone != null)
            {
                _interzone.text =
                    $"Inter-zone volume: {state.InterZoneTradeVolume:N0}/day · " +
                    $"Friction ×{state.MeanInterZoneFriction:F2} · " +
                    $"Delivery {Mathf.RoundToInt(Mathf.Clamp01(state.MeanGoodsDeliveryDelay) * 100f)}% · " +
                    $"Transport {Mathf.RoundToInt(Mathf.Clamp01(state.GoodsTransportCostIndex) * 100f)}%";
            }

            if (_demand != null)
            {
                _demand.text =
                    $"RCI demand (export proxy): R {FormatDemand(state.DemandResidential)} · " +
                    $"C {FormatDemand(state.DemandCommercial)} · I {FormatDemand(state.DemandIndustrial)}";
            }
        }

        static string FormatDemand(float demand)
        {
            var pct = Mathf.RoundToInt(demand * 100f);
            return pct >= 0 ? $"+{pct}%" : $"{pct}%";
        }
    }
}
