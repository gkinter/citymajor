using CityMajor.Input;
using CityMajor.Sim;
using Forge.SimCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Bottom zoning palette — R/C/I + Office/Mixed/Ag/Park with P2.1 era gates and
    /// Low/Med/High density (U3.4 era/tech gates). Paints via <see cref="CitySimBridge.PaintZone"/>.
    /// </summary>
    public sealed class ZoningToolbarController : MonoBehaviour
    {
        const string ToolbarPath = "Assets/UI/ZoningToolbar.uxml";

        CitySimBridge _sim;
        ZonePaintTool _zone;
        UIDocument _document;
        VisualElement _tiers;
        Label _status;
        Button _densityLow;
        Button _densityMed;
        Button _densityHigh;
        readonly System.Collections.Generic.Dictionary<ZonePaintTool.ZoneKind, Button> _tierButtons = new();

        public void Configure(CitySimBridge sim, ZonePaintTool zone)
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshotChanged;

            _sim = sim;
            _zone = zone;
            EnsureUi();
            RebuildTiers();
            RefreshSelection();

            if (_sim != null)
                _sim.OnSnapshotChanged += OnSnapshotChanged;
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshotChanged;
        }

        void Update() => RefreshSelection();

        void OnSnapshotChanged(SimSnapshot _)
        {
            RebuildTiers();
            RefreshDensityButtons();
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
                    Debug.LogError($"[CityMajor] Missing zoning toolbar at {ToolbarPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 96;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _tiers = root.Q<VisualElement>("zoning-tiers");
            _status = root.Q<Label>("zoning-status");
            _densityLow = root.Q<Button>("density-low");
            _densityMed = root.Q<Button>("density-med");
            _densityHigh = root.Q<Button>("density-high");

            _densityLow?.RegisterCallback<ClickEvent>(_ => SetDensity(1));
            _densityMed?.RegisterCallback<ClickEvent>(_ => SetDensity(2));
            _densityHigh?.RegisterCallback<ClickEvent>(_ => SetDensity(3));
            RefreshDensityButtons();
        }

        void SetDensity(byte density)
        {
            if (_zone == null)
                return;

            if (!_zone.TrySetZoneDensity(density) && _status != null)
                _status.text = _zone.DensityLockReason(density);
            RefreshSelection();
        }

        void RefreshDensityButtons()
        {
            SetDensityButtonState(_densityLow, 1);
            SetDensityButtonState(_densityMed, 2);
            SetDensityButtonState(_densityHigh, 3);
        }

        void SetDensityButtonState(Button btn, byte density)
        {
            if (btn == null)
                return;

            var unlocked = _zone == null || _zone.IsDensityUnlocked(density);
            btn.SetEnabled(unlocked);
            btn.tooltip = unlocked
                ? $"{ZonePaintTool.DensityLabel(density)} density"
                : (_zone?.DensityLockReason(density) ?? "Locked");
        }

        void RebuildTiers()
        {
            if (_tiers == null)
                BindElements();
            if (_tiers == null)
                return;

            _tiers.Clear();
            _tierButtons.Clear();

            var erase = new Button { text = "✕" };
            erase.AddToClassList("zoning-btn");
            erase.tooltip = "Erase zone (0)";
            erase.clicked += () =>
            {
                _zone?.SetActiveZone(ZonePaintTool.ZoneKind.None);
                RefreshSelection();
            };
            _tiers.Add(erase);
            _tierButtons[ZonePaintTool.ZoneKind.None] = erase;

            var supportsExtended = _sim != null && _sim.UsesForgeSimCore;
            foreach (var tier in ZoneTiers.Paintable)
            {
                if (!ZoneTiers.IsVisible(tier, supportsExtended))
                    continue;

                var unlocked = ZoneTiers.IsUnlocked(tier, _sim);
                var btn = new Button { text = tier.ShortLabel };
                btn.AddToClassList("zoning-btn");
                btn.SetEnabled(unlocked);
                btn.tooltip = unlocked
                    ? $"{tier.Label} ({HotkeyHint(tier.Kind)})"
                    : ZoneTiers.LockReason(tier, _sim);

                var kind = tier.Kind;
                btn.clicked += () =>
                {
                    if (_zone == null)
                        return;
                    if (!_zone.TrySetActiveZone(kind) && _status != null)
                        _status.text = _zone.ZoneLockReason(kind);
                    RefreshSelection();
                };

                _tiers.Add(btn);
                _tierButtons[kind] = btn;
            }

            RefreshDensityButtons();
        }

        void RefreshSelection()
        {
            if (_zone == null)
                return;

            foreach (var pair in _tierButtons)
            {
                var selected = pair.Key == _zone.ActiveZone;
                pair.Value.EnableInClassList("zoning-btn--active", selected);
            }

            RefreshDensityButtons();

            var density = ZonePaintTool.NormalizeDensity(_zone.ZoneDensity);
            _densityLow?.EnableInClassList("zoning-btn--active", density == 1);
            _densityMed?.EnableInClassList("zoning-btn--active", density == 2);
            _densityHigh?.EnableInClassList("zoning-btn--active", density == 3);

            if (_status != null)
            {
                if (_zone.ActiveZone == ZonePaintTool.ZoneKind.None)
                    _status.text = "Erase · LMB drag";
                else if (!_zone.IsZoneUnlocked(_zone.ActiveZone))
                    _status.text = _zone.ZoneLockReason(_zone.ActiveZone);
                else if (!_zone.IsDensityUnlocked(density))
                    _status.text = _zone.DensityLockReason(density);
                else
                    _status.text =
                        $"{_zone.ActiveZoneLabel()} · D cycles density";
            }
        }

        static string HotkeyHint(ZonePaintTool.ZoneKind kind) => kind switch
        {
            ZonePaintTool.ZoneKind.Residential => "1",
            ZonePaintTool.ZoneKind.Commercial => "2",
            ZonePaintTool.ZoneKind.Industrial => "3",
            ZonePaintTool.ZoneKind.Office => "5",
            ZonePaintTool.ZoneKind.Mixed => "6",
            ZonePaintTool.ZoneKind.Agricultural => "7",
            ZonePaintTool.ZoneKind.Park => "8",
            _ => "0",
        };
    }
}
