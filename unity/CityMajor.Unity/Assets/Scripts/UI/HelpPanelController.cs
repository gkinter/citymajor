using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Keyboard + overlay reference. Toggle with F1.</summary>
    public sealed class HelpPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/HelpPanel.uxml";

        UIDocument _document;
        VisualElement _root;
        Button _closeBtn;
        bool _open;

        public void Configure(CitySimBridge _)
        {
            EnsureUi();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F1))
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
                    Debug.LogError($"[CityMajor] Missing help panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 120;
            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("help-root");
            _closeBtn = docRoot?.Q<Button>("help-close");
            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
