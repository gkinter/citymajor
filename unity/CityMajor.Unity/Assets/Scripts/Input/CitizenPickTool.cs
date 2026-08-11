using CityMajor.Rendering;
using CityMajor.Sim;
using CityMajor.UI;
using UnityEngine;

namespace CityMajor.Input
{
    /// <summary>
    /// Ray pick citizen dots (LMB) → open citizen panel with job / commute / rent (P4.4).
    /// </summary>
    public sealed class CitizenPickTool : MonoBehaviour
    {
        Camera _cam;
        PedestrianInstancer _peds;
        CitizenPanelController _panel;

        public void Configure(Camera cam, PedestrianInstancer peds, CitizenPanelController panel)
        {
            _cam = cam;
            _peds = peds;
            _panel = panel;
        }

        void Update()
        {
            if (_cam == null || _peds == null || _panel == null)
                return;

            if (!UnityEngine.Input.GetMouseButtonDown(0))
                return;

            var ray = _cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var enter))
                return;

            var hit = ray.GetPoint(enter);
            if (_peds.TryPick(hit, radius: 1.5f, out var householdId))
                _panel.SelectHousehold(householdId);
        }
    }
}
