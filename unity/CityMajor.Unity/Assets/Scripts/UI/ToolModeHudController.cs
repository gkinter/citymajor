using CityMajor.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Bottom-left label showing the active paint/plop tool (Play QA aid).</summary>
    public sealed class ToolModeHudController : MonoBehaviour
    {
        const string HudPath = "Assets/UI/ToolModeHud.uxml";

        BulldozeTool _bulldoze;
        BuildPlopTool _build;
        RoadPaintTool _road;
        ZonePaintTool _zone;
        UIDocument _document;
        Label _label;

        public void Configure(
            ZonePaintTool zone,
            RoadPaintTool road,
            BuildPlopTool build,
            BulldozeTool bulldoze)
        {
            _zone = zone;
            _road = road;
            _build = build;
            _bulldoze = bulldoze;
            EnsureUi();
        }

        void Update() => RefreshLabel();

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(HudPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing tool mode HUD at {HudPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 90;
            _label = _document.rootVisualElement?.Q<Label>("tool-mode-label");
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (_label == null)
                return;

            _label.text = $"Tool: {ResolveModeLabel()}";
        }

        string ResolveModeLabel()
        {
            if (_bulldoze != null && _bulldoze.BulldozeModeActive)
                return "Bulldoze (X)";

            if (_build != null && _build.BuildModeActive)
            {
                var name = string.IsNullOrEmpty(_build.SelectedLabel) ? "Build" : _build.SelectedLabel;
                return $"Plop {name} (B)";
            }

            if (_road != null && _road.RoadModeActive)
                return "Road (4)";

            if (_zone == null)
                return "—";

            return _zone.ActiveZoneLabel();
        }
    }
}
