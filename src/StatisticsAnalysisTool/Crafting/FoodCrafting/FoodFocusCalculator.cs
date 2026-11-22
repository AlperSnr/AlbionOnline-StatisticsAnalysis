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

        // Verified in-game: every specialization level gives 30 to all foods (incl. its own),
        // and the food's own specialization gives 250 extra on top (280 per level in total).
        var ownCategory = FoodCategories.Normalize(category);
        var total = Math.Clamp(board.MasteryLevel, 0, 100) * board.FcePerMasteryLevel;

        foreach (var specialization in board.Specializations ?? [])
        {
            if (specialization == null || !FoodCategories.IsSpecialization(specialization.Category))
            {
                continue;
            }

            var level = Math.Clamp(specialization.Level, 0, 100);
            total += level * board.FcePerOtherSpecializationLevel;

            if (FoodCategories.Normalize(specialization.Category) == ownCategory)
            {
                total += level * board.FceExtraPerOwnSpecializationLevel;
            }
        }

        return Math.Max(0m, total);
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
