using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Top-right budget + economy summary from SimSnapshot (Forge.SimCore).
    /// Mirrors web BudgetPanel.tsx essentials.
    /// </summary>
    public sealed class BudgetPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/BudgetPanel.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _treasury;
        Label _cashflow;
        Label _cashflowDetail;
        Label _taxRes;
        Label _taxCom;
        Label _taxInd;
        Label _approval;
        Label _happiness;
        Label _loan;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnStateChanged;
            }

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnSnapshotChanged += OnSnapshot;
                _sim.OnStateChanged += OnStateChanged;
                if (_sim.LatestSnapshot != null)
                    ApplySnapshot(_sim.LatestSnapshot);
                else
                    ApplyState(_sim.State);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnStateChanged;
            }
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
                    Debug.LogError($"[CityMajor] Missing budget panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 90;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _treasury = root.Q<Label>("treasury-value");
            _cashflow = root.Q<Label>("cashflow-value");
            _cashflowDetail = root.Q<Label>("cashflow-detail");
            _taxRes = root.Q<Label>("tax-res-value");
            _taxCom = root.Q<Label>("tax-com-value");
            _taxInd = root.Q<Label>("tax-ind-value");
            _approval = root.Q<Label>("approval-value");
            _happiness = root.Q<Label>("happiness-value");
            _loan = root.Q<Label>("loan-value");
        }

        void OnSnapshot(SimSnapshot snap) => ApplySnapshot(snap);

        void OnStateChanged(CitySimState state)
        {
            if (_sim?.LatestSnapshot == null)
                ApplyState(state);
        }

        void ApplySnapshot(SimSnapshot snap)
        {
            if (_treasury == null)
                BindElements();
            if (_treasury == null)
                return;

            _treasury.text = FormatFunds(snap.CityFunds);

            var net = snap.MonthlyIncome - snap.MonthlyExpenses;
            _cashflow.text = $"{FormatSignedMoney(net)}/mo";
            _cashflow.RemoveFromClassList("panel-value--positive");
            _cashflow.RemoveFromClassList("panel-value--negative");
            _cashflow.AddToClassList(net >= 0 ? "panel-value--positive" : "panel-value--negative");

            _cashflowDetail.text =
                $"{FormatSignedMoney(snap.MonthlyIncome)} in · {FormatSignedMoney(-snap.MonthlyExpenses)} out";

            _taxRes.text = FormatTaxRate(snap.PropertyTaxRate);
            _taxCom.text = FormatTaxRate(snap.CommercialTaxRate);
            _taxInd.text = FormatTaxRate(snap.IndustrialTaxRate);
            _approval.text = $"{snap.ApprovalRating * 100f:0}%";
            _happiness.text = $"{snap.Happiness * 100f:0}%";
            _loan.text = snap.LoanBalance > 0 ? FormatFunds(snap.LoanBalance) : "—";
        }

        void ApplyState(CitySimState state)
        {
            if (_treasury == null)
                BindElements();
            if (_treasury == null)
                return;

            _treasury.text = FormatFunds(state.Funds);
            var net = state.MonthlyIncome - state.MonthlyExpense;
            _cashflow.text = $"{FormatSignedMoney(net)}/mo";
            _cashflowDetail.text =
                $"{FormatSignedMoney(state.MonthlyIncome)} in · {FormatSignedMoney(-state.MonthlyExpense)} out";
            _approval.text = $"{state.Approval * 100f:0}%";
            _happiness.text = $"{state.Happiness * 100f:0}%";
            _taxRes.text = _taxCom.text = _taxInd.text = _loan.text = "—";
        }

        static string FormatFunds(long cityFunds)
        {
            var abs = Mathf.Abs((int)Mathf.Clamp(cityFunds, int.MinValue, int.MaxValue));
            if (abs >= 1_000_000)
                return $"${cityFunds / 1_000_000f:0.0}M";
            if (abs >= 1_000)
                return $"${cityFunds / 1_000f:0.0}K";
            return $"${cityFunds:N0}";
        }

        static string FormatSignedMoney(long amount)
        {
            var abs = Mathf.Abs((int)Mathf.Clamp(amount, int.MinValue, int.MaxValue));
            var sign = amount >= 0 ? "+" : "−";
            if (abs >= 1_000_000)
                return $"{sign}${abs / 1_000_000f:0.0}M";
            if (abs >= 1_000)
                return $"{sign}${abs / 1_000f:0.0}K";
            return $"{sign}${abs:N0}";
        }

        static string FormatTaxRate(float rate)
        {
            var pct = rate <= 1f ? rate * 100f : rate;
            return Mathf.Approximately(pct, Mathf.Round(pct))
                ? $"{pct:0}%"
                : $"{pct:0.1}%";
        }
    }
}
