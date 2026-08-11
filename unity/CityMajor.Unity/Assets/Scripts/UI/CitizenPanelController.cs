using System.Text;
using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Citizen drill-down panel — toggle with C. Mirrors web CitizenPanel.tsx (L2 sample).</summary>
    public sealed class CitizenPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/CitizenPanel.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        VisualElement _statsRoot;
        VisualElement _listRoot;
        Label _fallback;
        Label _selected;
        Button _closeBtn;
        string _selectedId = "";

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
            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        public void SelectHousehold(string householdId)
        {
            _selectedId = householdId ?? "";
            Refresh();
            SetOpen(true);
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
                    Debug.LogError($"[CityMajor] Missing citizen panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 112;
            BindElements();
        }

        void BindElements()
        {
            var docRoot = _document.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("citizen-root");
            _statsRoot = docRoot.Q<VisualElement>("citizen-stats");
            _listRoot = docRoot.Q<VisualElement>("citizen-list");
            _fallback = docRoot.Q<Label>("citizen-fallback");
            _selected = docRoot.Q<Label>("citizen-selected");
            _closeBtn = docRoot.Q<Button>("citizen-close");
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

        void OnState(CitySimState state) { if (_open) Refresh(); }

        void Refresh()
        {
            if (_statsRoot == null || _listRoot == null)
                return;

            var state = _sim.State;
            _statsRoot.Clear();
            AddStat("Population", state.Population.ToString("N0"));
            AddStat("Households", state.HouseholdCount.ToString());
            AddStat("Happiness", $"{state.Happiness * 100f:F0}%");
            AddStat("Hour", $"{state.TimeOfDay:F1}h");

            _listRoot.Clear();
            var households = state.Households;
            var hasL2 = households is { Length: > 0 };

            if (_fallback != null)
            {
                _fallback.style.display = hasL2 ? DisplayStyle.None : DisplayStyle.Flex;
                _fallback.text = hasL2
                    ? ""
                    : "Population L2 sample empty — grow residential zones and wait for households.";
            }

            if (hasL2)
            {
                foreach (var h in households)
                {
                    var row = new Button { text = FormatRow(h) };
                    row.AddToClassList("citizen-row");
                    if (h.Id == _selectedId)
                        row.AddToClassList("citizen-row--selected");
                    var id = h.Id;
                    row.clicked += () =>
                    {
                        _selectedId = id;
                        Refresh();
                    };
                    _listRoot.Add(row);
                }
            }

            if (_selected != null)
            {
                var show = !string.IsNullOrEmpty(_selectedId);
                _selected.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                {
                    HouseholdPreview? match = null;
                    if (hasL2)
                    {
                        foreach (var h in households)
                        {
                            if (h.Id != _selectedId) continue;
                            match = h;
                            break;
                        }
                    }

                    _selected.text = match.HasValue
                        ? FormatSelected(match.Value)
                        : $"Selected: {_selectedId}";
                }
            }
        }

        void AddStat(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("citizen-stat");
            row.Add(new Label(label) { name = "label" });
            row.Add(new Label(value) { name = "value" });
            _statsRoot.Add(row);
        }

        static string FormatSelected(HouseholdPreview h)
        {
            var job = h.WorkBuildingId > 0 ? $"Job bldg #{h.WorkBuildingId}" : "Unemployed";
            return
                $"Selected: {h.Id}\n" +
                $"Home bldg #{h.HomeBuildingId} · {job}\n" +
                $"Commute {h.CommuteMin:F0}m · Rent burden {h.RentBurden * 100f:F0}% · " +
                $"({h.TileX},{h.TileZ})";
        }

        static string FormatRow(HouseholdPreview h)
        {
            var sb = new StringBuilder();
            sb.Append(h.Id);
            sb.Append(" · ");
            sb.Append((h.Happiness * 100f).ToString("F0"));
            sb.Append("% · ");
            sb.Append(h.WorkBuildingId > 0 ? $"job #{h.WorkBuildingId}" : "no job");
            sb.Append(" · ");
            sb.Append(h.CommuteMin.ToString("F0"));
            sb.Append("m · rent ");
            sb.Append((h.RentBurden * 100f).ToString("F0"));
            sb.Append('%');
            return sb.ToString();
        }
    }
}
