using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Cathedral P2.4 — deterministic Herald triggers driven by rent burden and housing vacancy.
/// Fires <c>housing_crisis</c> and <c>housing_shortage</c> sim events when thresholds hold
/// for consecutive months; web EventTicker reads active events via WASM export.
/// </summary>
public sealed class HousingHeraldSystem
{
  public const float HousingCrisisBurdenThreshold = 0.50f;
  public const int HousingCrisisConsecutiveMonths = 2;

  public const float HousingShortageVacancyThreshold = 0.05f;
  public const float HousingShortageDemandThreshold = 0.30f;
  public const int HousingShortageConsecutiveMonths = 2;

  private int _monthsHighMeanBurden;
  private int _monthsHousingShortage;

  /// <summary>
  /// City-wide residential vacancy: 1 − occupied capacity / total capacity.
  /// Returns 1 when no residential buildings exist (no supply pressure).
  /// </summary>
  public static float CalculateCityVacancy(WorldState state)
  {
    int totalCapacity = 0;
    int totalOccupants = 0;
    var buildings = state.Buildings;

    for (int b = 0; b < buildings.Capacity; b++)
    {
      if (!buildings.IsActive(b)) continue;
      byte zone = GetResidentialZoneType(state, b);
      if (zone is not (1 or 2 or 6)) continue;

      totalCapacity += buildings.MaxOccupants[b];
      totalOccupants += buildings.Occupants[b];
    }

    if (totalCapacity == 0) return 1f;
    return 1f - (float)totalOccupants / totalCapacity;
  }

  /// <summary>
  /// Evaluate monthly housing Herald triggers after rent burden and RCI demand rollups.
  /// </summary>
  public void MonthlyTick(
    WorldState state,
    EventSystem events,
    float meanRentBurden,
    float residentialDemand)
  {
    EvaluateHousingCrisis(state, events, meanRentBurden);
    EvaluateHousingShortage(state, events, residentialDemand);
  }

  private void EvaluateHousingCrisis(WorldState state, EventSystem events, float meanRentBurden)
  {
    if (meanRentBurden > HousingCrisisBurdenThreshold)
      _monthsHighMeanBurden++;
    else
      _monthsHighMeanBurden = 0;

    if (_monthsHighMeanBurden < HousingCrisisConsecutiveMonths) return;
    if (events.IsEventTypeActive("housing_crisis")) return;

    if (events.TriggerEvent("housing_crisis", state, severityOverride: 0.75f))
      _monthsHighMeanBurden = 0;
  }

  private void EvaluateHousingShortage(WorldState state, EventSystem events, float residentialDemand)
  {
    float vacancy = CalculateCityVacancy(state);
    bool shortagePressure =
      vacancy < HousingShortageVacancyThreshold
      && residentialDemand > HousingShortageDemandThreshold;

    if (shortagePressure)
      _monthsHousingShortage++;
    else
      _monthsHousingShortage = 0;

    if (_monthsHousingShortage < HousingShortageConsecutiveMonths) return;
    if (events.IsEventTypeActive("housing_shortage")) return;

    if (events.TriggerEvent("housing_shortage", state, severityOverride: 0.65f))
      _monthsHousingShortage = 0;
  }

  private static byte GetResidentialZoneType(WorldState state, int buildingId)
  {
    if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return 0;
    int gx = state.Buildings.GridX[buildingId];
    int gy = state.Buildings.GridY[buildingId];
    if (!state.Tiles.InBounds(gx, gy)) return 0;
    return state.Tiles.ZoneType[state.Tiles.Index(gx, gy)];
  }
}
