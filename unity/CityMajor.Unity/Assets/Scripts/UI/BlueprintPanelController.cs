using CityMajor.Platform;
using CityMajor.Save;
using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>District blueprint export panel — toggle with P. CMJR chunk 0x02 header stub (v2.5).</summary>
    public sealed class BlueprintPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/BlueprintPanel.uxml";
        const int DefaultSliceSize = 32;
        const int DefaultMapSize = 256;
        const string DefaultDisplayName = "District";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        VisualElement _dimsRoot;
        Label _status;
        Button _exportBtn;
        Button _closeBtn;
        bool _open;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            Refresh();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
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
                    Debug.LogError($"[CityMajor] Missing blueprint panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 115;
            BindElements();
        }

        void BindElements()
        {
            var docRoot = _document.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("blueprint-root");
            _dimsRoot = docRoot.Q<VisualElement>("blueprint-dims");
            _status = docRoot.Q<Label>("blueprint-status");
            _exportBtn = docRoot.Q<Button>("blueprint-export");
            _closeBtn = docRoot.Q<Button>("blueprint-close");

            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
            if (_exportBtn != null)
                _exportBtn.clicked += OnExportClicked;
        }

        void OnDestroy()
        {
            if (_exportBtn != null)
                _exportBtn.clicked -= OnExportClicked;
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
                Refresh();
        }

        void Refresh()
        {
            if (_dimsRoot == null)
                return;

            var mapSize = DefaultMapSize;
            var originX = mapSize / 2 - DefaultSliceSize / 2;
            var originZ = mapSize / 2 - DefaultSliceSize / 2;

            _dimsRoot.Clear();
            AddDim("Origin X", originX.ToString());
            AddDim("Origin Z", originZ.ToString());
            AddDim("Width", DefaultSliceSize.ToString());
            AddDim("Height", DefaultSliceSize.ToString());
            AddDim("Chunk", $"0x{BlueprintChunkIds.DistrictSlice:X2}");

            if (_status != null)
                _status.text = "Header-only export — full CMJR slice writer pending v2.5";
        }

        void AddDim(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("blueprint-dim-row");
            row.Add(new Label(label) { name = "label" });
            row.Add(new Label(value) { name = "value" });
            _dimsRoot.Add(row);
        }

        void OnExportClicked()
        {
            var mapSize = DefaultMapSize;
            var originX = mapSize / 2 - DefaultSliceSize / 2;
            var originZ = mapSize / 2 - DefaultSliceSize / 2;

            var header = new BlueprintSliceHeader
            {
                OriginTileX = originX,
                OriginTileZ = originZ,
                WidthTiles = DefaultSliceSize,
                HeightTiles = DefaultSliceSize,
                DisplayName = DefaultDisplayName
            };

            var bytes = BlueprintSliceWriter.WriteHeaderOnly(header);
            Debug.Log($"[CityMajor] Blueprint header exported — {bytes.Length} bytes (chunk 0x02 stub)");

            SteamWorkshopStub.PublishBlueprint(bytes, header.DisplayName);

            if (_status != null)
                _status.text = $"Exported header — {bytes.Length} bytes (logged, not saved)";
        }
    }
}
