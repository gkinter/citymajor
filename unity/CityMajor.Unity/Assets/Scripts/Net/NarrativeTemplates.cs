using System;
using System.Collections.Generic;
using Forge.Engine.Simulation;

namespace CityMajor.Net
{
    /// <summary>
    /// Template fallback when the narrative API is unreachable (mirrors web/lib/narrative-templates.ts).
    /// </summary>
    public static class NarrativeTemplates
    {
        public const string CityPlaceholder = "the city";

        public enum SimStateBucket
        {
            HealthcareLow,
            PollutionSpike,
            TrafficCongestion,
            BudgetCrisis,
            HousingShortage,
            EconomyShortage,
            CrimeRising,
            HappinessLow,
            ProsperityHigh,
            Default,
        }

        public sealed class NarrativeOption
        {
            public string Id;
            public string Label;
            public string Tradeoff;
        }

        public sealed class NarrativeEvent
        {
            public SimStateBucket Bucket;
            public string Headline;
            public string Body;
            public List<NarrativeOption> Options = new();
            public string Source = "template";
        }

        const float RciExtremeDemand = 0.65f;
        const float GoodsShortageThreshold = 0.35f;
        const float LowHappinessApproval = 40f;
        const float ProsperityApproval = 70f;
        const long ProsperityFunds = 500_000;

        public static string BucketToApiKey(SimStateBucket bucket) => bucket switch
        {
            SimStateBucket.HealthcareLow => "healthcare_low",
            SimStateBucket.PollutionSpike => "pollution_spike",
            SimStateBucket.TrafficCongestion => "traffic_congestion",
            SimStateBucket.BudgetCrisis => "budget_crisis",
            SimStateBucket.HousingShortage => "housing_shortage",
            SimStateBucket.EconomyShortage => "economy_shortage",
            SimStateBucket.CrimeRising => "crime_rising",
            SimStateBucket.HappinessLow => "happiness_low",
            SimStateBucket.ProsperityHigh => "prosperity_high",
            _ => "default",
        };

        public static SimStateBucket ApiKeyToBucket(string key) => key switch
        {
            "healthcare_low" => SimStateBucket.HealthcareLow,
            "pollution_spike" => SimStateBucket.PollutionSpike,
            "traffic_congestion" => SimStateBucket.TrafficCongestion,
            "budget_crisis" => SimStateBucket.BudgetCrisis,
            "housing_shortage" => SimStateBucket.HousingShortage,
            "economy_shortage" => SimStateBucket.EconomyShortage,
            "crime_rising" => SimStateBucket.CrimeRising,
            "happiness_low" => SimStateBucket.HappinessLow,
            "prosperity_high" => SimStateBucket.ProsperityHigh,
            _ => SimStateBucket.Default,
        };

        public static SimStateBucket DeriveBucket(
            float healthcareCoverage,
            float approvalPercent,
            long cityFunds,
            float residentialDemand,
            float commercialDemand,
            float industrialDemand,
            float goodsShortageIndex,
            float goodsSurplusIndex = 0f)
        {
            _ = goodsSurplusIndex;

            if (cityFunds < 0)
                return SimStateBucket.BudgetCrisis;

            if (goodsShortageIndex >= GoodsShortageThreshold)
            {
                if (residentialDemand >= RciExtremeDemand)
                    return SimStateBucket.HousingShortage;

                return SimStateBucket.EconomyShortage;
            }

            if (approvalPercent < LowHappinessApproval)
                return SimStateBucket.HappinessLow;

            if (residentialDemand >= RciExtremeDemand)
                return SimStateBucket.HousingShortage;

            if (commercialDemand >= RciExtremeDemand
                && industrialDemand >= RciExtremeDemand
                && cityFunds >= 0)
                return SimStateBucket.ProsperityHigh;

            if (healthcareCoverage < 0.3f)
                return SimStateBucket.HealthcareLow;

            if (approvalPercent >= ProsperityApproval && cityFunds >= ProsperityFunds)
                return SimStateBucket.ProsperityHigh;

            return SimStateBucket.Default;
        }

