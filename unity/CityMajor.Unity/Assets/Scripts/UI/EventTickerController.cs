using CityMajor.Net;
using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Bottom news ticker — prefers live sim Herald events
    /// (housing_crisis, housing_shortage, approval_unrest), else metric bucket (mirrors web NewsTicker).
    /// </summary>
    public sealed class EventTickerController : MonoBehaviour
    {
        const string TickerPath = "Assets/UI/EventTicker.uxml";
        const string IdleMessage = "City Major Gazette — municipal wires quiet";

        CitySimBridge _sim;
        UIDocument _document;
        Label _line;
        Label _countBadge;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnStateChanged += OnState;
                OnState(_sim.State);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(TickerPath);
                if (asset == null)
                {
                    _document.sortingOrder = 50;
                    _line = new Label(IdleMessage);
                    _document.rootVisualElement?.Add(_line);
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 50;
            var tree = _document.rootVisualElement;
            if (tree == null)
                return;

            EnsureCountBadge(tree);
            _line = tree.Q<Label>("ticker-line");
        }

        void EnsureCountBadge(VisualElement root)
        {
            _countBadge = root.Q<Label>("ticker-event-count");
            if (_countBadge != null)
                return;

            _countBadge = new Label();
            _countBadge.name = "ticker-event-count";
            _countBadge.AddToClassList("ticker-event-count");
            _countBadge.style.display = DisplayStyle.None;
            _countBadge.style.marginRight = 8;
            _countBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _countBadge.style.color = new Color(1f, 0.78f, 0.35f, 1f);

            var tickerRoot = root.Q<VisualElement>("ticker-root") ?? root;
            tickerRoot.Insert(0, _countBadge);
        }

        void OnState(CitySimState state)
        {
            if (_line == null)
                EnsureUi();

            if (_line == null)
                return;

            var snap = _sim?.LatestSnapshot;
            var active = _sim?.LatestActiveEvents;
            var count = SimActiveEventHeadlines.ResolveCount(active);

            if (_countBadge != null)
            {
                var badge = SimActiveEventHeadlines.FormatCountBadge(count);
                if (badge != null)
                {
                    _countBadge.text = badge;
                    _countBadge.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _countBadge.text = "";
                    _countBadge.style.display = DisplayStyle.None;
                }
            }

            if (snap != null || count > 0)
            {
                var evt = NarrativeTemplates.FromActiveEventsOrSnapshot(active, snap, state);
                var headline = string.IsNullOrEmpty(evt.Headline) ? IdleMessage : evt.Headline;
                _line.text = headline;
                return;
            }

            _line.text =
                $"Pop {state.Population:N0} · Approval {(state.Approval * 100f):F0}% · H Herald";
        }
    }
}
