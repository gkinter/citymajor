using CityMajor.Sim;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// GL overlay for power/water stress (toggle U). Stepped samples from SimHost.
    /// </summary>
    public sealed class UtilityStressOverlay : MonoBehaviour
    {
        [SerializeField] bool visible;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Material _mat;
        UtilityCoverageDto[] _samples = System.Array.Empty<UtilityCoverageDto>();

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
            if (UnityEngine.Input.GetKeyDown(KeyCode.U))
                visible = !visible;
        }

        void RefreshSamples()
        {
            _samples = _sim?.LatestUtilityCoverage ?? System.Array.Empty<UtilityCoverageDto>();
        }

        void OnRenderObject()
        {
            if (!visible || _grid == null || _mat == null || Camera.current == null || _samples.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var stressIndex = _sim?.State.UtilityStressIndex ?? 0f;
            var half = _grid.TileSize * 0.45f;
            foreach (var s in _samples)
            {
                var powerOk = s.Power >= 0.5f;
                var waterOk = s.Water >= 0.5f;
                var localDeficit = 1f - Mathf.Min(s.Power, s.Water);
                var alphaScale = Mathf.Max(stressIndex, localDeficit);
                if (powerOk && waterOk)
                    alphaScale = Mathf.Max(alphaScale, 0.15f);
                else if (alphaScale < 0.05f)
                    continue;

                Color tint;
                if (powerOk && waterOk)
                    tint = new Color(0.25f, 0.9f, 0.45f, 0.28f * alphaScale);
                else if (!powerOk && !waterOk)
                    tint = new Color(0.95f, 0.25f, 0.2f, 0.32f * alphaScale);
                else
                    tint = new Color(0.95f, 0.65f, 0.15f, 0.3f * alphaScale);

                var c = _grid.TileCenter(s.TileX, s.TileZ);
                c.y = 0.14f;

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
