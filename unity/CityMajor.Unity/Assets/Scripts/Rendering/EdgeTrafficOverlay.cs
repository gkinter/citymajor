using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Draws hot edge segments between adjacent high-traffic road tiles (TopEdges stub until sim exports edges).
    /// Toggle with <see cref="KeyCode.T"/>.
    /// </summary>
    public sealed class EdgeTrafficOverlay : MonoBehaviour
    {
        [SerializeField] bool visible;
        [SerializeField] float densityThreshold = 0.35f;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Material _mat;
        SimSnapshot _snap;

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _sim.OnSnapshotChanged += s => _snap = s;
            if (_sim.LatestSnapshot != null)
                _snap = _sim.LatestSnapshot;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.T))
                visible = !visible;
        }

        void OnRenderObject()
        {
            if (!visible || _grid == null || _mat == null || _snap == null || Camera.current == null)
                return;

            var roads = _snap.TileRoadFlags;
            var traffic = _snap.TileTraffic;
            if (roads == null || traffic == null || roads.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            var size = Mathf.Min(_grid.MapSize, _snap.WorldSize > 0 ? _snap.WorldSize : _grid.MapSize);
            var step = Mathf.Max(1, size / 96);

            for (var y = 0; y < size; y += step)
            for (var x = 0; x < size; x += step)
            {
                var idx = y * size + x;
                if (idx >= roads.Length || roads[idx] == 0)
                    continue;

                var t0 = idx < traffic.Length ? traffic[idx] : 0f;
                if (t0 < densityThreshold)
                    continue;

                var c0 = _grid.TileCenter(x, y);
                c0.y = 0.2f;

                if (x + step < size)
                {
                    var idxR = y * size + (x + step);
                    if (idxR < roads.Length && roads[idxR] != 0)
                    {
                        var t1 = traffic[idxR];
                        if (t1 >= densityThreshold * 0.8f)
                        {
                            var c1 = _grid.TileCenter(x + step, y);
                            c1.y = 0.2f;
                            var heat = (t0 + t1) * 0.5f;
                            GL.Color(new Color(1f, 0.35f, 0.1f, 0.35f + heat * 0.45f));
                            GL.Vertex3(c0.x, c0.y, c0.z);
                            GL.Vertex3(c1.x, c1.y, c1.z);
                        }
                    }
                }

                if (y + step < size)
                {
                    var idxD = (y + step) * size + x;
                    if (idxD < roads.Length && roads[idxD] != 0)
                    {
                        var t1 = traffic[idxD];
                        if (t1 >= densityThreshold * 0.8f)
                        {
                            var c1 = _grid.TileCenter(x, y + step);
                            c1.y = 0.2f;
                            var heat = (t0 + t1) * 0.5f;
                            GL.Color(new Color(1f, 0.55f, 0.15f, 0.35f + heat * 0.45f));
                            GL.Vertex3(c0.x, c0.y, c0.z);
                            GL.Vertex3(c1.x, c1.y, c1.z);
                        }
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
