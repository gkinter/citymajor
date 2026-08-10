using System.Collections.Generic;
using CityMajor.Core;
using CityMajor.Input;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// GPU-instanced buildings from SimSnapshot (Forge.SimCore). Plain C# sim → DrawMeshInstanced.
    /// </summary>
    public sealed class BuildingInstancer : MonoBehaviour
    {
        [SerializeField] int maxInstances = 5000;
        [SerializeField] float buildingHeight = 4f;
        [SerializeField] float boxLodDistance = 200f;

        ZoneGrid _grid;
        CitySimBridge _sim;
        ZoneGrowthVisualizer _growth;
        IsometricCameraController _camera;
        Mesh _fallbackMesh;
        Material _mat;
        Material _matConstructing;
        SimSnapshot _snap;

        const byte StateConstructing = 0;
        const byte StateOperational = 1;

        readonly Dictionary<string, InstanceGroup> _groups = new();

        struct InstanceGroup
        {
            public Mesh Mesh;
            public Material Material;
            public bool IsFallback;
            public Matrix4x4[] Matrices;
            public int Count;
        }

        public void Configure(ZoneGrid grid, CitySimBridge sim, IsometricCameraController camera = null)
        {
            _grid = grid;
            _sim = sim;
            _camera = camera;
            _growth = GetComponent<ZoneGrowthVisualizer>();

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _fallbackMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);

            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _matConstructing = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.92f, 0.68f, 0.22f, 1f),
            };
            GltfMeshCache.Preload(GltfCatalog.ShippedKeys);

            _sim.OnSnapshotChanged += OnSnapshot;
            _sim.OnStateChanged += OnSimState;
            if (_sim.LatestSnapshot != null)
                RebuildFromSnapshot(_sim.LatestSnapshot);
            else
                RebuildFromZoneFallback();
        }

        void OnDestroy()
        {
            if (_sim == null)
                return;
            _sim.OnSnapshotChanged -= OnSnapshot;
            _sim.OnStateChanged -= OnSimState;
        }

        void LateUpdate()
        {
            if (_mat == null)
                return;

            var useBoxLod = _camera != null && _camera.Distance >= boxLodDistance;

            foreach (var pair in _groups)
            {
                var group = pair.Value;
                if (group.Count <= 0 || group.Mesh == null)
                    continue;

                var mesh = useBoxLod ? _fallbackMesh : group.Mesh;
                Graphics.DrawMeshInstanced(mesh, 0, group.Material, group.Matrices, group.Count);
            }
        }

        void OnSnapshot(SimSnapshot snap)
        {
            _snap = snap;
            RebuildFromSnapshot(snap);
        }

        void OnSimState(CitySimState _) { }

        void RebuildFromSnapshot(SimSnapshot snap)
        {
            _groups.Clear();
            if (snap?.Buildings == null || _grid == null)
            {
                RebuildFromZoneFallback();
                return;
            }

            var grouped = new Dictionary<string, List<Matrix4x4>>();
            var total = 0;

            foreach (var b in snap.Buildings)
            {
                if (b.TypeId == 0 || (b.State != StateConstructing && b.State != StateOperational))
                    continue;

                var catalogKey = BuildingArchetypes.CatalogKeyForTypeId(b.TypeId);
                if (string.IsNullOrEmpty(catalogKey))
                    continue;

                var groupKey = b.State == StateConstructing ? catalogKey + "::constructing" : catalogKey;
                var matrix = ComposeBuildingMatrix(b.GridX, b.GridY, b.TypeId, b.Level, b.State, b.Condition);
                if (!grouped.TryGetValue(groupKey, out var list))
                {
                    list = new List<Matrix4x4>();
                    grouped[groupKey] = list;
                }
                list.Add(matrix);
                total++;
                if (total >= maxInstances)
                    break;
            }

            if (total == 0)
            {
                RebuildFromZoneFallback();
                return;
            }

            FinalizeGroups(grouped);
        }

        void RebuildFromZoneFallback()
        {
            if (!_sim.UsesForgeSimCore)
                RebuildProceduralZones();
        }

        void RebuildProceduralZones()
        {
            _groups.Clear();
            if (_grid == null)
                return;

            var rng = new System.Random(42);
            var grouped = new Dictionary<string, List<Matrix4x4>>();

            for (var y = 0; y < _grid.MapSize; y++)
            for (var x = 0; x < _grid.MapSize; x++)
            {
                var zone = _grid.GetZone(x, y);
                if (zone == ZonePaintTool.ZoneKind.None)
                    continue;
                if (rng.NextDouble() > 0.12)
                    continue;

                var typeId = BuildingArchetypes.DefaultTypeIdForZone(zone) + rng.Next(0, 8);
                var catalogKey = BuildingArchetypes.CatalogKeyForTypeId(typeId)
                    ?? BuildingArchetypes.DefaultCatalogKeyForZone(zone);
                if (string.IsNullOrEmpty(catalogKey))
                    continue;

                var matrix = ComposeBuildingMatrix(x, y, typeId, 1, 1, 255);
                if (!grouped.TryGetValue(catalogKey, out var list))
                {
                    list = new List<Matrix4x4>();
                    grouped[catalogKey] = list;
                }
                list.Add(matrix);
                if (TotalCount(grouped) >= maxInstances)
                    goto done;
            }

            done:
            FinalizeGroups(grouped);
        }

        void FinalizeGroups(Dictionary<string, List<Matrix4x4>> grouped)
        {
            foreach (var pair in grouped)
            {
                var groupKey = pair.Key;
                var matrices = pair.Value;
                var count = Mathf.Min(matrices.Count, maxInstances);
                var buffer = new Matrix4x4[count];
                for (var i = 0; i < count; i++)
                    buffer[i] = matrices[i];

                var constructing = groupKey.EndsWith("::constructing");
                var catalogKey = constructing ? groupKey[..^"::constructing".Length] : groupKey;
                var mesh = GltfMeshCache.GetOrLoad(catalogKey);
                var isFallback = mesh == null;
                if (isFallback)
                    mesh = _fallbackMesh;

                _groups[groupKey] = new InstanceGroup
                {
                    Mesh = mesh,
                    Material = constructing ? _matConstructing : _mat,
                    IsFallback = isFallback,
                    Matrices = buffer,
                    Count = count,
                };
            }
        }

        Matrix4x4 ComposeBuildingMatrix(int tileX, int tileY, int typeId, int level, byte state, byte condition)
        {
            var h = buildingHeight * (0.5f + level * 0.25f);
            var growth = _growth != null
                ? _growth.ScaleFor(tileX, tileY, state, condition)
                : (condition < 200 ? 0.35f : 1f);
            h *= growth;
            var center = _grid.TileCenter(tileX, tileY);
            center.y = h * 0.5f;
            var scale = new Vector3(_grid.TileSize * 0.55f, h, _grid.TileSize * 0.55f);
            var yaw = TileYawDegrees(tileX, tileY, typeId);
            return Matrix4x4.TRS(center, Quaternion.Euler(0f, yaw, 0f), scale);
        }

        static float TileYawDegrees(int x, int y, int typeId)
        {
            var h = (x * 73856093) ^ (y * 19349663) ^ (typeId * 83492791);
            return (h & 3) * 90f;
        }

        static int TotalCount(Dictionary<string, List<Matrix4x4>> grouped)
        {
            var total = 0;
            foreach (var pair in grouped)
                total += pair.Value.Count;
            return total;
        }
    }
}
