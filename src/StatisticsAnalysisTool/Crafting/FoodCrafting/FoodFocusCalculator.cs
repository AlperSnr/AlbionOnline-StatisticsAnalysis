using System;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public static class FoodFocusCalculator
{
    /// <summary>Every 10,000 focus cost efficiency halves the focus cost.</summary>
    public const decimal FceHalvingStep = 10000m;

    public static decimal GetFocusCostEfficiency(FoodDestinyBoard board, string category)
    {
        if (board == null)
        {
            return 0m;
        }

        var ownCategory = FoodCategories.Normalize(category);
        var mastery = Math.Clamp(board.MasteryLevel, 0, 100) * board.FcePerMasteryLevel;
        var own = 0m;
        var others = 0m;

        foreach (var specialization in board.Specializations ?? [])
        {
            if (specialization == null)
            {
                continue;
            }

            var level = Math.Clamp(specialization.Level, 0, 100);
            if (FoodCategories.Normalize(specialization.Category) == ownCategory)
            {
                own += level * board.FcePerOwnSpecializationLevel;
            }
            else
            {
                others += level * board.FcePerOtherSpecializationLevel;
            }
        }

        return Math.Max(0m, mastery + own + others);
    }

    /// <summary>Focus cost = base focus × 0.5^(FCE / 10,000).</summary>
    public static decimal GetFocusCost(decimal baseFocus, decimal focusCostEfficiency)
    {
        if (baseFocus <= 0m)
        {
            return 0m;
        }

        var multiplier = Math.Pow(0.5d, (double) (Math.Max(0m, focusCostEfficiency) / FceHalvingStep));
        return Math.Round(baseFocus * (decimal) multiplier, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Reverse calculation: which FCE gives the in-game focus cost? Used to check the formula.
    /// </summary>
    public static decimal GetImpliedFocusCostEfficiency(decimal baseFocus, decimal actualFocus)
    {
        if (baseFocus <= 0m || actualFocus <= 0m || actualFocus > baseFocus)
        {
            return 0m;
        }

        var value = Math.Log2((double) (baseFocus / actualFocus)) * (double) FceHalvingStep;
        return Math.Round((decimal) value, 0, MidpointRounding.AwayFromZero);
    }
}
