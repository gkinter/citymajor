using System.Text;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace CityMajor.UI
{
    /// <summary>
    /// UGUI Economy panel — goods imbalances, partition price spread, production flows.
    /// Toggle with E (toolbar button also available). Esc closes while open.
    /// </summary>
    public sealed class EconomyPanelController : MonoBehaviour
    {
        CitySimBridge _sim;
        GameObject _root;
        Text _summary;
        Text _shortages;
        Text _surpluses;
        Text _spreads;
        Text _flows;
        bool _open;

        public bool IsOpen => _open;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnState;
            }

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnSnapshotChanged += OnSnapshot;
                _sim.OnStateChanged += OnState;
                Refresh();
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnState;
            }
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.SetActive(open);
            if (open)
                Refresh();
        }

        void OnSnapshot(SimSnapshot _) => Refresh();
        void OnState(CitySimState _) => Refresh();

        void EnsureUi()
        {
            if (_root != null)
                return;

            var canvas = UguiPanelBuilder.EnsureOverlayCanvas(gameObject, "EconomyPoliticsCanvas", 200);
            var panel = UguiPanelBuilder.CreatePanel(
                canvas.transform,
                "EconomyPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-220f, 20f),
                new Vector2(420f, 620f));
            _root = panel.gameObject;

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(panel, false);
            var headerRt = (RectTransform)header.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 48f);
            header.GetComponent<Image>().color = UguiPanelBuilder.HeaderBg;

            var title = UguiPanelBuilder.AddText(
                header.transform, "Title", "Economy [E]", 18, UguiPanelBuilder.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(14f, 0f);
            titleRt.offsetMax = new Vector2(-48f, 0f);

            var close = UguiPanelBuilder.AddButton(header.transform, "Close", "✕", new Vector2(36f, 32f));
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            close.onClick.AddListener(() => SetOpen(false));

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(panel, false);
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(14f, 14f);
            bodyRt.offsetMax = new Vector2(-14f, -56f);

            _summary = UguiPanelBuilder.AddText(
                body.transform, "Summary", "", 13, UguiPanelBuilder.Muted);
            Stretch(_summary, 0f, 1f, 0f, -4f);

            _shortages = UguiPanelBuilder.AddText(
                body.transform, "Shortages", "", 13, Color.white);
            Stretch(_shortages, 0f, 0.82f, 0f, -72f);

            _surpluses = UguiPanelBuilder.AddText(
                body.transform, "Surpluses", "", 13, Color.white);
            Stretch(_surpluses, 0f, 0.62f, 0f, -180f);

            _spreads = UguiPanelBuilder.AddText(
                body.transform, "PartitionSpreads", "", 13, Color.white);
            Stretch(_spreads, 0f, 0.38f, 0f, -290f);

            _flows = UguiPanelBuilder.AddText(
                body.transform, "Flows", "", 13, Color.white);
            Stretch(_flows, 0f, 0f, 0f, -410f);

            _root.SetActive(false);
        }

        static void Stretch(Text text, float left, float topNormalized, float right, float topOffset)
        {
            var rt = (RectTransform)text.transform;
            rt.anchorMin = new Vector2(0f, topNormalized);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-right, topOffset);
            rt.pivot = new Vector2(0f, 1f);
        }

        void Refresh()
        {
            if (!_open || _summary == null || _spreads == null || _sim == null)
                return;

            var snap = _sim.LatestSnapshot;
            var state = _sim.State;
            var shortageIdx = snap.GoodsShortageIndex > 0f
                ? snap.GoodsShortageIndex
                : state.GoodsShortageIndex;
            var surplusIdx = snap.GoodsSurplusIndex > 0f
                ? snap.GoodsSurplusIndex
                : state.GoodsSurplusIndex;
            var trade = state.TradeBalance;
            var emp = Mathf.RoundToInt(state.EmploymentRate * 100f);
            var zones = Mathf.Max(1, state.MarketZoneCount);
            var spread = Mathf.Max(1f, state.MaxPartitionPriceSpread);

            _summary.text =
                $"Shortage pressure {Mathf.RoundToInt(shortageIdx * 100f)}% · " +
                $"Surplus {Mathf.RoundToInt(surplusIdx * 100f)}%\n" +
                $"Employment {emp}% · Trade {(trade >= 0 ? "+" : "")}{trade:N0}/mo · " +
                $"Inter-zone {state.InterZoneTradeVolume:N0}/d\n" +
                $"Markets {zones} · Spread ×{spread:F2} · " +
                $"Friction ×{state.MeanInterZoneFriction:F2} · " +
                $"Delivery {Mathf.RoundToInt(Mathf.Clamp01(state.MeanGoodsDeliveryDelay) * 100f)}% · " +
                $"Transport cost {Mathf.RoundToInt(Mathf.Clamp01(state.GoodsTransportCostIndex) * 100f)}%\n" +
                $"Bilateral {Mathf.Max(0, state.BilateralRouteCount)} · " +
                $"notional {state.BilateralTradeValue:N0}/mo · " +
                $"freight {Mathf.Max(0f, state.MeanFreightMonths):0.#} mo";

            var hasImbalances = _sim.TryGetGoodImbalances(out var shortages, out var surpluses);
            var sb = new StringBuilder(256);
            sb.AppendLine("Shortages");
            if (hasImbalances)
            {
                if (shortages.Length == 0)
                    sb.AppendLine("  (balanced)");
                else
                {
                    for (var i = 0; i < shortages.Length; i++)
                    {
                        var row = shortages[i];
                        sb.Append("  • ")
                            .Append(UguiPanelBuilder.FormatGoodName(row.Name))
                            .Append("  −")
                            .Append(UguiPanelBuilder.FormatUnits(row.Magnitude))
                            .AppendLine();
                    }
                }
            }
            else
            {
                AppendSnapshotImbalances(sb, snap.ShortageGoods, "  (no live economy)");
            }

            _shortages.text = sb.ToString().TrimEnd();
            _shortages.color = shortageIdx >= 0.35f ? UguiPanelBuilder.Warn : Color.white;

            sb.Clear();
            sb.AppendLine("Surpluses");
            if (hasImbalances && surpluses.Length > 0)
            {
                for (var i = 0; i < surpluses.Length; i++)
                {
                    var row = surpluses[i];
                    sb.Append("  • ")
                        .Append(UguiPanelBuilder.FormatGoodName(row.Name))
                        .Append("  +")
                        .Append(UguiPanelBuilder.FormatUnits(row.Magnitude))
                        .AppendLine();
                }
            }
            else if (!hasImbalances)
            {
                AppendSnapshotImbalances(sb, snap.SurplusGoods, "  (no live economy)");
            }
            else
            {
                sb.AppendLine("  (none)");
            }

            _surpluses.text = sb.ToString().TrimEnd();

            sb.Clear();
            sb.Append("Partition prices");
            if (zones > 1)
                sb.Append(" (").Append(zones).Append(" markets)");
            sb.AppendLine();
            var spreads = _sim.GetAnchorZonePriceSpreads();
            if (zones <= 1 || spreads.Length == 0)
            {
                sb.AppendLine(zones <= 1
                    ? "  (single market — grow population to split)"
                    : "  (awaiting price divergence)");
            }
            else
            {
                var shown = 0;
                for (var i = 0; i < spreads.Length && shown < 5; i++)
                {
                    var row = spreads[i];
                    if (row.MinPrice <= 0f || row.MaxPrice <= row.MinPrice * 1.01f)
                        continue;
                    sb.Append("  • ")
                        .Append(UguiPanelBuilder.FormatGoodName(row.Name))
                        .Append("  ")
                        .Append(UguiPanelBuilder.FormatUnits(row.MinPrice))
                        .Append("–")
                        .Append(UguiPanelBuilder.FormatUnits(row.MaxPrice))
                        .Append("  avg ")
                        .Append(UguiPanelBuilder.FormatUnits(row.CityAvgPrice))
                        .AppendLine();
                    shown++;
                }

                if (shown == 0)
                    sb.AppendLine("  (prices aligned across districts)");
            }

            _spreads.text = sb.ToString().TrimEnd();
            _spreads.color = spread >= 1.2f ? UguiPanelBuilder.Warn : Color.white;

            sb.Clear();
            sb.AppendLine("Production flows");
            var flows = _sim.GetTopGoodFlows(8);
            if (flows.Length == 0)
            {
                sb.AppendLine("  (awaiting market activity)");
            }
            else
            {
                for (var i = 0; i < flows.Length; i++)
                {
                    var flow = flows[i];
                    sb.Append("  • ")
                        .Append(UguiPanelBuilder.FormatGoodName(flow.Name))
                        .Append("  ")
                        .Append(UguiPanelBuilder.FormatUnits(flow.Production))
                        .Append(" prod / ")
                        .Append(UguiPanelBuilder.FormatUnits(flow.Demand))
                        .Append(" dem  ")
                        .Append(UguiPanelBuilder.FormatInventoryRate(flow.InventoryRate))
                        .AppendLine();
                }
            }

            _flows.text = sb.ToString().TrimEnd();
        }

        static void AppendSnapshotImbalances(
            StringBuilder sb,
            SimSnapshot.GoodImbalanceSnapshot[] rows,
            string emptyLine)
        {
            if (rows == null || rows.Length == 0)
            {
                sb.AppendLine(emptyLine);
                return;
            }

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                sb.Append("  • Good #")
                    .Append(row.GoodId)
                    .Append("  ")
                    .Append(UguiPanelBuilder.FormatUnits(row.Score))
                    .AppendLine();
            }
        }
    }
}
