using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public static class FoodCategories
{
    public const string Soups = "soups";
    public const string Salads = "salads";
    public const string Pies = "pies";
    public const string Omelettes = "omelettes";
    public const string Stews = "stews";
    public const string Sandwiches = "sandwiches";
    public const string Roasts = "roasts";
    public const string Ingredients = "ingredients";
    public const string Meat = "meat";

    /// <summary>
    /// Cooking specializations on the destiny board. Food items match them by @shopsubcategory2
    /// (soups, salads, ...). Ingredients and Meat have no food items in this tab, but their levels
    /// still add focus efficiency to every food.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string DisplayName)> Specializations =
    [
        (Soups, "Soups"),
        (Salads, "Salads"),
        (Pies, "Pies"),
        (Omelettes, "Omelettes"),
        (Stews, "Stews"),
        (Sandwiches, "Sandwiches"),
        (Roasts, "Roasts"),
        (Ingredients, "Ingredients"),
        (Meat, "Meat")
    ];

    /// <summary>Lower-case, trimmed category id (any value from items.json, not only specializations).</summary>
    public static string Normalize(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim().ToLowerInvariant();
    }

    public static bool IsSpecialization(string category)
    {
        var id = Normalize(category);
        return Specializations.Any(x => x.Id == id);
    }

    public static int GetSortIndex(string category)
    {
        var id = Normalize(category);
        for (var i = 0; i < Specializations.Count; i++)
        {
            if (Specializations[i].Id == id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    public static string GetDisplayName(string category)
    {
        var id = Normalize(category);
        foreach (var (specializationId, displayName) in Specializations)
        {
            if (string.Equals(specializationId, id, StringComparison.Ordinal))
            {
                return displayName;
            }
        }

        return string.IsNullOrEmpty(id) ? "Other" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id);
    }
}
