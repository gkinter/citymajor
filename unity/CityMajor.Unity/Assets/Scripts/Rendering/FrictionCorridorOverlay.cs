using CityMajor.Sim;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Cathedral U3.3 / P3.4 — goods-transport friction corridors from
    /// <see cref="CitySimBridge.LatestFrictionCorridors"/> (market-zone boundaries).
    /// Cool cyan → amber → magenta by friction heat. Toggle with <see cref="KeyCode.F"/>.
    /// </summary>
    public sealed class FrictionCorridorOverlay : MonoBehaviour
    {
        const float HeatFloor = 0.05f;

        [SerializeField] bool visible;
        [SerializeField] float heatThreshold = HeatFloor;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Material _mat;
        FrictionCorridorDto[] _corridors = System.Array.Empty<FrictionCorridorDto>();

        public bool IsVisible => visible;

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                RefreshCorridors();
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;
            if (_mat != null)
                Destroy(_mat);
        }

        void OnStateChanged(CitySimState _) => RefreshCorridors();

        void RefreshCorridors()
        {
            _corridors = _sim?.LatestFrictionCorridors ?? System.Array.Empty<FrictionCorridorDto>();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                visible = !visible;
        }

        void OnRenderObject()
        {
            if (!visible || _grid == null || _mat == null || Camera.current == null)
                return;
            if (_corridors == null || _corridors.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var half = _grid.TileSize * 0.45f;
            var threshold = Mathf.Max(HeatFloor, heatThreshold);

            for (var i = 0; i < _corridors.Length; i++)
            {
                var tile = _corridors[i];
                if (tile == null)
                    continue;

                var heat = Mathf.Clamp01(tile.Friction);
                if (heat < threshold)
                    continue;

                var c = _grid.TileCenter(tile.TileX, tile.TileZ);
                c.y = 0.16f;

                GL.Color(FrictionColor(heat));
                GL.Vertex3(c.x - half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z + half);
                GL.Vertex3(c.x - half, c.y, c.z + half);
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>Cool cyan (local) → amber → magenta (high inter-zone friction).</summary>
        internal static Color FrictionColor(float friction)
        {
            var t = Mathf.Clamp01(friction);
            Color tint;
            if (t < 0.5f)
                tint = Color.Lerp(
                    new Color(0.15f, 0.75f, 0.85f),
                    new Color(0.95f, 0.72f, 0.18f),
                    t * 2f);
            else
                tint = Color.Lerp(
                    new Color(0.95f, 0.72f, 0.18f),
                    new Color(0.92f, 0.25f, 0.78f),
                    (t - 0.5f) * 2f);
            tint.a = 0.28f + t * 0.42f;
            return tint;
        }
    }
}
