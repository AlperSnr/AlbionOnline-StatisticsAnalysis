using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public sealed class FoodShoppingItem
{
    public string UniqueName { get; init; }

    /// <summary>Expected quantity to buy when returns are re-used.</summary>
    public decimal NetQuantity { get; init; }

    /// <summary>Quantity before any return (worst case, needed to start all crafts at once).</summary>
    public decimal GrossQuantity { get; init; }

    public decimal UnitPrice { get; init; }

    /// <summary>True when the item is an ingredient for self-crafted fish sauce.</summary>
    public bool IsForFishSauce { get; init; }

    /// <summary>
    /// Variance of the returned quantity. Returns are random, so with few crafts you can get less back than expected.
    /// Each unit is treated as returned with the return rate chance (binomial), which is on the safe side.
    /// </summary>
    public decimal ReturnVariance { get; init; }

    /// <summary>Extra standard deviations to buy on top of the expected quantity (0 = none, 2 ≈ 98% enough).</summary>
    public decimal SafetyMarginSigma { get; init; }

    /// <summary>Expected quantity + safety margin, never more than the quantity without any returns.</summary>
    public decimal SafeQuantity => Math.Min(
        GrossQuantity,
        NetQuantity + Math.Max(0m, SafetyMarginSigma) * (decimal) Math.Sqrt((double) Math.Max(0m, ReturnVariance)));

    public int NetQuantityToBuy => (int) Math.Ceiling(NetQuantity);
    public int GrossQuantityToBuy => (int) Math.Ceiling(GrossQuantity);
    public int SafeQuantityToBuy => Math.Max(NetQuantityToBuy, (int) Math.Ceiling(SafeQuantity));
    public int SafetyExtra => SafeQuantityToBuy - NetQuantityToBuy;
    public decimal SafeCost => SafeQuantityToBuy * UnitPrice;
    public decimal NetCost => NetQuantityToBuy * UnitPrice;
}

public static class FoodShoppingListBuilder
{
    public static IReadOnlyList<FoodShoppingItem> Build(
        FoodPlan plan,
        IReadOnlyDictionary<int, FishSauceRecipe> sauceRecipes,
        IReadOnlyDictionary<string, decimal> unitPrices,
        decimal sauceReturnRatePercent,
        decimal safetyMarginSigma = 0m)
    {
        var totals = new Dictionary<string, (decimal Net, decimal Gross, decimal Price, bool ForSauce, decimal Variance)>(StringComparer.OrdinalIgnoreCase);

        void Add(string uniqueName, decimal net, decimal gross, decimal price, bool forSauce)
        {
            if (string.IsNullOrWhiteSpace(uniqueName) || (net <= 0m && gross <= 0m))
            {
                return;
            }

            // Return chance implied by this line (net = gross × (1 − r)); variance of a binomial(gross, r).
            var returnRate = gross > 0m ? Math.Clamp(1m - net / gross, 0m, 1m) : 0m;
            var variance = gross * returnRate * (1m - returnRate);

            totals.TryGetValue(uniqueName, out var current);
            totals[uniqueName] = (current.Net + net, current.Gross + gross, price > 0m ? price : current.Price,
                current.ForSauce || forSauce, current.Variance + variance);
        }

        foreach (var entry in plan?.Entries ?? [])
        {
            foreach (var resource in entry.Candidate?.Resources ?? [])
            {
                var net = resource.NetQuantityPerCraft * entry.Crafts;
                var gross = resource.GrossQuantityPerCraft * entry.Crafts;

                if (resource.IsCraftedSauce
                    && sauceRecipes != null
                    && sauceRecipes.TryGetValue(resource.FishSauceLevel, out var recipe))
                {
                    // We need 'net' sauces in the long run, 'gross' sauces to start all crafts at once.
                    var netIngredients = FishSauceCalculator.GetIngredientsForSauces(recipe, net, sauceReturnRatePercent).ToList();
                    var grossIngredients = FishSauceCalculator.GetIngredientsForSauces(recipe, gross, 0m).ToDictionary(x => x.UniqueName, x => x.GrossQuantity);

                    foreach (var ingredient in netIngredients)
                    {
                        var price = unitPrices != null && unitPrices.TryGetValue(ingredient.UniqueName, out var p) ? p : 0m;
                        Add(ingredient.UniqueName, ingredient.NetQuantity, grossIngredients.GetValueOrDefault(ingredient.UniqueName), price, true);
                    }

                    continue;
                }

                Add(resource.UniqueName, net, gross, resource.UnitPrice, false);
            }
        }

        return totals
            .Select(x => new FoodShoppingItem
            {
                UniqueName = x.Key,
                NetQuantity = Math.Round(x.Value.Net, 4, MidpointRounding.AwayFromZero),
                GrossQuantity = Math.Round(x.Value.Gross, 4, MidpointRounding.AwayFromZero),
                UnitPrice = x.Value.Price,
                IsForFishSauce = x.Value.ForSauce,
                ReturnVariance = Math.Round(x.Value.Variance, 4, MidpointRounding.AwayFromZero),
                SafetyMarginSigma = Math.Max(0m, safetyMarginSigma)
            })
            .OrderBy(x => x.IsForFishSauce)
            .ThenByDescending(x => x.NetCost)
            .ToList();
    }
}
