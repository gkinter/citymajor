using CityMajor.Sim;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// GL overlay for police / health / fire / education coverage (toggle V). Stepped samples from SimHost.
    /// </summary>
    public sealed class ServiceCoverageOverlay : MonoBehaviour
    {
        [SerializeField] bool visible;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Material _mat;
        ServiceCoverageDto[] _samples = System.Array.Empty<ServiceCoverageDto>();

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            _sim.OnStateChanged += _ => RefreshSamples();
            RefreshSamples();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.V))
                visible = !visible;
        }

        void RefreshSamples()
        {
            _samples = _sim?.LatestServiceCoverage ?? System.Array.Empty<ServiceCoverageDto>();
        }

        void OnRenderObject()
        {
            if (!visible || _grid == null || _mat == null || Camera.current == null || _samples.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var half = _grid.TileSize * 0.45f;
            foreach (var s in _samples)
            {
                var c = _grid.TileCenter(s.TileX, s.TileZ);
                c.y = 0.12f;
                var strength = Mathf.Max(s.Police, s.Health, s.Fire, s.Education);
                if (strength < 0.08f)
                    continue;

                Color tint;
                if (s.Police >= s.Health && s.Police >= s.Fire && s.Police >= s.Education)
                    tint = new Color(0.25f, 0.45f, 0.95f, 0.22f * strength);
                else if (s.Health >= s.Fire && s.Health >= s.Education)
                    tint = new Color(0.25f, 0.9f, 0.45f, 0.22f * strength);
                else if (s.Fire >= s.Education)
                    tint = new Color(0.95f, 0.35f, 0.2f, 0.22f * strength);
                else
                    tint = new Color(0.85f, 0.75f, 0.2f, 0.22f * strength);

                GL.Color(tint);
                GL.Vertex3(c.x - half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z + half);
                GL.Vertex3(c.x - half, c.y, c.z + half);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
