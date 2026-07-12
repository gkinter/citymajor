using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Bottom news ticker scaffold — surfaces Herald headline placeholder.</summary>
    public sealed class EventTickerController : MonoBehaviour
    {
        const string TickerPath = "Assets/UI/EventTicker.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _line;

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

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(TickerPath);
                if (asset == null)
                {
                    // Minimal runtime fallback if UXML missing.
                    _document.sortingOrder = 50;
                    _line = new Label("CityMajor — press H for Herald");
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

            _line.text =
                $"Pop {state.Population:N0} · Approval {(state.Approval * 100f):F0}% · Hour {state.TimeOfDay:F0} · Rush ×{state.RushMultiplier:F1} — H Herald · C Citizens";
        }
    }
}
