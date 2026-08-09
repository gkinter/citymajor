using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>
    /// Animates building scale from foundation → full height when new growth appears (v1.5 scaffold).
    /// BuildingInstancer queries <see cref="ScaleFor"/> when composing matrices.
    /// </summary>
    public sealed class ZoneGrowthVisualizer : MonoBehaviour
    {
        [SerializeField] float growSpeed = 0.35f;
        [SerializeField] float foundationScale = 0.28f;

        readonly Dictionary<long, float> _growth = new();

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            _sim.OnSnapshotChanged += OnSnapshot;
            if (_sim.LatestSnapshot != null)
                OnSnapshot(_sim.LatestSnapshot);
        }

        CitySimBridge _sim;

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnSnapshotChanged -= OnSnapshot;
        }

        void OnSnapshot(SimSnapshot snap)
        {
            if (snap?.Buildings == null)
                return;

            var seen = new HashSet<long>();
            foreach (var b in snap.Buildings)
            {
                if (b.TypeId == 0 || (b.State != 0 && b.State != 1))
                    continue;

                var key = Key(b.GridX, b.GridY);
                seen.Add(key);
                if (!_growth.ContainsKey(key))
                {
                    _growth[key] = b.State == 0
                        ? Mathf.Clamp(b.Condition / 255f, 0.1f, 1f)
                        : b.Condition < 200 ? foundationScale : 1f;
                }
            }

            // Drop demolished / inactive keys.
            var stale = new List<long>();
            foreach (var key in _growth.Keys)
            {
                if (!seen.Contains(key))
                    stale.Add(key);
            }

            foreach (var key in stale)
                _growth.Remove(key);
        }

        void Update()
        {
            if (_growth.Count == 0)
                return;

            var keys = new List<long>(_growth.Keys);
            foreach (var key in keys)
            {
                var v = _growth[key];
                if (v < 1f)
                    _growth[key] = Mathf.MoveTowards(v, 1f, growSpeed * Time.deltaTime);
            }
        }

        public float ScaleFor(int tileX, int tileY, byte state, byte condition)
        {
            if (state == 0)
                return Mathf.Clamp(condition / 255f, 0.1f, 1f);

            var key = Key(tileX, tileY);
            if (_growth.TryGetValue(key, out var scale))
                return scale;

            return condition < 200 ? foundationScale : 1f;
        }

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
