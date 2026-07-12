using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Cosmetic traffic — samples SimSnapshot vehicles or spawns on high-traffic road tiles.
    /// Simulation truth stays in Forge.SimCore; these meshes never feed back into the sim.
    /// </summary>
    public sealed class VehicleInstancer : MonoBehaviour
    {
        const int TypeCount = 5;

        [SerializeField] int maxVehicles = 150;
        [SerializeField] float fallbackTrafficThreshold = 0.12f;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Mesh _mesh;
        Material[] _materialsByType;
        SimSnapshot _snap;

        readonly CosmeticVehicle[] _pool = new CosmeticVehicle[256];
        int _activeCount;
        readonly Matrix4x4[][] _matricesByType = new Matrix4x4[TypeCount][];
        readonly int[] _countsByType = new int[TypeCount];
        readonly System.Random _rng = new(77);

        struct CosmeticVehicle
        {
            public bool Active;
            public byte Type;
            public Vector3 Position;
            public Vector3 Target;
            public float Speed;
            public float YawDegrees;
        }

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);

            _materialsByType = new Material[TypeCount];
            for (var t = 0; t < TypeCount; t++)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = TintForType(t);
                _materialsByType[t] = mat;
            }

            for (var t = 0; t < TypeCount; t++)
                _matricesByType[t] = new Matrix4x4[maxVehicles];

            _sim.OnSnapshotChanged += OnSnapshot;
            if (_sim.LatestSnapshot != null)
                OnSnapshot(_sim.LatestSnapshot);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshot;
        }

        void OnSnapshot(SimSnapshot snap)
        {
            _snap = snap;
            if (snap == null || _grid == null)
                return;

            if (snap.VehicleCount > 0 && snap.Vehicles != null)
                SyncFromSimVehicles(snap);
            else
                SyncFromTrafficTiles(snap);
        }

        void SyncFromSimVehicles(SimSnapshot snap)
        {
            var count = Mathf.Min(snap.VehicleCount, snap.Vehicles.Length, maxVehicles, _pool.Length);
            _activeCount = 0;

            for (var i = 0; i < count; i++)
            {
                var v = snap.Vehicles[i];
                var world = SimToUnity(v.WorldX, v.WorldY);
                var type = (byte)(v.TypeId % TypeCount);
                var yaw = v.Heading * Mathf.Rad2Deg;

                ref var slot = ref _pool[_activeCount++];
                if (!slot.Active)
                {
                    slot.Position = world;
                    slot.Active = true;
                }

                slot.Target = world;
                slot.Type = type;
                slot.Speed = Mathf.Max(0.5f, v.Speed > 0.01f ? v.Speed : v.MaxSpeed * 0.5f);
                slot.YawDegrees = yaw;
            }

            for (var i = _activeCount; i < _pool.Length; i++)
                _pool[i].Active = false;
        }

        void SyncFromTrafficTiles(SimSnapshot snap)
        {
            var roads = snap.TileRoadFlags;
            var traffic = snap.TileTraffic;
            if (roads == null || roads.Length == 0)
            {
                _activeCount = 0;
                return;
            }

            var rush = LifeSimMath.RushHourMultiplier(snap.TimeOfDay);
            var budget = Mathf.Min(maxVehicles, Mathf.RoundToInt(maxVehicles * 0.35f * rush));
            var size = Mathf.Min(_grid.MapSize, snap.WorldSize > 0 ? snap.WorldSize : _grid.MapSize);
            var step = Mathf.Max(1, size / 64);
            var candidates = new List<(int x, int y, float t)>(budget * 2);

            for (var y = 0; y < size; y += step)
            for (var x = 0; x < size; x += step)
            {
                var idx = y * size + x;
                if (idx >= roads.Length || roads[idx] == 0)
                    continue;
                var t = traffic != null && idx < traffic.Length ? traffic[idx] : 0.2f;
                if (t < fallbackTrafficThreshold)
                    continue;
                candidates.Add((x, y, t));
            }

            _activeCount = Mathf.Min(budget, candidates.Count, _pool.Length);
            for (var i = 0; i < _activeCount; i++)
            {
                var pick = candidates[_rng.Next(candidates.Count)];
                var center = _grid.TileCenter(pick.x, pick.y);
                center.y = 0.35f;

                ref var slot = ref _pool[i];
                if (!slot.Active)
                {
                    slot.Position = center;
                    slot.Active = true;
                    slot.Type = (byte)_rng.Next(TypeCount);
                    slot.YawDegrees = _rng.Next(4) * 90f;
                }

                slot.Target = center;
                slot.Speed = Mathf.Lerp(2f, 8f, pick.t) * rush;
                slot.Type = (byte)((pick.x + pick.y + i) % TypeCount);
            }

            for (var i = _activeCount; i < _pool.Length; i++)
                _pool[i].Active = false;
        }

        void LateUpdate()
        {
            if (_mesh == null || _grid == null)
                return;

            AnimatePool();
            DrawBatches();
        }

        void AnimatePool()
        {
            var dt = Time.deltaTime;
            for (var i = 0; i < _activeCount; i++)
            {
                ref var v = ref _pool[i];
                if (!v.Active)
                    continue;

                // Drift along facing when no sim path — cheap motion on road tiles.
                if (_snap == null || _snap.VehicleCount == 0)
                {
                    var rad = v.YawDegrees * Mathf.Deg2Rad;
                    var delta = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * (v.Speed * dt);
                    v.Position += delta;

                    var tile = _grid.WorldToTile(v.Position);
                    if (!_grid.InBounds(tile.x, tile.y))
                    {
                        v.YawDegrees = (v.YawDegrees + 90f) % 360f;
                        v.Position = _grid.TileCenter(tile.x, tile.y);
                        v.Position.y = 0.35f;
                    }
                }
                else
                {
                    v.Position = Vector3.MoveTowards(v.Position, v.Target, v.Speed * dt);
                }
            }
        }

        void DrawBatches()
        {
            for (var t = 0; t < TypeCount; t++)
                _countsByType[t] = 0;

            for (var i = 0; i < _activeCount; i++)
            {
                ref var v = ref _pool[i];
                if (!v.Active)
                    continue;

                var type = Mathf.Clamp(v.Type, 0, TypeCount - 1);
                var idx = _countsByType[type]++;
                if (idx >= _matricesByType[type].Length)
                    continue;

                var scale = ScaleForType(type);
                _matricesByType[type][idx] = Matrix4x4.TRS(
                    v.Position,
                    Quaternion.Euler(0f, v.YawDegrees, 0f),
                    scale);
            }

            for (var t = 0; t < TypeCount; t++)
            {
                var count = _countsByType[t];
                if (count <= 0)
                    continue;
                Graphics.DrawMeshInstanced(_mesh, 0, _materialsByType[t], _matricesByType[t], count);
            }
        }

        Vector3 SimToUnity(float simX, float simY)
        {
            if (_grid == null)
                return new Vector3(simX, 0.35f, simY);

            if (simX >= 0f && simY >= 0f && simX < _grid.MapSize && simY < _grid.MapSize)
            {
                var c = _grid.TileCenter(Mathf.FloorToInt(simX), Mathf.FloorToInt(simY));
                c.y = 0.35f;
                return c;
            }

            return new Vector3(simX, 0.35f, simY);
        }

        static Color TintForType(int type) => type switch
        {
            0 => new Color(0.55f, 0.62f, 0.72f),
            1 => new Color(0.62f, 0.48f, 0.32f),
            2 => new Color(0.92f, 0.78f, 0.18f),
            3 => new Color(0.9f, 0.22f, 0.18f),
            _ => new Color(0.28f, 0.78f, 0.42f),
        };

        static Vector3 ScaleForType(int type) => type switch
        {
            1 => new Vector3(1.8f, 0.9f, 0.9f),
            2 => new Vector3(2.4f, 1.1f, 0.95f),
            4 => new Vector3(0.5f, 0.6f, 0.25f),
            _ => new Vector3(1.2f, 0.55f, 0.65f),
        };
    }
}
