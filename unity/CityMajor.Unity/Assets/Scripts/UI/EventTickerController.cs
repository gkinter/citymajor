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
            _line = _document.rootVisualElement?.Q<Label>("ticker-line");
        }

        void OnState(CitySimState state)
        {
            if (_line == null)
                return;

            var snap = _sim?.LatestSnapshot;
            var active = _sim?.LatestActiveEvents;
            var count = SimActiveEventHeadlines.ResolveCount(active);
            if (snap != null || count > 0)
            {
                var evt = NarrativeTemplates.FromActiveEventsOrSnapshot(active, snap, state);
                var headline = string.IsNullOrEmpty(evt.Headline) ? IdleMessage : evt.Headline;
                _line.text = count > 0 ? $"[{count}] {headline}" : headline;
                return;
            }

            _line.text =
                $"Pop {state.Population:N0} · Approval {(state.Approval * 100f):F0}% · H Herald";
        }
    }
}
