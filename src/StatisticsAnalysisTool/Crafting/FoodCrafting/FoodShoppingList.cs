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

    public int NetQuantityToBuy => (int) Math.Ceiling(NetQuantity);
    public int GrossQuantityToBuy => (int) Math.Ceiling(GrossQuantity);
    public decimal NetCost => NetQuantityToBuy * UnitPrice;
}

public static class FoodShoppingListBuilder
{
    public static IReadOnlyList<FoodShoppingItem> Build(
        FoodPlan plan,
        IReadOnlyDictionary<int, FishSauceRecipe> sauceRecipes,
        IReadOnlyDictionary<string, decimal> unitPrices,
        decimal sauceReturnRatePercent)
    {
        var totals = new Dictionary<string, (decimal Net, decimal Gross, decimal Price, bool ForSauce)>(StringComparer.OrdinalIgnoreCase);

        void Add(string uniqueName, decimal net, decimal gross, decimal price, bool forSauce)
        {
            if (string.IsNullOrWhiteSpace(uniqueName) || (net <= 0m && gross <= 0m))
            {
                return;
            }

            totals.TryGetValue(uniqueName, out var current);
            totals[uniqueName] = (current.Net + net, current.Gross + gross, price > 0m ? price : current.Price, current.ForSauce || forSauce);
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
                IsForFishSauce = x.Value.ForSauce
            })
            .OrderBy(x => x.IsForFishSauce)
            .ThenByDescending(x => x.NetCost)
            .ToList();
    }
}