        public static string ExplainBucket(
            SimStateBucket bucket,
            float approvalPercent,
            long cityFunds,
            float goodsShortageIndex = 0f)
        {
            return bucket switch
            {
                SimStateBucket.BudgetCrisis =>
                    $"Treasury {FormatFundsShort(cityFunds)} — deficit stories take priority in the Herald",
                SimStateBucket.HappinessLow =>
                    $"Approval {approvalPercent:0}% — unrest coverage in the Herald",
                SimStateBucket.HousingShortage when goodsShortageIndex >= GoodsShortageThreshold =>
                    $"Goods shortage {goodsShortageIndex * 100f:0}% and residential demand extreme — housing pressure edition",
                SimStateBucket.HousingShortage => "Residential demand is extreme — housing shortage edition",
                SimStateBucket.EconomyShortage =>
                    $"Goods shortage index {goodsShortageIndex * 100f:0}% — supply-chain coverage in the Herald",
                SimStateBucket.HealthcareLow => "Healthcare coverage is low — clinic petition edition",
                SimStateBucket.ProsperityHigh => "Strong approval and treasury — investor expansion edition",
                _ => "Balanced city pulse — general Herald coverage",
            };
        }

        public static string FormatGoodsShortageLine(float goodsShortageIndex) =>
            $"Goods shortage index: {goodsShortageIndex * 100f:0}%";

        public static NarrativeEvent FromBucket(SimStateBucket bucket)
        {
            var template = Templates[bucket];
            return new NarrativeEvent
            {
                Bucket = bucket,
                Headline = template.Headline,
                Body = template.Body,
                Options = new List<NarrativeOption>(template.Options),
                Source = "template",
            };
        }

        public static NarrativeEvent FromSnapshot(SimSnapshot snap, CityMajor.Sim.CitySimState state)
        {
            var healthcare = EstimateHealthcareCoverage(snap);
            var approvalPct = snap.ApprovalRating * 100f;
            var goodsShortage = snap.GoodsShortageIndex;
            var goodsSurplus = snap.GoodsSurplusIndex;

            var bucket = DeriveBucket(
                healthcare,
                approvalPct,
                snap.CityFunds,
                state.DemandResidential,
                state.DemandCommercial,
                state.DemandIndustrial,
                goodsShortage,
                goodsSurplus);

            return FromBucket(bucket);
        }

        public static float EstimateHealthcareCoverage(SimSnapshot snap)
        {
            if (snap.BuildingCount <= 0)
                return 0.15f;

            // Service buildings proxy until WASM exports aggregate coverage.
            var serviceCount = 0;
            if (snap.Buildings != null)
            {
                foreach (var b in snap.Buildings)
                {
                    if (b.TypeId is >= 200 and < 300)
                        serviceCount++;
                }
            }

            var ratio = serviceCount / Math.Max(1f, snap.BuildingCount / 400f);
            return Math.Clamp(ratio, 0f, 1f);
        }

        static string FormatFundsShort(long cityFunds)
        {
            var abs = Math.Abs(cityFunds);
            if (abs >= 1_000_000)
                return $"${cityFunds / 1_000_000f:0.0}M";
            if (abs >= 1_000)
                return $"${cityFunds / 1_000f:0.0}K";
            return $"${cityFunds:N0}";
        }

        static string EraName(int era) => era switch
        {
            0 => "Ancient",
            1 => "Medieval",
            2 => "Colonial",
            3 => "Industrial",
            4 => "Modern",
            5 => "Future",
            _ => "Frontier",
        };

        sealed class TemplateEntry
        {
            public string Headline;
            public string Body;
            public NarrativeOption[] Options;
        }

