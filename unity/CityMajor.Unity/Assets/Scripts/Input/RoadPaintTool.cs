using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>
    /// Paint roads into Forge.SimCore via CitySimBridge. Key 4 toggles road brush.
    /// Inspector: <see cref="paintBridge"/> / <see cref="paintTunnel"/> map to Cathedral P1.3 RoadFlags.
    /// </summary>
    public sealed class RoadPaintTool : MonoBehaviour
    {
        [SerializeField] int brushRadius = 1;
        [SerializeField] byte roadTier = 1;
        [SerializeField] bool paintBridge;
        [SerializeField] bool paintTunnel;
        [SerializeField] bool roadMode;

        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;

        public bool RoadModeActive => roadMode;
        public bool PaintBridge => paintBridge;
        public bool PaintTunnel => paintTunnel;

        public void SetRoadMode(bool active) => roadMode = active;

        public void SetBridgeTunnel(bool bridge, bool tunnel)
        {
            paintBridge = bridge;
            paintTunnel = tunnel;
        }

        public void Configure(Camera cityCamera, ZoneGrid grid, CitySimBridge sim)
        {
            _camera = cityCamera;
            _grid = grid;
            _sim = sim;
        }

        void Update()
        {
            if (_camera == null || _grid == null || _sim == null)
                return;

            var bulldoze = GetComponent<BulldozeTool>();
            if (bulldoze != null && bulldoze.BulldozeModeActive)
            {
                roadMode = false;
                _painting = false;
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4))
                roadMode = !roadMode;

            var buildTool = GetComponent<BuildPlopTool>();
            if (buildTool != null && buildTool.BuildModeActive)
            {
                roadMode = false;
                _painting = false;
                return;
            }

            if (!roadMode)
            {
                _painting = false;
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _painting = true;
            if (UnityEngine.Input.GetMouseButtonUp(0))
                _painting = false;

            if (_painting || UnityEngine.Input.GetMouseButtonDown(0))
                PaintAtMouse();
        }

        void PaintAtMouse()
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
                _sim.PlaceRoad(center.x + dx, center.y + dy, roadTier, paintBridge, paintTunnel);
            }
        }
    }
}
