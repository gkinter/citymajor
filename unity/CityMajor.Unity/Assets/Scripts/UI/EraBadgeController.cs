using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Top-left era badge — mirrors web ResourcesHud era pill (web/lib/era.ts).</summary>
    public sealed class EraBadgeController : MonoBehaviour
    {
        const string BadgePath = "Assets/UI/EraBadge.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        Label _eraLabel;
        VisualElement _badgeRoot;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshotChanged;

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnSnapshotChanged += OnSnapshotChanged;
                ApplyEra(ResolveEra(_sim));
            }
            else
            {
                ApplyEra(EraNames.DefaultEra);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshotChanged;
        }

        static int ResolveEra(CitySimBridge sim) =>
            sim.UsesForgeSimCore ? sim.LatestSnapshot.Era : EraNames.DefaultEra;

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(BadgePath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing era badge at {BadgePath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 101;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _badgeRoot = root.Q<VisualElement>("era-badge-root");
            _eraLabel = root.Q<Label>("era-label");
        }

        void OnSnapshotChanged(SimSnapshot snap) => ApplyEra(snap.Era);

        void ApplyEra(int era)
        {
            if (_eraLabel == null)
                BindElements();

            if (_eraLabel == null)
                return;

            var palette = EraNames.HudEraBadgePalette(era);
            _eraLabel.text = EraNames.HudEraName(era);
            _eraLabel.style.color = palette.Text;

            if (_badgeRoot != null)
            {
                _badgeRoot.style.backgroundColor = palette.Background;
                _badgeRoot.style.borderTopColor = palette.Border;
                _badgeRoot.style.borderRightColor = palette.Border;
                _badgeRoot.style.borderBottomColor = palette.Border;
                _badgeRoot.style.borderLeftColor = palette.Border;
            }
        }
    }
}
