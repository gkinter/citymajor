using UnityEngine;

namespace CityMajor.Core
{
    /// <summary>
    /// Pan / zoom isometric rig for city view (Cities: Skylines–style orbit).
    /// </summary>
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [SerializeField] float yaw = 45f;
        [SerializeField] float pitch = 35f;
        [SerializeField] float distance = 120f;
        [SerializeField] float minDistance = 40f;
        [SerializeField] float maxDistance = 400f;
        [SerializeField] float panSpeed = 0.4f;
        [SerializeField] float zoomSpeed = 8f;

        Vector3 _focus = Vector3.zero;

        /// <summary>Current orbit distance — used by building LOD and overlays.</summary>
        public float Distance => distance;

        public void FocusOn(Vector3 worldPoint)
        {
            _focus = worldPoint;
            ApplyTransform();
        }

        void LateUpdate()
        {
            if (UnityEngine.Input.GetMouseButton(2) || (UnityEngine.Input.GetMouseButton(1) && UnityEngine.Input.GetKey(KeyCode.LeftAlt)))
            {
                var right = transform.right;
                right.y = 0f;
                right.Normalize();
                var forward = transform.forward;
                forward.y = 0f;
                forward.Normalize();
                var delta = new Vector3(UnityEngine.Input.GetAxis("Mouse X"), 0f, UnityEngine.Input.GetAxis("Mouse Y"));
                _focus -= (right * delta.x + forward * delta.z) * panSpeed * distance * 0.05f;
            }

            distance -= UnityEngine.Input.mouseScrollDelta.y * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            ApplyTransform();
        }

        void ApplyTransform()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rot;
            transform.position = _focus - rot * Vector3.forward * distance;
        }
    }
}
