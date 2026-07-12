using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Modern-era research catalog (Forge.SimCore filtered tech). Toggle with R.
    /// Enqueues via CitySimBridge → SimHost.EnqueueResearch.
    /// </summary>
    public sealed class ResearchPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/ResearchPanel.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        Label _subtitle;
        Label _rpValue;
        Label _rpRate;
        VisualElement _activeSection;
        Label _activeName;
        Label _activePct;
        VisualElement _activeFill;
        Label _activeEta;
        ScrollView _scroll;
        Button _closeBtn;

        bool _open;
        bool _listDirty = true;
        readonly List<ResearchSystem.TechDefinition> _techs = new();

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshot;

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnSnapshotChanged += OnSnapshot;
                _listDirty = true;
                if (_sim.LatestSnapshot != null)
                    ApplySnapshot(_sim.LatestSnapshot);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshot;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                SetOpen(!_open);
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
                    Debug.LogError($"[CityMajor] Missing research panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 110;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _root = root.Q<VisualElement>("research-root");
            _subtitle = root.Q<Label>("research-subtitle");
            _rpValue = root.Q<Label>("rp-value");
            _rpRate = root.Q<Label>("rp-rate-value");
            _activeSection = root.Q<VisualElement>("active-research");
            _activeName = root.Q<Label>("active-tech-name");
            _activePct = root.Q<Label>("active-tech-pct");
            _activeFill = root.Q<VisualElement>("active-progress-fill");
            _activeEta = root.Q<Label>("active-tech-eta");
            _scroll = root.Q<ScrollView>("research-scroll");
            _closeBtn = root.Q<Button>("research-close");

            if (_closeBtn != null)
                _closeBtn.clicked += () => SetOpen(false);
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
                _listDirty = true;
        }

        void OnSnapshot(SimSnapshot snap) => ApplySnapshot(snap);

        void ApplySnapshot(SimSnapshot snap)
        {
            if (_root == null)
                BindElements();
            if (_root == null)
                return;

            _rpValue.text = snap.ResearchPoints.ToString("0.0");
            _rpRate.text = $"{snap.ResearchRate:0.0}/mo";

            var unlocked = _sim.CountUnlockedTechs();
            var total = _sim?.ResearchTechCount ?? _techs.Count;
            _subtitle.text = $"{unlocked} / {total} technologies unlocked";

            if (snap.CurrentResearchId >= 0)
            {
                _activeSection.style.display = DisplayStyle.Flex;
                var name = _sim?.TechName(snap.CurrentResearchId) ?? $"Tech #{snap.CurrentResearchId}";
                var pct = Mathf.Clamp01(snap.CurrentResearchProgress);
                _activeName.text = name;
                _activePct.text = $"{Mathf.RoundToInt(pct * 100f)}%";
                _activeFill.style.width = Length.Percent(pct * 100f);

                var eta = EstimateMonthsRemaining(snap);
                _activeEta.text = eta <= 0f
                    ? "completing…"
                    : eta < 1f
                        ? "<1 mo remaining"
                        : $"~{eta:0.1} mo remaining";
            }
            else
            {
                _activeSection.style.display = DisplayStyle.None;
            }

            if (_open && _listDirty)
                RebuildTechList(snap);
        }

        void RebuildTechList(SimSnapshot snap)
        {
            _scroll.Clear();
            _techs.Clear();

            if (_sim == null || !_sim.UsesForgeSimCore)
            {
                _scroll.Add(new Label("Forge.SimCore required for research."));
                _listDirty = false;
                return;
            }

            _sim.CollectResearchTechs(_techs);
                _subtitle.text = $"{_sim.CountUnlockedTechs()} / {_techs.Count} technologies unlocked";

            foreach (var tech in _techs)
            {
                if (tech == null)
                    continue;

                var availability = Classify(tech.Id, snap);
                var item = BuildTechRow(tech, availability);
                _scroll.Add(item);
            }

            _listDirty = false;
        }

        VisualElement BuildTechRow(
            ResearchSystem.TechDefinition tech,
            TechAvailability availability)
        {
            var item = new VisualElement();
            item.AddToClassList("research-item");
            item.AddToClassList(availability switch
            {
                TechAvailability.Unlocked => "research-item--unlocked",
                TechAvailability.Researching => "research-item--researching",
                TechAvailability.Available => "research-item--available",
                _ => "",
            });

            var row = new VisualElement();
            row.AddToClassList("research-item-row");
            var name = new Label(tech.Name);
            name.AddToClassList("research-item-name");
            row.Add(name);

            if (availability == TechAvailability.Unlocked)
            {
                var badge = new Label("✓");
                badge.AddToClassList("research-item-cost");
                row.Add(badge);
            }
            else if (availability != TechAvailability.Researching)
            {
                var cost = new Label($"{tech.Cost:0} RP");
                cost.AddToClassList("research-item-cost");
                row.Add(cost);
            }

            item.Add(row);

            var meta = new Label($"{tech.Category}");
            meta.AddToClassList("research-item-meta");
            item.Add(meta);

            if (!string.IsNullOrWhiteSpace(tech.Description))
            {
                var desc = new Label(tech.Description);
                desc.AddToClassList("research-item-desc");
                item.Add(desc);
            }

            if (availability == TechAvailability.Available)
            {
                var btn = new Button(() =>
                {
                    if (_sim.EnqueueResearch(tech.Id))
                        _listDirty = true;
                })
                { text = "Enqueue" };
                btn.AddToClassList("research-enqueue-btn");
                item.Add(btn);
            }

            return item;
        }

        TechAvailability Classify(int techId, SimSnapshot snap)
        {
            if (_sim.IsTechUnlocked(techId))
                return TechAvailability.Unlocked;
            if (snap.CurrentResearchId == techId)
                return TechAvailability.Researching;
            if (_sim.ArePrerequisitesMet(techId))
                return TechAvailability.Available;
            return TechAvailability.Locked;
        }

        float EstimateMonthsRemaining(SimSnapshot snap)
        {
            if (snap.CurrentResearchId < 0 || snap.ResearchRate <= 0f)
                return 0f;

            var cost = _sim?.TechCost(snap.CurrentResearchId) ?? 0f;
            if (cost <= 0f)
                return 0f;

            var remaining = (1f - Mathf.Clamp01(snap.CurrentResearchProgress)) * cost;
            return remaining / snap.ResearchRate;
        }

        enum TechAvailability
        {
            Unlocked,
            Researching,
            Available,
            Locked,
        }
    }
}
