using CityMajor.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Bottom-center road type toolbar — mirrors web RoadTypeToolbar.tsx.
    /// Local / Collector / Highway + Ramp + Bridge / Tunnel → PlaceRoad API flags.
    /// Visible while road paint mode is active (key 4).
    /// </summary>
    public sealed class RoadTypeToolbarController : MonoBehaviour
    {
        const string ToolbarPath = "Assets/UI/RoadTypeToolbar.uxml";

        RoadPaintTool _road;
        UIDocument _document;
        VisualElement _root;
        Button _tier0;
        Button _tier1;
        Button _tier2;
        Button _ramp;
        Button _bridge;
        Button _tunnel;
        bool _visible;

        public void Configure(RoadPaintTool road)
        {
            _road = road;
            EnsureUi();
            RefreshSelection();
        }

        void Update()
        {
            if (_road == null || _root == null)
                return;

            var want = _road.RoadModeActive;
            if (want != _visible)
            {
                _visible = want;
                _root.style.display = want ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_visible)
                RefreshSelection();
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(ToolbarPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing road toolbar at {ToolbarPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 92;
            BindElements();
        }

        void BindElements()
        {
            var docRoot = _document.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("road-toolbar-root");
            _tier0 = docRoot.Q<Button>("road-tier-0");
            _tier1 = docRoot.Q<Button>("road-tier-1");
            _tier2 = docRoot.Q<Button>("road-tier-2");
            _ramp = docRoot.Q<Button>("road-tool-ramp");
            _bridge = docRoot.Q<Button>("road-elev-bridge");
            _tunnel = docRoot.Q<Button>("road-elev-tunnel");

            _tier0?.RegisterCallback<ClickEvent>(_ => OnSelectTier(0));
            _tier1?.RegisterCallback<ClickEvent>(_ => OnSelectTier(1));
            _tier2?.RegisterCallback<ClickEvent>(_ => OnSelectTier(2));
            _ramp?.RegisterCallback<ClickEvent>(_ => OnSelectRamp());
            _bridge?.RegisterCallback<ClickEvent>(_ => OnToggleElevation(RoadPaintTool.RoadElevation.Bridge));
            _tunnel?.RegisterCallback<ClickEvent>(_ => OnToggleElevation(RoadPaintTool.RoadElevation.Tunnel));
        }

        void OnSelectTier(byte tier)
        {
            _road?.SelectTierTool(tier);
            RefreshSelection();
        }

        void OnSelectRamp()
        {
            _road?.SelectRampTool();
            RefreshSelection();
        }

        void OnToggleElevation(RoadPaintTool.RoadElevation mode)
        {
            if (_road == null || _road.PaintRamp)
                return;

            _road.SetElevation(_road.Elevation == mode
                ? RoadPaintTool.RoadElevation.None
                : mode);
            RefreshSelection();
        }

        void RefreshSelection()
        {
            if (_road == null)
                return;

            var tool = _road.ActiveTool;
            SetPressed(_tier0, tool == RoadPaintTool.RoadTool.Local);
            SetPressed(_tier1, tool == RoadPaintTool.RoadTool.Collector);
            SetPressed(_tier2, tool == RoadPaintTool.RoadTool.Highway);
            SetPressed(_ramp, tool == RoadPaintTool.RoadTool.Ramp);

            var elev = _road.Elevation;
            var rampActive = _road.PaintRamp;
            SetPressed(_bridge, !rampActive && elev == RoadPaintTool.RoadElevation.Bridge);
            SetPressed(_tunnel, !rampActive && elev == RoadPaintTool.RoadElevation.Tunnel);
            SetEnabled(_bridge, !rampActive);
            SetEnabled(_tunnel, !rampActive);
        }

        static void SetPressed(Button btn, bool pressed)
        {
            if (btn == null)
                return;
            btn.EnableInClassList("road-toolbar__btn--active", pressed);
        }

        static void SetEnabled(Button btn, bool enabled)
        {
            if (btn == null)
                return;
            btn.SetEnabled(enabled);
            btn.EnableInClassList("road-toolbar__btn--disabled", !enabled);
        }
    }
}
