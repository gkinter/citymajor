using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>
    /// Bulldoze zones via SimHost.Bulldoze. Toggle with X. Defers zone/road/build tools while active.
    /// </summary>
    public sealed class BulldozeTool : MonoBehaviour
    {
        [SerializeField] int brushRadius = 2;

        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;

        public bool BulldozeModeActive { get; private set; }

        public void Configure(Camera cityCamera, ZoneGrid grid, CitySimBridge sim)
        {
            _camera = cityCamera;
            _grid = grid;
            _sim = sim;
        }

        public void SetBulldozeMode(bool active)
        {
            BulldozeModeActive = active;
            if (!active)
                _painting = false;
        }

        void Update()
        {
            if (_camera == null || _grid == null)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.X))
            {
                SetBulldozeMode(!BulldozeModeActive);
                if (BulldozeModeActive)
                    DeferSiblingTools();
            }

            if (!BulldozeModeActive)
            {
                _painting = false;
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _painting = true;
            if (UnityEngine.Input.GetMouseButtonUp(0))
                _painting = false;

            if (_painting || UnityEngine.Input.GetMouseButtonDown(0))
                BulldozeAtMouse();
        }

        void DeferSiblingTools()
        {
            var road = GetComponent<RoadPaintTool>();
            if (road != null)
                road.SetRoadMode(false);

            var build = GetComponent<BuildPlopTool>();
            build?.SetBuildMode(false);
        }

        void BulldozeAtMouse()
        {
            var ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5000f))
                return;

            var center = _grid.WorldToTile(hit.point);
            for (var dy = -brushRadius; dy <= brushRadius; dy++)
            for (var dx = -brushRadius; dx <= brushRadius; dx++)
            {
                if (dx * dx + dy * dy > brushRadius * brushRadius)
                    continue;

                var x = center.x + dx;
                var y = center.y + dy;
                _grid.SetZone(x, y, ZonePaintTool.ZoneKind.None);
                _sim?.Bulldoze(x, y);
            }
        }
    }
}
