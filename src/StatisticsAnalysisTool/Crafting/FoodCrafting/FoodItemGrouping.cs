using System;
using System.Text.RegularExpressions;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
/// <summary>Groups items of the same type (T4_MILK, T5_MILK, ... → MILK) so lists show them together.</summary>
public static partial class FoodItemGrouping
{
    [GeneratedRegex(@"^T(\d+)_(.+?)(?:@(\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    /// <summary>Item type without tier and enchantment, e.g. "T6_MEAL_SALAD@1" → "MEAL_SALAD".</summary>
    public static string GetGroupKey(string uniqueName)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            return string.Empty;
        }

        var match = NameRegex().Match(uniqueName.Trim());
        return (match.Success ? match.Groups[2].Value : uniqueName.Trim()).ToUpperInvariant();
    }

    public static int GetTier(string uniqueName)
    {
        var match = NameRegex().Match(uniqueName?.Trim() ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, out var tier) ? tier : 0;
    }

    public static int GetEnchantment(string uniqueName)
    {
        var match = NameRegex().Match(uniqueName?.Trim() ?? string.Empty);
        return match.Success && match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var level) ? level : 0;
    }
}
