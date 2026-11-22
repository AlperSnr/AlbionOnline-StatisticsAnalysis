using System;
using System.Collections.Generic;

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
    public const string GrilledFish = "grilledfish";
    public const string Other = "other";

    /// <summary>
    /// Food categories as found in items.json (@shopsubcategory2 of food items).
    /// Each one is treated as a destiny board specialization.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string DisplayName)> All =
    [
        (Soups, "Soups"),
        (Salads, "Salads"),
        (Pies, "Pies"),
        (Omelettes, "Omelettes"),
        (Stews, "Stews"),
        (Sandwiches, "Sandwiches"),
        (Roasts, "Roasts"),
        (GrilledFish, "Grilled Fish"),
        (Other, "Other")
    ];

    public static string Normalize(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Other;
        }

        var value = category.Trim().ToLowerInvariant();
        foreach (var (id, _) in All)
        {
            if (string.Equals(id, value, StringComparison.Ordinal))
            {
                return id;
            }
        }

        return Other;
    }

    public static string GetDisplayName(string category)
    {
        var id = Normalize(category);
        foreach (var (categoryId, displayName) in All)
        {
            if (categoryId == id)
            {
                return displayName;
            }
        }

        return category ?? string.Empty;
    }
}
