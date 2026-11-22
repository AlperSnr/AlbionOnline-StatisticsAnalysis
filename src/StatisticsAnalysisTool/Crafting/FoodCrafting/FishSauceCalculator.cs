using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public sealed class FishSauceRecipe
{
    public int Level { get; init; }
    public string UniqueName { get; init; }
    public int AmountCrafted { get; init; } = 1;
    public IReadOnlyList<FishSauceIngredient> Ingredients { get; init; } = [];

    public static string GetUniqueName(int level) => $"T1_FISHSAUCE_LEVEL{level}";

    public static int GetLevel(string uniqueName)
    {
        const string prefix = "T1_FISHSAUCE_LEVEL";
        if (string.IsNullOrWhiteSpace(uniqueName) || !uniqueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(uniqueName[prefix.Length..], out var level) ? level : 0;
    }
}

public sealed class FishSauceIngredient
{
    public string UniqueName { get; init; }
    public int Count { get; init; }
}

public sealed class FishSauceCost
{
    public int Level { get; init; }
    public decimal MarketPrice { get; init; }
    public decimal CraftedCost { get; init; }
    public bool CanCraft { get; init; }
    public FishSauceMode Mode { get; init; }
    public bool UsesCrafting { get; init; }

    /// <summary>The price per sauce used in food calculations. 0 = unknown.</summary>
    public decimal EffectivePrice => UsesCrafting ? CraftedCost : MarketPrice;
}

public static class FishSauceCalculator
{
    /// <summary>
    /// Expected cost of one crafted sauce: ingredients × (1 − return rate) / amount crafted.
    /// Returns 0 when any ingredient price is unknown.
    /// </summary>
    public static decimal GetCraftedCost(FishSauceRecipe recipe, IReadOnlyDictionary<string, decimal> unitPrices, decimal returnRatePercent)
    {
        if (recipe?.Ingredients == null || recipe.Ingredients.Count == 0)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var ingredient in recipe.Ingredients)
        {
            if (unitPrices == null || !unitPrices.TryGetValue(ingredient.UniqueName, out var price) || price <= 0m)
            {
                return 0m;
            }

            total += ingredient.Count * price;
        }

        var returnRate = Math.Clamp(returnRatePercent, 0m, 100m) / 100m;
        return Math.Round(total * (1m - returnRate) / Math.Max(1, recipe.AmountCrafted), 2, MidpointRounding.AwayFromZero);
    }

    public static FishSauceCost Resolve(int level, FishSauceMode mode, decimal marketPrice, decimal craftedCost)
    {
        var canCraft = craftedCost > 0m;
        var hasMarketPrice = marketPrice > 0m;
        var usesCrafting = mode switch
        {
            FishSauceMode.Craft => canCraft,
            FishSauceMode.Buy => !hasMarketPrice && canCraft,
            _ => canCraft && (!hasMarketPrice || craftedCost < marketPrice)
        };

        return new FishSauceCost
        {
            Level = level,
            MarketPrice = marketPrice,
            CraftedCost = craftedCost,
            CanCraft = canCraft,
            Mode = mode,
            UsesCrafting = usesCrafting
        };
    }

    /// <summary>Ingredients needed for a number of crafted sauces, after expected returns.</summary>
    public static IEnumerable<(string UniqueName, decimal NetQuantity, decimal GrossQuantity)> GetIngredientsForSauces(
        FishSauceRecipe recipe,
        decimal sauceQuantity,
        decimal returnRatePercent)
    {
        if (recipe == null || sauceQuantity <= 0m)
        {
            return [];
        }

        var crafts = sauceQuantity / Math.Max(1, recipe.AmountCrafted);
        var returnRate = Math.Clamp(returnRatePercent, 0m, 100m) / 100m;

        return recipe.Ingredients
            .Select(x => (x.UniqueName, crafts * x.Count * (1m - returnRate), crafts * x.Count))
            .ToList();
    }
}
