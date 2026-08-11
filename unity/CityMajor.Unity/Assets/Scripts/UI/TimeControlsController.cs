using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Pause + sim speed. Keys: Space pause; <c>[</c>/<c>]</c>/<c>\</c> (or keypad 1/2/4) for 1×/2×/4×.
    /// Does not use 5/6/7 — those paint Office / Mixed / Ag zones.
    /// </summary>
    public sealed class TimeControlsController : MonoBehaviour
    {
        CitySimBridge _sim;
        UIDocument _document;
        Label _status;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            Refresh();
        }

        void Update()
        {
            if (_sim == null)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                _sim.Paused = !_sim.Paused;
                Refresh();
            }

            // Bracket keys avoid clash with ZonePaintTool Office/Mixed/Ag (5/6/7).
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
                SetSpeed(1f);
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
                SetSpeed(2f);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backslash) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
                SetSpeed(4f);
        }

        void SetSpeed(float scale)
        {
            _sim.Paused = false;
            _sim.TimeScale = scale;
            Refresh();
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            _document.sortingOrder = 90;
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            if (root.childCount == 0)
            {
                var bar = new VisualElement();
                bar.AddToClassList("time-controls");
                _status = new Label("▶ 1×");
                _status.name = "time-status";
                bar.Add(_status);
                root.Add(bar);
            }
            else
            {
                _status = root.Q<Label>("time-status");
            }
        }

        void Refresh()
        {
            if (_status == null || _sim == null)
                return;

            _status.text = _sim.Paused
                ? "⏸ Paused"
                : $"▶ {_sim.TimeScale:0}×";
        }
    }
}
