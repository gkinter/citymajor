using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>City ordinances panel — toggle with L. Mirrors web LawPanel.tsx.</summary>
    public sealed class LawPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/LawPanel.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        VisualElement _statsRoot;
        VisualElement _togglesRoot;
        Label _fallback;
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
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
                    Debug.LogError($"[CityMajor] Missing law panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 114;
            BindElements();
        }

        void BindElements()
        {
            var docRoot = _document.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("law-root");
            _statsRoot = docRoot.Q<VisualElement>("law-stats");
            _togglesRoot = docRoot.Q<VisualElement>("law-toggles");
            _fallback = docRoot.Q<Label>("law-fallback");
            _closeBtn = docRoot.Q<Button>("law-close");
            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
                Refresh();
        }

        void OnState(CitySimState _) { if (_open) Refresh(); }

        void Refresh()
        {
            if (_statsRoot == null || _togglesRoot == null)
                return;

            var state = _sim.State;
            var hasData = state.LawDefinitionCount > 0;

            if (_fallback != null)
            {
                _fallback.style.display = hasData ? DisplayStyle.None : DisplayStyle.Flex;
                _fallback.text = hasData
                    ? ""
                    : "Law catalog loads from base/data/laws/laws.json via Forge.SimCore.";
            }

            _statsRoot.Clear();
            AddStat("Definitions", state.LawDefinitionCount.ToString());
            AddStat("Active", state.ActiveLawCount.ToString());

            _togglesRoot.Clear();
            if (!string.IsNullOrEmpty(state.SampleLawId))
            {
                var row = new VisualElement();
                row.AddToClassList("law-row");
                var name = new Label(state.SampleLawName);
                name.AddToClassList("law-row__name");
                row.Add(name);

                var toggle = new Toggle { value = state.SampleLawActive };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (_sim.SetLawActive(state.SampleLawId, evt.newValue))
                        Refresh();
                });
                row.Add(toggle);
                _togglesRoot.Add(row);
            }

            var soon = new Label("Full ordinance catalog — coming soon");
            soon.AddToClassList("panel-note");
            _togglesRoot.Add(soon);
        }

        void AddStat(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("citizen-stat");
            row.Add(new Label(label));
            row.Add(new Label(value) { name = "value" });
            _statsRoot.Add(row);
        }
    }
}
