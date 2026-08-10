using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Cathedral P5.5 / P6.3 — deterministic Herald unrest trigger gated on mayor approval.
/// Fires <c>approval_unrest</c> only while snapshot approval stays below the Politics
/// protest-escalation band (&lt;40%); eliminates false-positive unrest when approval is healthy.
/// </summary>
public sealed class ApprovalHeraldSystem
{
  /// <summary>
  /// Approval percent below this value maps to the happiness_low / unrest Herald bucket
  /// (matches <see cref="PoliticsSystem.UpdateProtests"/> escalation band).
  /// </summary>
  public const float UnrestApprovalThresholdPercent = 40f;

  public const int UnrestConsecutiveMonths = 2;

  public const string UnrestEventTypeId = "approval_unrest";

  private int _monthsBelowThreshold;

  /// <summary>
  /// Snapshot predicate: unrest / happiness_low Herald only when approval percent is below threshold.
  /// Mirrors web <c>LOW_HAPPINESS_APPROVAL</c> / Unity <c>LowHappinessApproval</c>.
  /// </summary>
  public static bool IsUnrestBucket(float approvalPercent) =>
    approvalPercent < UnrestApprovalThresholdPercent;

  /// <summary>
  /// Evaluate monthly approval Herald trigger after politics writes <see cref="WorldState.ApprovalRating"/>.
  /// </summary>
  public void MonthlyTick(WorldState state, EventSystem events)
  {
    float approvalPercent = state.ApprovalRating * 100f;

    if (IsUnrestBucket(approvalPercent))
      _monthsBelowThreshold++;
    else
      _monthsBelowThreshold = 0;

    if (_monthsBelowThreshold < UnrestConsecutiveMonths) return;
    if (events.IsEventTypeActive(UnrestEventTypeId)) return;

    if (events.TriggerEvent(UnrestEventTypeId, state, severityOverride: 0.70f))
      _monthsBelowThreshold = 0;
  }
}
