using CityMajor.Sim;
using Forge.Engine.Simulation;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Congestion overlay from CitySimBridge <see cref="CitySimBridge.LatestRoadGraph"/>
    /// (edge volumes preferred, <c>RoadEdgeTravelTimes</c> fallback). GL lines between
    /// graph nodes; falls back to tile traffic tint when the graph export is empty.
    /// Toggle with <see cref="KeyCode.T"/>.
    /// </summary>
    public sealed class EdgeTrafficOverlay : MonoBehaviour
    {
        const float VolumeFloor = 1e-6f;

        [SerializeField] bool visible;
        [SerializeField] float densityThreshold = 0.08f;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Material _mat;
        RoadGraphSnapshotDto _graph = new();
        SimSnapshot _snap;

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;
            var shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            _sim.OnStateChanged += OnStateChanged;
            _sim.OnSnapshotChanged += OnSnapshotChanged;
            if (_sim.LatestSnapshot != null)
                _snap = _sim.LatestSnapshot;
            RefreshGraph();
        }

        void OnDestroy()
        {
            if (_sim == null)
                return;
            _sim.OnStateChanged -= OnStateChanged;
            _sim.OnSnapshotChanged -= OnSnapshotChanged;
        }

        void OnStateChanged(CitySimState _) => RefreshGraph();

        void OnSnapshotChanged(SimSnapshot snap) => _snap = snap;

        void RefreshGraph()
        {
            _graph = _sim?.LatestRoadGraph ?? new RoadGraphSnapshotDto();
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.T))
                visible = !visible;
        }

        void OnRenderObject()
        {
            if (!visible || _grid == null || _mat == null || Camera.current == null)
                return;

            if (TryRenderEdgeLines())
                return;

            RenderTileTrafficFallback();
        }

        bool TryRenderEdgeLines()
        {
            var graph = _graph;
            if (graph == null || graph.EdgeCount <= 0 || graph.NodeCount <= 0)
                return false;

            var edgeFrom = graph.EdgeFrom;
            var edgeTo = graph.EdgeTo;
            var volumes = graph.EdgeVolumes;
            var travelTimes = graph.TravelTimes;
            var xs = graph.NodeTileX;
            var zs = graph.NodeTileZ;
            if (edgeFrom == null || edgeTo == null || xs == null || zs == null)
                return false;
            if (edgeFrom.Length == 0 || edgeTo.Length == 0)
                return false;

            var nEdges = Mathf.Min(graph.EdgeCount, edgeFrom.Length, edgeTo.Length);
            var hasVolumes = volumes is { Length: > 0 };
            var hasTimes = travelTimes is { Length: > 0 };
            if (!hasVolumes && !hasTimes)
                return false;

            var maxVolume = ArrayMax(volumes, nEdges);
            var maxTravelTime = ArrayMax(travelTimes, nEdges);
            var minTravelTime = ArrayMinPositive(travelTimes, nEdges);

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            var anyDrawn = false;
            for (var e = 0; e < nEdges; e++)
            {
                var from = edgeFrom[e];
                var to = edgeTo[e];
                if (from < 0 || to < 0 || from >= graph.NodeCount || to >= graph.NodeCount)
                    continue;
                if (from >= xs.Length || to >= xs.Length || from >= zs.Length || to >= zs.Length)
                    continue;

                var density = EdgeCongestionDensity(
                    e, volumes, travelTimes, maxVolume, minTravelTime, maxTravelTime);
                if (density < densityThreshold)
                    continue;

                var c0 = _grid.TileCenter(xs[from], zs[from]);
                var c1 = _grid.TileCenter(xs[to], zs[to]);
                c0.y = 0.22f;
                c1.y = 0.22f;

                GL.Color(CongestionColor(density));
                GL.Vertex3(c0.x, c0.y, c0.z);
                GL.Vertex3(c1.x, c1.y, c1.z);
                anyDrawn = true;
            }

            GL.End();
            GL.PopMatrix();
            return anyDrawn;
        }

        void RenderTileTrafficFallback()
        {
            if (_snap == null)
                return;

            var roads = _snap.TileRoadFlags;
            var traffic = _snap.TileTraffic;
            if (roads == null || traffic == null || roads.Length == 0)
                return;

            _mat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.QUADS);

            var size = Mathf.Min(_grid.MapSize, _snap.WorldSize > 0 ? _snap.WorldSize : _grid.MapSize);
            var step = Mathf.Max(1, size / 96);
            var half = _grid.TileSize * step * 0.45f;

            for (var y = 0; y < size; y += step)
            for (var x = 0; x < size; x += step)
            {
                var idx = y * size + x;
                if (idx >= roads.Length || roads[idx] == 0)
                    continue;

                var density = idx < traffic.Length ? traffic[idx] : 0f;
                if (density < densityThreshold)
                    continue;

                var c = _grid.TileCenter(x, y);
                c.y = 0.18f;
                GL.Color(CongestionColor(density));
                GL.Vertex3(c.x - half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z - half);
                GL.Vertex3(c.x + half, c.y, c.z + half);
                GL.Vertex3(c.x - half, c.y, c.z + half);
            }

            GL.End();
            GL.PopMatrix();
        }

        static float EdgeCongestionDensity(
            int edgeIndex,
            float[] volumes,
            float[] travelTimes,
            float maxVolume,
            float minTravelTime,
            float maxTravelTime)
        {
            if (volumes != null && edgeIndex < volumes.Length && maxVolume > VolumeFloor)
                return Mathf.Clamp01(volumes[edgeIndex] / maxVolume);

            if (travelTimes != null
                && edgeIndex < travelTimes.Length
                && maxTravelTime > minTravelTime + VolumeFloor)
            {
                var tt = travelTimes[edgeIndex];
                if (!float.IsNaN(tt) && !float.IsInfinity(tt))
                    return Mathf.Clamp01((tt - minTravelTime) / (maxTravelTime - minTravelTime));
            }

            return 0f;
        }

        static Color CongestionColor(float density)
        {
            var t = Mathf.Clamp01(density);
            // Green → amber → red
            Color tint;
            if (t < 0.5f)
                tint = Color.Lerp(new Color(0.2f, 0.85f, 0.35f), new Color(0.95f, 0.75f, 0.15f), t * 2f);
            else
                tint = Color.Lerp(new Color(0.95f, 0.75f, 0.15f), new Color(0.95f, 0.2f, 0.15f), (t - 0.5f) * 2f);
            tint.a = 0.35f + t * 0.55f;
            return tint;
        }

        static float ArrayMax(float[] values, int count)
        {
            if (values == null || values.Length == 0 || count <= 0)
                return 0f;
            var max = 0f;
            var n = Mathf.Min(count, values.Length);
            for (var i = 0; i < n; i++)
            {
                if (values[i] > max)
                    max = values[i];
            }
            return max;
        }

        static float ArrayMinPositive(float[] values, int count)
        {
            if (values == null || values.Length == 0 || count <= 0)
                return 0f;
            var min = float.PositiveInfinity;
            var n = Mathf.Min(count, values.Length);
            for (var i = 0; i < n; i++)
            {
                var v = values[i];
                if (!float.IsNaN(v) && !float.IsInfinity(v) && v < min)
                    min = v;
            }
            return !float.IsInfinity(min) ? min : 0f;
        }
    }
}
