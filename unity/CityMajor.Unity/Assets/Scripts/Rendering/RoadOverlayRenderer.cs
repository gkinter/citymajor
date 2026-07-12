using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Road + traffic tint from SimSnapshot (Forge.SimCore). GL overlay — no per-tile GameObjects.
    /// </summary>
    public sealed class RoadOverlayRenderer : MonoBehaviour
    {
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
            _sim.OnSnapshotChanged += OnSnapshot;
            if (_sim.LatestSnapshot != null)
                _snap = _sim.LatestSnapshot;
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshot;
        }

        void OnSnapshot(SimSnapshot snap) => _snap = snap;

        void OnRenderObject()
        {
            if (_grid == null || _mat == null || Camera.current == null || _snap == null)
                return;

            var roads = _snap.TileRoadFlags;
            var traffic = _snap.TileTraffic;
            if (roads == null || roads.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var step = Mathf.Max(1, _grid.MapSize / 128);
            var size = _grid.MapSize;
            for (var y = 0; y < size; y += step)
            for (var x = 0; x < size; x += step)
            {
                var idx = y * size + x;
                if (idx >= roads.Length || roads[idx] == 0)
                    continue;

                var t = traffic != null && idx < traffic.Length ? traffic[idx] : 0f;
                var c = Color.Lerp(new Color(0.35f, 0.35f, 0.38f, 0.7f),
                    new Color(0.95f, 0.45f, 0.15f, 0.75f), Mathf.Clamp01(t));
                GL.Color(c);

                var s = _grid.TileSize * step;
                var ox = x * _grid.TileSize;
                var oz = y * _grid.TileSize;
                var y0 = 0.1f;
                GL.Vertex3(ox, y0, oz);
                GL.Vertex3(ox + s, y0, oz);
                GL.Vertex3(ox + s, y0, oz + s);
                GL.Vertex3(ox, y0, oz + s);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
