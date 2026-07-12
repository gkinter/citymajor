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
        Material _mat;
        SimSnapshot _snap;
        CitySimState _state;

        readonly List<PedSlot> _slots = new(maxPedestrians);
        readonly Matrix4x4[] _matrices = new Matrix4x4[256];
        readonly System.Random _rng = new(91);

        struct PedSlot
        {
            public Vector3 Position;
            public Vector3 Target;
            public float Speed;
            public Color Tint;
            public string HouseholdId;
        }

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);

            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _mat.enableInstancing = true;

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
                    var (r, g, b) = LifeSimMath.HappinessRgb(h.Happiness);
                    _slots.Add(new PedSlot
                    {
                        Position = pos,
                        Target = pos + RandomWalkDelta(),
                        Speed = Mathf.Lerp(0.8f, 2.2f, h.Happiness),
                        Tint = new Color(r, g, b, 1f),
                        HouseholdId = h.Id,
                    });
                }

                return;
            }

            // Fallback: cluster near commercial + residential zones.
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
                var (r, g, b) = LifeSimMath.HappinessRgb(happiness);
                _slots.Add(new PedSlot
                {
                    Position = center,
                    Target = center + RandomWalkDelta(),
                    Speed = 1.2f,
                    Tint = new Color(r, g, b, 1f),
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
            if (_mesh == null || _mat == null || _slots.Count == 0)
                return;

            var dt = Time.deltaTime;
            var count = Mathf.Min(_slots.Count, _matrices.Length);
            for (var i = 0; i < count; i++)
            {
                var slot = _slots[i];
                slot.Position = Vector3.MoveTowards(slot.Position, slot.Target, slot.Speed * dt);
                if ((slot.Position - slot.Target).sqrMagnitude < 0.04f)
                    slot.Target = slot.Position + RandomWalkDelta();
                _slots[i] = slot;

                _matrices[i] = Matrix4x4.TRS(slot.Position, Quaternion.identity, new Vector3(0.22f, 0.35f, 0.22f));
            }

            // Per-instance color via MaterialPropertyBlock would be ideal; single tint batch for scaffold.
            var avg = AverageTint(count);
            _mat.color = avg;
            Graphics.DrawMeshInstanced(_mesh, 0, _mat, _matrices, count);
        }

        Color AverageTint(int count)
        {
            if (count <= 0)
                return Color.white;
            var r = 0f;
            var g = 0f;
            var b = 0f;
            for (var i = 0; i < count; i++)
            {
                var c = _slots[i].Tint;
                r += c.r;
                g += c.g;
                b += c.b;
            }

            var inv = 1f / count;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }

        /// <summary>Ray pick for citizen panel — returns household id if within radius.</summary>
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
