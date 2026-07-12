using CityMajor.Input;
using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Modern service plop catalog — toggle with B, mirrors web build menu (v1 subset).</summary>
    public sealed class BuildPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/BuildPanel.uxml";

        CitySimBridge _sim;
        BuildPlopTool _plop;
        UIDocument _document;
        VisualElement _root;
        VisualElement _list;
        Label _status;
        Button _closeBtn;
        bool _open;

        public void Configure(CitySimBridge sim, BuildPlopTool plop)
        {
            _sim = sim;
            _plop = plop;
            EnsureUi();
            RefreshList();
        }

        void Update()
        {
            if (_plop != null && _open != _plop.BuildModeActive)
            {
                _open = _plop.BuildModeActive;
                if (_root != null)
                    _root.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_open && _status != null && _plop != null)
                _status.text = _plop.SelectedTypeId > 0
                    ? $"Placing: {_plop.SelectedLabel} (TypeId {_plop.SelectedTypeId}) — click zoned tiles"
                    : "Select a building below";
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
                    Debug.LogError($"[CityMajor] Missing build panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 113;
            BindElements();
        }

        void BindElements()
        {
            var docRoot = _document.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("build-root");
            _list = docRoot.Q<VisualElement>("build-list");
            _status = docRoot.Q<Label>("build-status");
            _closeBtn = docRoot.Q<Button>("build-close");
            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _plop?.SetBuildMode(open, open ? _plop.SelectedTypeId : 0, _plop.SelectedLabel);
        }

        void RefreshList()
        {
            if (_list == null)
                return;

            _list.Clear();
            foreach (var entry in ModernBuildCatalog.ServiceEntries)
            {
                var btn = new Button { text = $"{entry.Category}: {entry.Label}" };
                btn.AddToClassList("build-row");
                var typeId = entry.TypeId;
                var label = entry.Label;
                btn.clicked += () =>
                {
                    _plop?.SelectPloppable(typeId, label);
                    SetOpen(true);
                };
                _list.Add(btn);
            }
        }
    }
}
