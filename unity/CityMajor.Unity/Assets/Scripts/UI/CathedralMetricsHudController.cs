using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CityMajor.UI
{
    /// <summary>
    /// Screen-Space Overlay Canvas (UGUI Text, TMP when Unity.TextMeshPro is present) for Cathedral metrics:
    /// rent burden, vacancy, unemployment, mode shares, approval, power/water, goods prices.
    /// </summary>
    public sealed class CathedralMetricsHudController : MonoBehaviour
    {
        const int SortingOrder = 120;
        const float PanelWidth = 320f;
        const float PanelPad = 12f;

        CitySimBridge _sim;
        Canvas _canvas;
        RectTransform _panel;
        Text _title;
        Text _body;
        Component _tmpBody;
        bool _useTmp;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            _sim = sim;
            EnsureCanvas();

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

        void EnsureCanvas()
        {
            if (_canvas != null)
                return;

            var canvasGo = new GameObject("CathedralMetricsCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = CreatePanel(canvasGo.transform);
            CreateLabels(_panel);
        }

        static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -140f);
            rt.sizeDelta = new Vector2(PanelWidth, 240f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.06f, 0.09f, 0.14f, 0.82f);
            image.raycastTarget = false;

            return rt;
        }

        void CreateLabels(RectTransform panel)
        {
            _useTmp = TryCreateTmpLabels(panel);
            if (_useTmp)
                return;

            _title = CreateUguiText(panel, "Title", 15, FontStyle.Bold, new Vector2(PanelPad, -PanelPad),
                new Vector2(PanelWidth - PanelPad * 2f, 22f));
            _title.text = "Cathedral";
            _title.color = new Color(0.85f, 0.9f, 0.95f, 1f);

            _body = CreateUguiText(panel, "Body", 13, FontStyle.Normal, new Vector2(PanelPad, -36f),
                new Vector2(PanelWidth - PanelPad * 2f, 190f));
            _body.alignment = TextAnchor.UpperLeft;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.color = new Color(0.78f, 0.84f, 0.9f, 1f);
            _body.text = "—";
        }

        static Text CreateUguiText(RectTransform parent, string name, int size, FontStyle style,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        bool TryCreateTmpLabels(RectTransform panel)
        {
            var tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType == null)
                return false;

            var title = CreateTmp(tmpType, panel, "Title", 15f, true,
                new Vector2(PanelPad, -PanelPad), new Vector2(PanelWidth - PanelPad * 2f, 22f), "Cathedral");
            _tmpBody = CreateTmp(tmpType, panel, "Body", 13f, false,
                new Vector2(PanelPad, -36f), new Vector2(PanelWidth - PanelPad * 2f, 190f), "—");
            return title != null && _tmpBody != null;
        }

        static Component CreateTmp(System.Type tmpType, RectTransform parent, string name, float size, bool bold,
            Vector2 anchoredPos, Vector2 sizeDelta, string initial)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.AddComponent(tmpType);
            tmpType.GetProperty("text")?.SetValue(tmp, initial);
            tmpType.GetProperty("fontSize")?.SetValue(tmp, size);
            tmpType.GetProperty("raycastTarget")?.SetValue(tmp, false);

            if (bold)
            {
                var styleType = System.Type.GetType("TMPro.FontStyles, Unity.TextMeshPro");
                if (styleType != null)
                    tmpType.GetProperty("fontStyle")?.SetValue(tmp, System.Enum.Parse(styleType, "Bold"));
            }

            var color = name == "Title"
                ? new Color(0.85f, 0.9f, 0.95f, 1f)
                : new Color(0.78f, 0.84f, 0.9f, 1f);
            tmpType.GetProperty("color")?.SetValue(tmp, color);

            if (name == "Body")
            {
                var alignmentType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                if (alignmentType != null)
                    tmpType.GetProperty("alignment")?.SetValue(tmp, System.Enum.Parse(alignmentType, "TopLeft"));
            }

            return tmp;
        }

        void OnStateChanged(CitySimState state) => ApplyState(state);

        void ApplyState(CitySimState state)
        {
            if (_panel == null)
                EnsureCanvas();

            var text = FormatMetrics(state, _sim?.LatestActiveEvents);
            if (_useTmp && _tmpBody != null)
            {
                _tmpBody.GetType().GetProperty("text")?.SetValue(_tmpBody, text);
                return;
            }

            if (_body != null)
                _body.text = text;
        }

        internal static string FormatMetrics(CitySimState state, Forge.SimWasm.ActiveEventDto[] activeEvents = null)
        {
            var rent = Pct(state.MeanRentBurden);
            var vacancy = Pct(state.ResidentialVacancy);
            var unemployment = Pct(state.UnemploymentRate);
            var approval = Pct(state.Approval);
            var power = Pct(state.PowerCoverageFraction);
            var water = Pct(state.WaterCoverageFraction);
            var car = Pct(state.CarModeShare);
            var transit = Pct(state.TransitModeShare);
            var walk = Pct(state.WalkModeShare);

            var goods = state.HasGoodsPrices
                ? $"Food {state.FoodAvgPrice:0.00} · Water {state.WaterAvgPrice:0.00} · Steel {state.SteelAvgPrice:0.00}"
                : $"Shortage {Pct(state.GoodsShortageIndex)} · Surplus {Pct(state.GoodsSurplusIndex)}";

            var eventCount = SimActiveEventHeadlines.ResolveCount(activeEvents);
            if (eventCount <= 0)
                eventCount = state.ActiveEventCount;
            var eventLine = SimActiveEventHeadlines.TryGetPriorityEvent(activeEvents, out var priority)
                ? $"Events [{eventCount}]  {SimActiveEventHeadlines.FormatPriorityHeadline(priority)}"
                : $"Events [{eventCount}]";

            return
                $"Rent burden  {rent}\n" +
                $"Vacancy      {vacancy}\n" +
                $"Unemployment {unemployment}\n" +
                $"Modes  Car {car} · Transit {transit} · Walk {walk}\n" +
                $"Approval     {approval}\n" +
                $"Power {power} · Water {water}\n" +
                $"Goods  {goods}\n" +
                eventLine;
        }

        static string Pct(float fraction) =>
            $"{Mathf.RoundToInt(Mathf.Max(0f, fraction) * 100f)}%";
    }
}
