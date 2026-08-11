using CityMajor.Sim;
using CityMajor.UI;
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
        public const string IllegalHighwayMergeToast = "Illegal highway merge — use a ramp";
        public const string InvalidRampToast = "Invalid ramp — connect to exactly one highway";

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
        [SerializeField] float rejectToastCooldown = 1.25f;

        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;
        float _nextRejectToastAt = -1f;

        public bool RoadModeActive => roadMode;
        public byte RoadTier => roadTier;
        public bool PaintBridge => Elevation == RoadElevation.Bridge;
        public bool PaintTunnel => Elevation == RoadElevation.Tunnel;
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

            // Bridge / tunnel / none are mutually exclusive.
            paintBridge = elevation == RoadElevation.Bridge;
            paintTunnel = elevation == RoadElevation.Tunnel;
        }

        public void SetBridgeTunnel(bool bridge, bool tunnel)
        {
            if (paintRamp)
                return;

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

        /// <summary>
        /// Normalize PlaceRoad bools so ramp never ships with elevation, and bridge XOR tunnel.
        /// Matches SimHost.ComputeRoadFlags mutual exclusivity (client-side guard).
        /// </summary>
        public static void NormalizePlaceFlags(
            bool ramp,
            bool bridge,
            bool tunnel,
            byte tier,
            out bool outRamp,
            out bool outBridge,
            out bool outTunnel,
            out byte outTier)
        {
            outTier = (byte)Mathf.Clamp(tier, 0, 2);
            if (ramp)
            {
                outRamp = true;
                outBridge = false;
                outTunnel = false;
                if (outTier >= 2)
                    outTier = 1;
                return;
            }

            outRamp = false;
            if (bridge && tunnel)
                tunnel = false;
            outBridge = bridge;
            outTunnel = tunnel;
        }

        public static string RejectToastForPaint(bool rampAttempt) =>
            rampAttempt ? InvalidRampToast : IllegalHighwayMergeToast;

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

        void OnValidate()
        {
            brushRadius = Mathf.Max(0, brushRadius);
            roadTier = (byte)Mathf.Clamp(roadTier, 0, 2);
            NormalizeInspectorFlags();
        }

        void NormalizeInspectorFlags()
        {
            if (paintRamp)
            {
                paintBridge = false;
                paintTunnel = false;
                if (roadTier >= 2)
                    roadTier = 1;
                return;
            }

            if (paintBridge && paintTunnel)
                paintTunnel = false;
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

            NormalizePlaceFlags(
                paintRamp,
                paintBridge,
                paintTunnel,
                roadTier,
                out var ramp,
                out var bridge,
                out var tunnel,
                out var tier);

            // Keep serialized fields coherent after clamp (e.g. highway+ramp → collector).
            if (ramp && roadTier != tier)
                roadTier = tier;
            if (ramp)
            {
                paintBridge = false;
                paintTunnel = false;
            }
            else if (bridge && paintTunnel)
            {
                paintTunnel = false;
            }

            var center = _grid.WorldToTile(hit.point);
            var anyReject = false;
            for (var dy = -brushRadius; dy <= brushRadius; dy++)
            for (var dx = -brushRadius; dx <= brushRadius; dx++)
            {
                if (dx * dx + dy * dy > brushRadius * brushRadius)
                    continue;
                if (!_sim.PlaceRoad(
                        center.x + dx,
                        center.y + dy,
                        tier,
                        bridge,
                        tunnel,
                        ramp))
                    anyReject = true;
            }

            if (anyReject)
                NotifyRejected(ramp);
        }

        void NotifyRejected(bool rampAttempt)
        {
            if (Time.unscaledTime < _nextRejectToastAt)
                return;

            _nextRejectToastAt = Time.unscaledTime + Mathf.Max(0.35f, rejectToastCooldown);
            StatusToastController.ShowGlobal(RejectToastForPaint(rampAttempt));
        }
    }
}
