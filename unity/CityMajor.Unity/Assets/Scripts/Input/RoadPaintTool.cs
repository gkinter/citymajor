using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>
    /// Paint roads into Forge.SimCore via CitySimBridge. Key 4 toggles road brush.
    /// Toolbar / API mirrors web RoadTypeToolbar: Local / Collector / Highway tiers,
    /// Bridge / Tunnel elevation, and Ramp connectors — all map to PlaceRoad(tier, bridge, tunnel, ramp).
    /// </summary>
    public sealed class RoadPaintTool : MonoBehaviour
    {
        public enum RoadElevation : byte
        {
            None = 0,
            Bridge = 1,
            Tunnel = 2,
        }

        /// <summary>Toolbar selection — tier paint or dedicated ramp connector (web RoadToolId).</summary>
        public enum RoadTool : byte
        {
            Local = 0,
            Collector = 1,
            Highway = 2,
            Ramp = 3,
        }

        [SerializeField] int brushRadius = 1;
        [SerializeField] byte roadTier = 0;
        [SerializeField] bool paintBridge;
        [SerializeField] bool paintTunnel;
        [SerializeField] bool paintRamp;
        [SerializeField] bool roadMode;

        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;

        public bool RoadModeActive => roadMode;
        public byte RoadTier => roadTier;
        public bool PaintBridge => paintBridge;
        public bool PaintTunnel => paintTunnel;
        public bool PaintRamp => paintRamp;

        public RoadElevation Elevation =>
            paintRamp ? RoadElevation.None
            : paintBridge ? RoadElevation.Bridge
            : paintTunnel ? RoadElevation.Tunnel
            : RoadElevation.None;

        public RoadTool ActiveTool =>
            paintRamp ? RoadTool.Ramp : (RoadTool)Mathf.Clamp(roadTier, 0, 2);

        public string ActiveToolLabel
        {
            get
            {
                if (paintRamp)
                    return "Ramp";
                var tierLabel = TierLabel(roadTier);
                return Elevation switch
                {
                    RoadElevation.Bridge => $"{tierLabel} bridge",
                    RoadElevation.Tunnel => $"{tierLabel} tunnel",
                    _ => tierLabel,
                };
            }
        }

        public void SetRoadMode(bool active) => roadMode = active;

        public void SetRoadTier(byte tier)
        {
            roadTier = (byte)Mathf.Clamp(tier, 0, 2);
            paintRamp = false;
        }

        /// <summary>Select Local / Collector / Highway paint (clears ramp; keeps elevation).</summary>
        public void SelectTierTool(byte tier)
        {
            SetRoadTier(tier);
            roadMode = true;
        }

        /// <summary>Cathedral P1.5 — dedicated ramp paint (clears bridge/tunnel elevation).</summary>
        public void SelectRampTool()
        {
            paintRamp = true;
            paintBridge = false;
            paintTunnel = false;
            // Ramp connectors prefer collector, else local — never highway (SimHost clamps).
            if (roadTier >= 2)
                roadTier = 1;
            roadMode = true;
        }

        public void SetElevation(RoadElevation elevation)
        {
            if (paintRamp)
                return;

            paintBridge = elevation == RoadElevation.Bridge;
            paintTunnel = elevation == RoadElevation.Tunnel;
        }

        public void SetBridgeTunnel(bool bridge, bool tunnel)
        {
            if (bridge && tunnel)
                tunnel = false;
            paintBridge = bridge;
            paintTunnel = tunnel;
            if (bridge || tunnel)
                paintRamp = false;
        }

        /// <summary>Cathedral P1.5 — dedicated ramp paint (clears bridge/tunnel elevation).</summary>
        public void SetRamp(bool ramp)
        {
            paintRamp = ramp;
            if (ramp)
            {
                paintBridge = false;
                paintTunnel = false;
                if (roadTier >= 2)
                    roadTier = 1;
            }
        }

        public static string TierLabel(byte tier) => tier switch
        {
            0 => "Local",
            1 => "Collector",
            2 => "Highway",
            _ => "Road",
        };

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
                _sim.PlaceRoad(
                    center.x + dx,
                    center.y + dy,
                    roadTier,
                    paintBridge,
                    paintTunnel,
                    paintRamp);
            }
        }
    }
}
