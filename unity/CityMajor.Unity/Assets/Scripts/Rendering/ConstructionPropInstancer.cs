using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>Crane / scaffold cubes on buildings under construction (State != active).</summary>
    public sealed class ConstructionPropInstancer : MonoBehaviour
    {
        [SerializeField] int maxProps = 128;

        ZoneGrid _grid;
        CitySimBridge _sim;
        Mesh _mesh;
        Material _mat;
        readonly List<Matrix4x4> _matrices = new();
        Matrix4x4[] _drawBuffer = System.Array.Empty<Matrix4x4>();

        public void Configure(ZoneGrid grid, CitySimBridge sim)
        {
            _grid = grid;
            _sim = sim;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);

            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.95f, 0.72f, 0.2f, 1f),
            };

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
            _matrices.Clear();
            if (snap?.Buildings == null || _grid == null)
                return;

            foreach (var b in snap.Buildings)
            {
                if (b.State == 1 && b.Condition >= 200)
                    continue;
                if (_matrices.Count >= maxProps)
                    break;

                var center = _grid.TileCenter(b.GridX, b.GridY);
                center.y = 3.5f;
                _matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, new Vector3(0.35f, 2.8f, 0.35f)));
            }

            _drawBuffer = _matrices.Count == 0
                ? System.Array.Empty<Matrix4x4>()
                : _matrices.ToArray();
        }

        void LateUpdate()
        {
            if (_mesh == null || _mat == null || _drawBuffer.Length == 0)
                return;

            Graphics.DrawMeshInstanced(_mesh, 0, _mat, _drawBuffer, _drawBuffer.Length);
        }
    }
}