        static readonly Dictionary<SimStateBucket, TemplateEntry> Templates = new()
        {
            [SimStateBucket.HealthcareLow] = new()
            {
                Headline = "Clinic petition reaches city hall",
                Body = "Healthcare gaps are widening across the city. Dr. Amara Osei reports treating patients in apartments because the nearest hospital is a long bus ride away. Riverside residents demand a neighborhood clinic.",
                Options = new[]
                {
                    new NarrativeOption { Id = "fund_clinic", Label = "Fund a public clinic", Tradeoff = "−$2.4M budget, +healthcare coverage in Riverside" },
                    new NarrativeOption { Id = "defer", Label = "Defer to next budget cycle", Tradeoff = "No immediate cost, satisfaction −8 in affected districts" },
                    new NarrativeOption { Id = "private_partnership", Label = "Offer tax incentives to a private provider", Tradeoff = "Lower upfront cost, slower coverage gains" },
                },
            },
            [SimStateBucket.PollutionSpike] = new()
            {
                Headline = "Smog advisory issued for South End",
                Body = "Industrial emissions pushed particulate levels past safe thresholds across the city. Respiratory admissions are climbing and the Greens faction is calling for emergency zoning review.",
                Options = new[]
                {
                    new NarrativeOption { Id = "emission_caps", Label = "Impose temporary emission caps", Tradeoff = "−12% industrial output, pollution −25%" },
                    new NarrativeOption { Id = "monitor_only", Label = "Monitor and issue health guidance", Tradeoff = "No economic hit, pollution persists, approval −5" },
                    new NarrativeOption { Id = "relocate_heavy", Label = "Fast-track heavy industry relocation", Tradeoff = "High relocation subsidies, long-term land-value recovery" },
                },
            },
            [SimStateBucket.TrafficCongestion] = new()
            {
                Headline = "Commute times hit a new peak",
                Body = "Average rush-hour travel time crossed 48 minutes. Logistics firms warn that delivery delays are raising retail prices across the city.",
                Options = new[]
                {
                    new NarrativeOption { Id = "transit_expansion", Label = "Expand bus lanes and frequency", Tradeoff = "−$1.8M OPEX, traffic −15% in core corridors" },
                    new NarrativeOption { Id = "congestion_pricing", Label = "Pilot congestion pricing downtown", Tradeoff = "Revenue +$400K/mo, unpopular with commuters" },
                    new NarrativeOption { Id = "status_quo", Label = "Hold current transport plan", Tradeoff = "No spend, congestion worsens over 2 quarters" },
                },
            },
            [SimStateBucket.BudgetCrisis] = new()
            {
                Headline = "Treasury projects a shortfall",
                Body = "The city's projected expenses exceed revenue for the third consecutive quarter. Department heads are asked to submit 8% contingency cuts.",
                Options = new[]
                {
                    new NarrativeOption { Id = "raise_taxes", Label = "Raise income tax 2 points", Tradeoff = "+$3.1M revenue, business faction approval −10" },
                    new NarrativeOption { Id = "cut_services", Label = "Trim non-essential services", Tradeoff = "Satisfaction −6, preserves current tax rate" },
                    new NarrativeOption { Id = "issue_bonds", Label = "Issue municipal bonds", Tradeoff = "Immediate liquidity, debt service +$200K/mo" },
                },
            },
            [SimStateBucket.HousingShortage] = new()
            {
                Headline = "Rent burden triggers protest organizing",
                Body = "Median rent now consumes 42% of household income in the North Quarter. Tenants' unions across the city plan a march unless affordable units are approved.",
                Options = new[]
                {
                    new NarrativeOption { Id = "inclusionary_zoning", Label = "Mandate 15% affordable units in new builds", Tradeoff = "Developer pushback, gradual rent relief" },
                    new NarrativeOption { Id = "public_housing", Label = "Break ground on public housing blocks", Tradeoff = "−$5M capital, +1,200 affordable units over 18 months" },
                    new NarrativeOption { Id = "rent_subsidy", Label = "Launch a temporary rent subsidy", Tradeoff = "−$900K/mo OPEX, fast relief, no new supply" },
                },
            },
            [SimStateBucket.EconomyShortage] = new()
            {
                Headline = "Store shelves thin as supply chains strain",
                Body = "Wholesale buyers report persistent stockouts across the city. Shopkeepers blame freight delays and weak industrial throughput; households are paying more for basics.",
                Options = new[]
                {
                    new NarrativeOption { Id = "industrial_incentives", Label = "Offer tax breaks for new industrial capacity", Tradeoff = "−$1.2M revenue, gradual goods relief" },
                    new NarrativeOption { Id = "import_subsidy", Label = "Subsidize emergency freight imports", Tradeoff = "−$800K/quarter, faster shelf recovery" },
                    new NarrativeOption { Id = "rationing_review", Label = "Study rationing for essential goods", Tradeoff = "Stabilizes prices, unpopular with retailers" },
                },
            },
            [SimStateBucket.CrimeRising] = new()
            {
                Headline = "Police chief requests overtime budget",
                Body = "Property crime rose 18% quarter-over-quarter across the city. Shop owners on Market Street are closing early and demanding visible patrols.",
                Options = new[]
                {
                    new NarrativeOption { Id = "expand_patrols", Label = "Fund overtime patrols", Tradeoff = "−$600K/quarter, crime −10%, police approval +5" },
                    new NarrativeOption { Id = "community_programs", Label = "Invest in youth and community programs", Tradeoff = "Slower crime reduction, long-term satisfaction gains" },
                    new NarrativeOption { Id = "surveillance", Label = "Install CCTV network in hot spots", Tradeoff = "Crime −6%, civil-liberties faction approval −8" },
                },
            },
            [SimStateBucket.HappinessLow] = new()
            {
                Headline = "Approval ratings slip below comfort zone",
                Body = "Satisfaction across the city dipped under 45%. Local media run editorials asking whether current leadership still reflects residents' priorities.",
                Options = new[]
                {
                    new NarrativeOption { Id = "town_hall", Label = "Hold a televised town hall", Tradeoff = "Minor approval bump if promises are kept" },
                    new NarrativeOption { Id = "parks_program", Label = "Launch a quick-win parks beautification blitz", Tradeoff = "−$400K, +satisfaction in 3 districts" },
                    new NarrativeOption { Id = "stay_course", Label = "Stay the course", Tradeoff = "Risk further approval erosion" },
                },
            },
            [SimStateBucket.ProsperityHigh] = new()
            {
                Headline = "Investors eye expansion zones",
                Body = "Tax revenue and employment are both above trend. Commercial developers petition for upzoning along the city's riverfront corridor.",
                Options = new[]
                {
                    new NarrativeOption { Id = "upzone_riverfront", Label = "Approve riverfront upzoning", Tradeoff = "+jobs, +traffic and pollution pressure" },
                    new NarrativeOption { Id = "infrastructure_first", Label = "Require infrastructure upgrades first", Tradeoff = "Slower growth, fewer downstream crises" },
                    new NarrativeOption { Id = "reject", Label = "Reject upzoning for now", Tradeoff = "Preserve character, forgo near-term revenue spike" },
                },
            },
            [SimStateBucket.Default] = new()
            {
                Headline = "Council notes for the week",
                Body = "Routine municipal business continues across the city. Advisors recommend monitoring healthcare, pollution, and budget metrics for emerging issues.",
                Options = new[]
                {
                    new NarrativeOption { Id = "review_metrics", Label = "Review city metrics", Tradeoff = "No immediate cost" },
                    new NarrativeOption { Id = "press_conference", Label = "Hold a press conference", Tradeoff = "Small approval swing depending on recent performance" },
                },
            },
        };

        public static string EraToApiString(int era) => EraName(era).ToLowerInvariant();
    }
}
