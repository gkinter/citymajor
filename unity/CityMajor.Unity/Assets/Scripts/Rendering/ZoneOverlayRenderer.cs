using CityMajor.Input;
using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Draws zone tint quads on the terrain. Phase 1 uses GL quads; mesh overlay later.
    /// </summary>
    public sealed class ZoneOverlayRenderer : MonoBehaviour
    {
        ZoneGrid _grid;
        Material _mat;

        public void Configure(ZoneGrid grid)
        {
            _grid = grid;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnRenderObject()
        {
            if (_grid == null || _mat == null || Camera.current == null)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var step = Mathf.Max(1, _grid.MapSize / 64);
            for (var y = 0; y < _grid.MapSize; y += step)
            for (var x = 0; x < _grid.MapSize; x += step)
            {
                var kind = _grid.GetZone(x, y);
                if (kind == ZonePaintTool.ZoneKind.None)
                    continue;
                var c = _grid.ZoneColor(kind);
                GL.Color(c);
                var s = _grid.TileSize * step;
                var ox = x * _grid.TileSize;
                var oz = y * _grid.TileSize;
                var y0 = 0.08f;
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
