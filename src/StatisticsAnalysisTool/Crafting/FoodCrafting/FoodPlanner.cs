using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public sealed class FoodPlanInput
{
    public decimal FocusBudget { get; init; }

    /// <summary>Max total investment (silver). 0 = no limit.</summary>
    public decimal MaxInvestment { get; init; }

    public decimal DailyVolumeDivisor { get; init; } = 10m;
    public decimal MinProfitPerFocus { get; init; }
}

public sealed class FoodPlanEntry
{
    public FoodCandidate Candidate { get; init; }
    public int Crafts { get; init; }
    public int MaxCrafts { get; init; }

    public int Items => Crafts * Math.Max(1, Candidate?.AmountCrafted ?? 1);
    public decimal Focus => Crafts * (Candidate?.FocusPerCraft ?? 0m);
    public decimal Profit => Crafts * (Candidate?.ProfitPerCraft ?? 0m);
    public decimal Investment => Crafts * (Candidate?.CostPerCraft ?? 0m);
    public decimal Revenue => Crafts * (Candidate?.RevenuePerCraft ?? 0m);
}

public sealed class FoodPlan
{
    public IReadOnlyList<FoodPlanEntry> Entries { get; init; } = [];
    public decimal FocusBudget { get; init; }
    public decimal MaxInvestment { get; init; }

    /// <summary>True when at least one food got fewer crafts because the investment limit was reached.</summary>
    public bool IsLimitedByInvestment { get; init; }

    public decimal TotalFocus => Entries.Sum(x => x.Focus);
    public decimal TotalProfit => Entries.Sum(x => x.Profit);
    public decimal TotalInvestment => Entries.Sum(x => x.Investment);
    public decimal TotalRevenue => Entries.Sum(x => x.Revenue);
    public int TotalItems => Entries.Sum(x => x.Items);
    public decimal RemainingFocus => Math.Max(0m, FocusBudget - TotalFocus);
    public decimal ProfitPerFocus => TotalFocus > 0m ? TotalProfit / TotalFocus : 0m;
    public decimal RemainingInvestment => MaxInvestment > 0m ? Math.Max(0m, MaxInvestment - TotalInvestment) : 0m;
}

public static class FoodPlanner
{
    /// <summary>Only profitable, valid foods that reach the minimum profit per focus.</summary>
    public static IEnumerable<FoodCandidate> GetEligible(IEnumerable<FoodCandidate> candidates, decimal minProfitPerFocus)
    {
        return (candidates ?? [])
            .Where(x => x is { IsValid: true }
                        && x.FocusPerCraft > 0m
                        && x.ProfitPerCraft > 0m
                        && x.ProfitPerFocus >= minProfitPerFocus)
            .OrderByDescending(x => x.ProfitPerFocus)
            .ThenByDescending(x => x.ProfitPerCraft);
    }

    /// <summary>Max crafts per day so we only sell (daily volume ÷ divisor) items.</summary>
    public static int GetMaxCrafts(FoodCandidate candidate, decimal dailyVolumeDivisor)
    {
        if (candidate == null || candidate.DailyVolume <= 0m)
        {
            return 0;
        }

        var divisor = Math.Max(1m, dailyVolumeDivisor);
        var maxItems = Math.Floor(candidate.DailyVolume / divisor);
        return (int) Math.Floor(maxItems / Math.Max(1, candidate.AmountCrafted));
    }

    /// <summary>
    /// Greedy: spend focus on the best profit per focus first, capped by the sales limit of each food
    /// and by the remaining investment budget (when set).
    /// </summary>
    public static FoodPlan Plan(IEnumerable<FoodCandidate> candidates, FoodPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var remainingFocus = Math.Max(0m, input.FocusBudget);
        var maxInvestment = Math.Max(0m, input.MaxInvestment);
        var remainingInvestment = maxInvestment;
        var isLimitedByInvestment = false;
        var entries = new List<FoodPlanEntry>();

        foreach (var candidate in GetEligible(candidates, input.MinProfitPerFocus))
        {
            if (remainingFocus < candidate.FocusPerCraft)
            {
                continue;
            }

            var maxCrafts = GetMaxCrafts(candidate, input.DailyVolumeDivisor);
            var affordableCrafts = (int) Math.Floor(remainingFocus / candidate.FocusPerCraft);
            var crafts = Math.Min(maxCrafts, affordableCrafts);

            if (maxInvestment > 0m && candidate.CostPerCraft > 0m)
            {
                var payableCrafts = (int) Math.Min(int.MaxValue, Math.Floor(remainingInvestment / candidate.CostPerCraft));
                if (payableCrafts < crafts)
                {
                    crafts = payableCrafts;
                    isLimitedByInvestment = true;
                }
            }

            if (crafts <= 0)
            {
                continue;
            }

            entries.Add(new FoodPlanEntry
            {
                Candidate = candidate,
                Crafts = crafts,
                MaxCrafts = maxCrafts
            });

            remainingFocus -= crafts * candidate.FocusPerCraft;
            remainingInvestment -= crafts * Math.Max(0m, candidate.CostPerCraft);
        }

        return new FoodPlan
        {
            Entries = entries,
            FocusBudget = Math.Max(0m, input.FocusBudget),
            MaxInvestment = maxInvestment,
            IsLimitedByInvestment = isLimitedByInvestment
        };
    }
}
