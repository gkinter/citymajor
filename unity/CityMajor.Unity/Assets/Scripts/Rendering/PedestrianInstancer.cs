using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Cosmetic pedestrians + citizen dots — clusters near commercial/service tiles or L2 household sample.
    /// Never feeds back into Forge.SimCore.
    /// </summary>
    public sealed class PedestrianInstancer : MonoBehaviour
    {
        [SerializeField] int maxPedestrians = 200;
        [SerializeField] float clusterRadius = 1.2f;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Mesh _mesh;
        readonly Material[] _materials = new Material[3];
        readonly Matrix4x4[][] _matricesByTier = { new Matrix4x4[256], new Matrix4x4[256], new Matrix4x4[256] };
        readonly int[] _countsByTier = new int[3];
        SimSnapshot _snap;
        CitySimState _state;

        readonly List<PedSlot> _slots = new(maxPedestrians);
        readonly System.Random _rng = new(91);

        struct PedSlot
        {
            public Vector3 Position;
            public Vector3 Target;
            public float Speed;
            public byte Tier;
            public string HouseholdId;
        }

        static byte TierForHappiness(float happiness)
        {
            if (happiness >= 0.65f) return 0;
            if (happiness >= 0.4f) return 1;
            return 2;
        }

        static Color TierColor(byte tier) => tier switch
        {
            0 => new Color(0.35f, 0.85f, 0.45f),
            1 => new Color(0.95f, 0.75f, 0.35f),
            _ => new Color(0.95f, 0.35f, 0.32f),
        };

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);

            for (var t = 0; t < 3; t++)
            {
                _materials[t] = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = TierColor((byte)t),
                    enableInstancing = true,
                };
            }

            _sim.OnSnapshotChanged += OnSnapshot;
            _sim.OnStateChanged += OnState;
            if (_sim.LatestSnapshot != null)
                OnSnapshot(_sim.LatestSnapshot);
            OnState(_sim.State);
        }

        void OnDestroy()
        {
            if (_sim == null)
                return;
            _sim.OnSnapshotChanged -= OnSnapshot;
            _sim.OnStateChanged -= OnState;
        }

        void OnSnapshot(SimSnapshot snap) => _snap = snap;

        void OnState(CitySimState state)
        {
            _state = state;
            RebuildSlots();
        }

        void RebuildSlots()
        {
            _slots.Clear();
            if (_grid == null)
                return;

            var rush = _state.RushMultiplier > 0f
                ? _state.RushMultiplier
                : LifeSimMath.RushHourMultiplier(_snap?.TimeOfDay ?? 12f);
            var budget = Mathf.Min(maxPedestrians, Mathf.RoundToInt(maxPedestrians * 0.45f * rush));

            if (_state.Households is { Length: > 0 })
            {
                for (var i = 0; i < _state.Households.Length && _slots.Count < budget; i++)
                {
                    var h = _state.Households[i];
                    var center = _grid.TileCenter(h.TileX, h.TileZ);
                    center.y = 0.55f;
                    var jitter = new Vector3(
                        (float)(_rng.NextDouble() - 0.5) * clusterRadius,
                        0f,
                        (float)(_rng.NextDouble() - 0.5) * clusterRadius);
                    var pos = center + jitter;
                    _slots.Add(new PedSlot
                    {
                        Position = pos,
                        Target = pos + RandomWalkDelta(),
                        Speed = Mathf.Lerp(0.8f, 2.2f, h.Happiness),
                        Tier = TierForHappiness(h.Happiness),
                        HouseholdId = h.Id,
                    });
                }

                return;
            }

            var zones = _snap?.TileZoneTypes;
            if (zones == null || zones.Length == 0)
                return;

            var size = _grid.MapSize;
            var step = Mathf.Max(1, size / 48);
            var candidates = new List<(int x, int y, byte zone)>(budget * 3);

            for (var y = 0; y < size; y += step)
            for (var x = 0; x < size; x += step)
            {
                var idx = y * size + x;
                if (idx >= zones.Length)
                    continue;
                var z = zones[idx];
                if (z is 1 or 2 or 3)
                    candidates.Add((x, y, z));
            }

            for (var i = 0; i < budget && candidates.Count > 0; i++)
            {
                var pick = candidates[_rng.Next(candidates.Count)];
                var center = _grid.TileCenter(pick.x, pick.y);
                center.y = 0.55f;
                var happiness = pick.zone == 3 ? 0.62f : 0.55f;
                _slots.Add(new PedSlot
                {
                    Position = center,
                    Target = center + RandomWalkDelta(),
                    Speed = 1.2f,
                    Tier = TierForHappiness(happiness),
                    HouseholdId = $"HH-{pick.x}-{pick.y}",
                });
            }
        }

        Vector3 RandomWalkDelta()
        {
            var a = (float)_rng.NextDouble() * Mathf.PI * 2f;
            var d = (float)_rng.NextDouble() * clusterRadius * 2f;
            return new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d);
        }

        void LateUpdate()
        {
            if (_mesh == null || _slots.Count == 0)
                return;

            for (var t = 0; t < 3; t++)
                _countsByTier[t] = 0;

            var dt = Time.deltaTime;
            var scale = new Vector3(0.22f, 0.35f, 0.22f);
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                slot.Position = Vector3.MoveTowards(slot.Position, slot.Target, slot.Speed * dt);
                if ((slot.Position - slot.Target).sqrMagnitude < 0.04f)
                    slot.Target = slot.Position + RandomWalkDelta();
                _slots[i] = slot;

                var tier = Mathf.Clamp(slot.Tier, (byte)0, (byte)2);
                var idx = _countsByTier[tier]++;
                if (idx < _matricesByTier[tier].Length)
                    _matricesByTier[tier][idx] = Matrix4x4.TRS(slot.Position, Quaternion.identity, scale);
            }

            for (var t = 0; t < 3; t++)
            {
                var count = _countsByTier[t];
                if (count <= 0 || _materials[t] == null)
                    continue;
                Graphics.DrawMeshInstanced(_mesh, 0, _materials[t], _matricesByTier[t], count);
            }
        }

        public bool TryPick(Vector3 worldPoint, float radius, out string householdId)
        {
            householdId = "";
            var r2 = radius * radius;
            for (var i = 0; i < _slots.Count; i++)
            {
                if ((_slots[i].Position - worldPoint).sqrMagnitude <= r2)
                {
                    householdId = _slots[i].HouseholdId;
                    return !string.IsNullOrEmpty(householdId);
                }
            }

            return false;
        }
    }
}
