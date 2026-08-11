using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityMajor.UI
{
    /// <summary>
    /// Bottom-center UGUI toolbar buttons for Economy [E], Politics [G], Trade [Y].
    /// Ensures an EventSystem exists so UGUI buttons receive clicks.
    /// </summary>
    public sealed class SimPanelToolbarController : MonoBehaviour
    {
        EconomyPanelController _economy;
        PoliticsPanelController _politics;
        TradeStripController _trade;
        Button _economyBtn;
        Button _politicsBtn;
        Button _tradeBtn;
        Text _economyLabel;
        Text _politicsLabel;
        Text _tradeLabel;

        public void Configure(
            EconomyPanelController economy,
            PoliticsPanelController politics,
            TradeStripController trade = null)
        {
            _economy = economy;
            _politics = politics;
            _trade = trade;
            EnsureUi();
            EnsureEventSystem();
        }

        void Update()
        {
            if (_economyLabel != null && _economy != null)
                _economyLabel.text = _economy.IsOpen ? "Economy ●" : "Economy [E]";
            if (_politicsLabel != null && _politics != null)
                _politicsLabel.text = _politics.IsOpen ? "Politics ●" : "Politics [G]";
            if (_tradeLabel != null && _trade != null)
                _tradeLabel.text = _trade.IsOpen ? "Trade ●" : "Trade [Y]";
        }

        void EnsureUi()
        {
            if (_economyBtn != null)
                return;

            var canvas = UguiPanelBuilder.EnsureOverlayCanvas(gameObject, "SimPanelToolbarCanvas", 210);
            var bar = UguiPanelBuilder.CreatePanel(
                canvas.transform,
                "PanelToolbar",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(420f, 44f));
            bar.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.85f);

            _economyBtn = UguiPanelBuilder.AddButton(bar, "EconomyBtn", "Economy [E]", new Vector2(124f, 32f));
            var eRt = (RectTransform)_economyBtn.transform;
            eRt.anchorMin = new Vector2(0f, 0.5f);
            eRt.anchorMax = new Vector2(0f, 0.5f);
            eRt.pivot = new Vector2(0f, 0.5f);
            eRt.anchoredPosition = new Vector2(8f, 0f);
            _economyLabel = _economyBtn.GetComponentInChildren<Text>();
            _economyBtn.onClick.AddListener(() => _economy?.Toggle());

            _politicsBtn = UguiPanelBuilder.AddButton(bar, "PoliticsBtn", "Politics [G]", new Vector2(124f, 32f));
            var pRt = (RectTransform)_politicsBtn.transform;
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = new Vector2(0f, 0f);
            _politicsLabel = _politicsBtn.GetComponentInChildren<Text>();
            _politicsBtn.onClick.AddListener(() => _politics?.Toggle());

            _tradeBtn = UguiPanelBuilder.AddButton(bar, "TradeBtn", "Trade [Y]", new Vector2(124f, 32f));
            var tRt = (RectTransform)_tradeBtn.transform;
            tRt.anchorMin = new Vector2(1f, 0.5f);
            tRt.anchorMax = new Vector2(1f, 0.5f);
            tRt.pivot = new Vector2(1f, 0.5f);
            tRt.anchoredPosition = new Vector2(-8f, 0f);
            _tradeLabel = _tradeBtn.GetComponentInChildren<Text>();
            _tradeBtn.onClick.AddListener(() => _trade?.Toggle());
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
