using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>Place civic/service buildings on zoned tiles via SimHost.PlaceBuilding. Toggle with B.</summary>
    public sealed class BuildPlopTool : MonoBehaviour
    {
        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;

        public bool BuildModeActive { get; private set; }
        public int SelectedTypeId { get; private set; }
        public string SelectedLabel { get; private set; } = "";

        public void Configure(Camera cityCamera, ZoneGrid grid, CitySimBridge sim)
        {
            _camera = cityCamera;
            _grid = grid;
            _sim = sim;
        }

        public void SetBuildMode(bool active, int typeId = 0, string label = "")
        {
            BuildModeActive = active;
            if (typeId > 0)
            {
                SelectedTypeId = typeId;
                SelectedLabel = label ?? "";
            }

            if (!active)
                _painting = false;
        }

        public void SelectPloppable(int typeId, string label)
        {
            SelectedTypeId = typeId;
            SelectedLabel = label ?? "";
            BuildModeActive = typeId > 0;
        }

        void Update()
        {
            if (_camera == null || _grid == null || _sim == null)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.B))
            {
                if (BuildModeActive)
                    SetBuildMode(false);
                else if (SelectedTypeId <= 0 && ModernBuildCatalog.ServiceEntries.Length > 0)
                {
                    var first = ModernBuildCatalog.ServiceEntries[0];
                    SetBuildMode(true, first.TypeId, first.Label);
                }
                else
                    SetBuildMode(!BuildModeActive);
            }

            if (!BuildModeActive || SelectedTypeId <= 0)
            {
                _painting = false;
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _painting = true;
            if (UnityEngine.Input.GetMouseButtonUp(0))
                _painting = false;

            if (_painting || UnityEngine.Input.GetMouseButtonDown(0))
                PlopAtMouse();
        }

        void PlopAtMouse()
        {
            var ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5000f))
                return;

            var tile = _grid.WorldToTile(hit.point);
            _sim.PlaceBuilding(tile.x, tile.y, SelectedTypeId);
        }
    }
}
