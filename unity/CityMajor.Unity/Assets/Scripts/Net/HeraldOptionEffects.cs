using Forge.SimCore;

namespace CityMajor.Net
{
    /// <summary>
    /// Council option → sim commands (mirrors web/lib/herald-option-commands.ts).
    /// </summary>
    public static class HeraldOptionEffects
    {
        sealed class Effects
        {
            public long BudgetAdjust;
            public float ApprovalDelta;
            public float ResearchBoost;
            public bool HasBudget;
            public bool HasResearch;
        }

        public static void Apply(SimHost host, string optionId, int eventId = -1)
        {
            if (host == null || string.IsNullOrEmpty(optionId))
                return;

            var effects = Lookup(optionId);

            if (effects.HasBudget && effects.BudgetAdjust != 0)
                host.AdjustBudget(effects.BudgetAdjust);

            if (effects.HasResearch && effects.ResearchBoost > 0f)
                host.BoostResearch(effects.ResearchBoost);

            host.ApplyApprovalDelta(effects.ApprovalDelta);

            if (eventId >= 0)
                host.ResolveHeraldEvent(eventId);
        }

        static Effects Lookup(string optionId) => optionId switch
        {
            "fund_clinic" => E(-2_400_000, 4f),
            "defer" => E(0, -3f),
            "private_partnership" => E(-800_000, 2f),
            "emission_caps" => E(0, 2f),
            "monitor_only" => E(0, -5f),
            "relocate_heavy" => E(-3_500_000, 3f),
            "transit_expansion" => E(-1_800_000, 4f),
            "congestion_pricing" => E(400_000, -3f, 1f),
            "status_quo" => E(0, -2f),
            "raise_taxes" => E(3_100_000, -10f),
            "cut_services" => E(0, -6f),
            "issue_bonds" => E(5_000_000, -2f),
            "inclusionary_zoning" => E(0, 3f),
            "public_housing" => E(-5_000_000, 6f),
            "rent_subsidy" => E(-900_000, 4f),
            "expand_patrols" => E(-600_000, 5f),
            "community_programs" => E(-400_000, 3f, 2f),
            "surveillance" => E(-300_000, -8f),
            "town_hall" => E(-50_000, 3f),
            "parks_program" => E(-400_000, 4f),
            "stay_course" => E(0, -3f),
            "upzone_riverfront" => E(0, -1f, 3f),
            "infrastructure_first" => E(-1_000_000, 2f, 5f),
            "reject" => E(0, 2f),
            "review_metrics" => E(0, 0f, 2f),
            "press_conference" => E(-25_000, 2f),
            _ => E(0, 0f),
        };

        static Effects E(long budget, float approval, float research = 0f) => new()
        {
            BudgetAdjust = budget,
            ApprovalDelta = approval,
            ResearchBoost = research,
            HasBudget = budget != 0,
            HasResearch = research > 0f,
        };
    }
}
