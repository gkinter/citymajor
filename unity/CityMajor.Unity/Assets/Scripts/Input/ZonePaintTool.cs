using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>
    /// Paint zones into Forge.SimCore via <see cref="CitySimBridge.PaintZone"/>.
    /// Density 1–3 = Low / Med / High (Cathedral P2.2) with U3.4 era/tech gates.
    /// Office / Mixed / Ag / Park use P2.1 era (+ tech) gates.
    /// </summary>
    public sealed class ZonePaintTool : MonoBehaviour
    {
        public enum ZoneKind
        {
            None,
            Residential,
            Commercial,
            Industrial,
            Office,
            Mixed,
            Agricultural,
            Park,
        }

        [SerializeField] ZoneKind activeZone = ZoneKind.Residential;
        [SerializeField] int brushRadius = 2;
        /// <summary>1–3 = low / medium / high; 0 treated as low when painting.</summary>
        [SerializeField] byte zoneDensity = 1;

        Camera _camera;
        ZoneGrid _grid;
        CitySimBridge _sim;
        bool _painting;

        public ZoneKind ActiveZone => activeZone;
        public byte ZoneDensity => zoneDensity;

        public string ActiveZoneLabel()
        {
            var densityLabel = DensityLabel(zoneDensity);
            return activeZone switch
            {
                ZoneKind.Residential => $"Zone R · {densityLabel} (1)",
                ZoneKind.Commercial => $"Zone C · {densityLabel} (2)",
                ZoneKind.Industrial => $"Zone I · {densityLabel} (3)",
                ZoneKind.Office => $"Zone Office · {densityLabel} (5)",
                ZoneKind.Mixed => $"Zone Mixed · {densityLabel} (6)",
                ZoneKind.Agricultural => $"Zone Ag · {densityLabel} (7)",
                ZoneKind.Park => $"Zone Park · {densityLabel} (8)",
                _ => "Erase zone (0)",
            };
        }

        public static string DensityLabel(byte density) => NormalizeDensity(density) switch
        {
            2 => "Med",
            3 => "High",
            _ => "Low",
        };

        public static byte NormalizeDensity(byte density) =>
            density is >= 1 and <= 3 ? density : (byte)1;

        public void Configure(Camera cityCamera, ZoneGrid grid, CitySimBridge sim)
        {
            _camera = cityCamera;
            _grid = grid;
            _sim = sim;
            zoneDensity = ZoneDensityTiers.ClampToUnlocked(zoneDensity, _sim);
        }

        void Update()
        {
            if (_camera == null || _grid == null)
                return;

            var bulldoze = GetComponent<BulldozeTool>();
            if (bulldoze != null && bulldoze.BulldozeModeActive)
            {
                _painting = false;
                return;
            }

            var roadTool = GetComponent<RoadPaintTool>();
            if (roadTool != null && roadTool.RoadModeActive)
            {
                _painting = false;
                return;
            }

            var buildTool = GetComponent<BuildPlopTool>();
            if (buildTool != null && buildTool.BuildModeActive)
            {
                _painting = false;
                return;
            }

            // Re-clamp density if era/research changes mid-session.
            zoneDensity = ZoneDensityTiers.ClampToUnlocked(zoneDensity, _sim);

            HandleHotkeys();

            if (UnityEngine.Input.GetMouseButtonDown(0))
                _painting = true;
            if (UnityEngine.Input.GetMouseButtonUp(0))
                _painting = false;

            if (_painting || UnityEngine.Input.GetMouseButtonDown(0))
                PaintAtMouse();
        }

        void HandleHotkeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                TrySetActiveZone(ZoneKind.Residential);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                TrySetActiveZone(ZoneKind.Commercial);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                TrySetActiveZone(ZoneKind.Industrial);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5))
                TrySetActiveZone(ZoneKind.Office);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6))
                TrySetActiveZone(ZoneKind.Mixed);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha7))
                TrySetActiveZone(ZoneKind.Agricultural);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha8))
                TrySetActiveZone(ZoneKind.Park);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0))
                activeZone = ZoneKind.None;

            if (UnityEngine.Input.GetKeyDown(KeyCode.D))
                CycleDensity();
        }

        void PaintAtMouse()
        {
            if (activeZone != ZoneKind.None && !IsZoneUnlocked(activeZone))
                return;

            var ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5000f))
                return;

            var density = activeZone == ZoneKind.None
                ? (byte)0
                : ZoneDensityTiers.ClampToUnlocked(NormalizeDensity(zoneDensity), _sim);
            var center = _grid.WorldToTile(hit.point);
            for (var dy = -brushRadius; dy <= brushRadius; dy++)
            for (var dx = -brushRadius; dx <= brushRadius; dx++)
            {
                if (dx * dx + dy * dy > brushRadius * brushRadius)
                    continue;
                var x = center.x + dx;
                var y = center.y + dy;
                _grid.SetZone(x, y, activeZone);
                _sim?.PaintZone(x, y, activeZone, density);
            }

            if (_sim != null && !_sim.UsesForgeSimCore)
                _sim.NotifyZonesChanged();
        }

        public void SetActiveZone(ZoneKind zone) => TrySetActiveZone(zone);

        public bool TrySetActiveZone(ZoneKind zone)
        {
            if (zone != ZoneKind.None && !IsZoneUnlocked(zone))
                return false;

            activeZone = zone;
            return true;
        }

        public void SetZoneDensity(byte density) => TrySetZoneDensity(density);

        public bool TrySetZoneDensity(byte density)
        {
            var normalized = NormalizeDensity(density);
            if (!ZoneDensityTiers.IsUnlocked(normalized, _sim))
                return false;

            zoneDensity = normalized;
            return true;
        }

        public void CycleDensity()
        {
            zoneDensity = ZoneDensityTiers.NextUnlocked(zoneDensity, _sim);
        }

        public bool IsZoneUnlocked(ZoneKind zone)
        {
            if (zone == ZoneKind.None)
                return true;

            if (!ZoneTiers.TryGet(zone, out var tier))
                return false;

            return ZoneTiers.IsUnlocked(tier, _sim);
        }

        public bool IsDensityUnlocked(byte density) =>
            ZoneDensityTiers.IsUnlocked(density, _sim);

        public string ZoneLockReason(ZoneKind zone)
        {
            if (!ZoneTiers.TryGet(zone, out var tier))
                return "Unknown zone";
            return ZoneTiers.LockReason(tier, _sim);
        }

        public string DensityLockReason(byte density) =>
            ZoneDensityTiers.LockReason(density, _sim);
    }
}
