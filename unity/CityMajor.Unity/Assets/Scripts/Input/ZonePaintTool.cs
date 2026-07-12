using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Input
{
  public sealed class ZonePaintTool : MonoBehaviour
  {
    public enum ZoneKind
    {
      None,
      Residential,
      Commercial,
      Industrial,
    }

    [SerializeField] ZoneKind activeZone = ZoneKind.Residential;
    [SerializeField] int brushRadius = 2;

    Camera _camera;
    ZoneGrid _grid;
    CitySimBridge _sim;
    bool _painting;

    public void Configure(Camera cityCamera, ZoneGrid grid, CitySimBridge sim)
    {
      _camera = cityCamera;
      _grid = grid;
      _sim = sim;
    }

    void Update()
    {
      if (_camera == null || _grid == null)
        return;

      var bulldoze = GetComponent<BulldozeTool>();
      if (bulldoze != null && bulldoze.BulldozeModeActive)
      {
        _painting = false;
        return;
      }

      var roadTool = GetComponent<RoadPaintTool>();
      if (roadTool != null && roadTool.RoadModeActive)
      {
        _painting = false;
        return;
      }

      var buildTool = GetComponent<BuildPlopTool>();
      if (buildTool != null && buildTool.BuildModeActive)
      {
        _painting = false;
        return;
      }

      if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) activeZone = ZoneKind.Residential;
      if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) activeZone = ZoneKind.Commercial;
      if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) activeZone = ZoneKind.Industrial;
      if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0)) activeZone = ZoneKind.None;

      if (UnityEngine.Input.GetMouseButtonDown(0))
        _painting = true;
      if (UnityEngine.Input.GetMouseButtonUp(0))
        _painting = false;

      if (_painting || UnityEngine.Input.GetMouseButtonDown(0))
        PaintAtMouse();
    }

    void PaintAtMouse()
    {
      var ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
      if (!Physics.Raycast(ray, out var hit, 5000f))
        return;

      var center = _grid.WorldToTile(hit.point);
      for (var dy = -brushRadius; dy <= brushRadius; dy++)
      for (var dx = -brushRadius; dx <= brushRadius; dx++)
      {
        if (dx * dx + dy * dy > brushRadius * brushRadius)
          continue;
        var x = center.x + dx;
        var y = center.y + dy;
        _grid.SetZone(x, y, activeZone);
        _sim?.PaintZone(x, y, activeZone);
      }

      if (_sim != null && !_sim.UsesForgeSimCore)
        _sim.NotifyZonesChanged();
    }

    public void SetActiveZone(ZoneKind zone) => activeZone = zone;
  }
}
