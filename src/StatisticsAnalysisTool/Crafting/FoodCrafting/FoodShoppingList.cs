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

    /// <summary>
    /// Part of the quantities that is for crafting fish sauce. Its safe amount is already decided by the
    /// fish sauce crafts (whole crafts of the safe number of sauces), so no extra margin is added on top.
    /// </summary>
    public decimal FishSauceNetQuantity { get; init; }

    public decimal FishSauceGrossQuantity { get; init; }
    public decimal FishSauceSafeQuantity { get; init; }

    /// <summary>
    /// Returns of the last craft come too late to be used, so that craft needs its full quantity.
    /// This is the part of one craft that comes back (gross - net of the biggest craft using the item).
    /// With a single craft this makes the need the full quantity without returns.
    /// </summary>
    public decimal LastCraftReturn { get; init; }

    /// <summary>Expected quantity when the returns of all crafts except the last are re-used.</summary>
    public decimal ExpectedQuantity => Math.Min(
                                           GrossQuantity - FishSauceGrossQuantity,
                                           NetQuantity - FishSauceNetQuantity + Math.Max(0m, LastCraftReturn))
                                       + FishSauceNetQuantity;

    /// <summary>Expected quantity + safety margin, never more than the quantity without any returns.</summary>
    public decimal SafeQuantity => Math.Min(
                                       GrossQuantity - FishSauceGrossQuantity,
                                       NetQuantity - FishSauceNetQuantity + Math.Max(0m, LastCraftReturn)
                                       + Math.Max(0m, SafetyMarginSigma) * (decimal) Math.Sqrt((double) Math.Max(0m, ReturnVariance)))
                                   + FishSauceSafeQuantity;

    public int NetQuantityToBuy => (int) Math.Ceiling(NetQuantity);
    public int GrossQuantityToBuy => (int) Math.Ceiling(GrossQuantity);
    public int ExpectedQuantityToBuy => Math.Max(NetQuantityToBuy, (int) Math.Ceiling(ExpectedQuantity));

    /// <summary>Extra because the last craft's returns can't be used.</summary>
    public int LastCraftExtra => ExpectedQuantityToBuy - NetQuantityToBuy;

    public int SafeQuantityToBuy => Math.Max(ExpectedQuantityToBuy, (int) Math.Ceiling(SafeQuantity));
    public int SafetyExtra => SafeQuantityToBuy - ExpectedQuantityToBuy;
    public decimal SafeCost => SafeQuantityToBuy * UnitPrice;

    /// <summary>Already owned (bank + entered by hand), at most the needed safe quantity.</summary>
    public int OwnedQuantity { get; init; }

    /// <summary>What is still to buy: safe quantity minus what you already own.</summary>
    public int QuantityToBuy => Math.Max(0, SafeQuantityToBuy - OwnedQuantity);

    public decimal ToBuyCost => QuantityToBuy * UnitPrice;
    public decimal NetCost => NetQuantityToBuy * UnitPrice;
}

/// <summary>How much of one fish sauce level to craft for the plan.</summary>
public sealed class FoodFishSauceCraft
{
    public int Level { get; init; }
    public string UniqueName { get; init; }

    /// <summary>Sauces used on average (food returns re-used).</summary>
    public decimal NetSauces { get; init; }

    /// <summary>Sauces needed when nothing comes back.</summary>
    public decimal GrossSauces { get; init; }

    /// <summary>Sauces needed: expected + safety margin, never more than <see cref="GrossSauces"/>.</summary>
    public int SaucesNeeded { get; init; }

    /// <summary>Sauces of this level you already own (bank + entered by hand), used before crafting new ones.</summary>
    public int OwnedSauces { get; init; }

    /// <summary>Sauces still to craft after using the owned ones.</summary>
    public int SaucesToCraft { get; init; }

    public int AmountCrafted { get; init; } = 1;
    public int Crafts { get; init; }

    /// <summary>Sauces you get from <see cref="Crafts"/> (can be a bit more than needed because of whole crafts).</summary>
    public int SaucesCrafted => Crafts * Math.Max(1, AmountCrafted);

    public IReadOnlyList<FoodFishSauceIngredientNeed> Ingredients { get; init; } = [];
}

public sealed class FoodFishSauceIngredientNeed
{
    public string UniqueName { get; init; }
    public int PerCraft { get; init; }
    public int Quantity { get; init; }
}

public sealed class FoodShoppingResult
{
    public IReadOnlyList<FoodShoppingItem> Items { get; init; } = [];
    public IReadOnlyList<FoodFishSauceCraft> FishSauceCrafts { get; init; } = [];
}

public static class FoodShoppingListBuilder
{
    public static IReadOnlyList<FoodShoppingItem> Build(
        FoodPlan plan,
        IReadOnlyDictionary<int, FishSauceRecipe> sauceRecipes,
        IReadOnlyDictionary<string, decimal> unitPrices,
        decimal sauceReturnRatePercent,
        decimal safetyMarginSigma = 0m,
        IReadOnlyDictionary<string, int> owned = null)
    {
        return BuildWithFishSauce(plan, sauceRecipes, unitPrices, sauceReturnRatePercent, safetyMarginSigma, owned).Items;
    }

    public static FoodShoppingResult BuildWithFishSauce(
        FoodPlan plan,
        IReadOnlyDictionary<int, FishSauceRecipe> sauceRecipes,
        IReadOnlyDictionary<string, decimal> unitPrices,
        decimal sauceReturnRatePercent,
        decimal safetyMarginSigma = 0m,
        IReadOnlyDictionary<string, int> owned = null)
    {
        var ownedByName = FoodOwnedItems.Combine(owned, null);
        int GetOwned(string uniqueName) => uniqueName != null && ownedByName.TryGetValue(uniqueName, out var quantity) ? quantity : 0;
        var sigma = Math.Max(0m, safetyMarginSigma);
        var totals = new Dictionary<string, ShoppingTotal>(StringComparer.OrdinalIgnoreCase);
        var sauceNeeds = new SortedDictionary<int, (decimal Net, decimal Gross, decimal Variance, decimal LastReturn, decimal LastVariance)>();

        ShoppingTotal Get(string uniqueName)
        {
            if (!totals.TryGetValue(uniqueName, out var total))
            {
                total = new ShoppingTotal();
                totals[uniqueName] = total;
            }

            return total;
        }

        foreach (var entry in plan?.Entries ?? [])
        {
            foreach (var resource in entry.Candidate?.Resources ?? [])
            {
                var net = resource.NetQuantityPerCraft * entry.Crafts;
                var gross = resource.GrossQuantityPerCraft * entry.Crafts;
                if (string.IsNullOrWhiteSpace(resource.UniqueName) || (net <= 0m && gross <= 0m))
                {
                    continue;
                }

                var variance = GetReturnVariance(net, gross);

                // The last craft's returns can't be used any more: keep the biggest single craft (on the safe side).
                var oneCraftReturn = Math.Max(0m, resource.GrossQuantityPerCraft - resource.NetQuantityPerCraft);
                var oneCraftVariance = GetReturnVariance(resource.NetQuantityPerCraft, resource.GrossQuantityPerCraft);

                if (resource.IsCraftedSauce && sauceRecipes != null && sauceRecipes.ContainsKey(resource.FishSauceLevel))
                {
                    sauceNeeds.TryGetValue(resource.FishSauceLevel, out var need);
                    var isBiggest = oneCraftReturn > need.LastReturn;
                    sauceNeeds[resource.FishSauceLevel] = (need.Net + net, need.Gross + gross, need.Variance + variance,
                        isBiggest ? oneCraftReturn : need.LastReturn, isBiggest ? oneCraftVariance : need.LastVariance);
                    continue;
                }

                var total = Get(resource.UniqueName);
                total.Net += net;
                total.Gross += gross;
                total.Variance += variance;
                if (oneCraftReturn > total.LastReturn)
                {
                    total.LastReturn = oneCraftReturn;
                    total.LastVariance = oneCraftVariance;
                }
                if (resource.UnitPrice > 0m)
                {
                    total.Price = resource.UnitPrice;
                }
            }
        }

        // Fish sauce: decide the safe number of sauces per level first, then buy the ingredients for whole crafts.
        var sauceReturnRate = Math.Clamp(sauceReturnRatePercent, 0m, 100m) / 100m;
        var sauceCrafts = new List<FoodFishSauceCraft>();

        foreach (var (level, need) in sauceNeeds)
        {
            var recipe = sauceRecipes[level];
            var sauceName = recipe.UniqueName ?? FishSauceRecipe.GetUniqueName(level);
            var amountCrafted = Math.Max(1, recipe.AmountCrafted);
            var expectedSauces = Math.Min(need.Gross, need.Net + need.LastReturn);
            var safeSauces = (int) Math.Ceiling(Math.Min(need.Gross, expectedSauces + sigma * Sqrt(need.Variance - need.LastVariance)));
            safeSauces = Math.Max(safeSauces, (int) Math.Ceiling(expectedSauces));
            var ownedSauces = Math.Min(GetOwned(sauceName), safeSauces);
            var saucesToCraft = safeSauces - ownedSauces;
            var crafts = (int) Math.Ceiling(saucesToCraft / (decimal) amountCrafted);

            // Owned sauces are used first, so fewer have to be crafted (expected and worst case).
            var netToCraft = Math.Max(0m, need.Net - ownedSauces);
            var grossToCraft = Math.Max(0m, need.Gross - ownedSauces);
            var ingredients = new List<FoodFishSauceIngredientNeed>();

            foreach (var ingredient in recipe.Ingredients)
            {
                var quantity = crafts * ingredient.Count;
                ingredients.Add(new FoodFishSauceIngredientNeed { UniqueName = ingredient.UniqueName, PerCraft = ingredient.Count, Quantity = quantity });

                if (quantity <= 0 && netToCraft <= 0m)
                {
                    continue;
                }

                var total = Get(ingredient.UniqueName);
                var sauceNet = netToCraft / amountCrafted * ingredient.Count * (1m - sauceReturnRate);
                var sauceGross = grossToCraft / amountCrafted * ingredient.Count;
                total.Net += sauceNet;
                total.Gross += sauceGross;
                total.SauceNet += sauceNet;
                total.SauceGross += sauceGross;
                total.SauceSafe += quantity;
                total.ForSauce = true;
                if (unitPrices != null && unitPrices.TryGetValue(ingredient.UniqueName, out var price) && price > 0m)
                {
                    total.Price = price;
                }
            }

            sauceCrafts.Add(new FoodFishSauceCraft
            {
                Level = level,
                UniqueName = sauceName,
                NetSauces = Math.Round(need.Net, 2, MidpointRounding.AwayFromZero),
                GrossSauces = Math.Round(need.Gross, 2, MidpointRounding.AwayFromZero),
                SaucesNeeded = safeSauces,
                OwnedSauces = ownedSauces,
                SaucesToCraft = saucesToCraft,
                AmountCrafted = amountCrafted,
                Crafts = crafts,
                Ingredients = ingredients
            });
        }

        var items = totals
            .Select(x => new FoodShoppingItem
            {
                OwnedQuantity = GetOwned(x.Key),
                UniqueName = x.Key,
                NetQuantity = Round(x.Value.Net),
                GrossQuantity = Round(x.Value.Gross),
                UnitPrice = x.Value.Price,
                IsForFishSauce = x.Value.ForSauce,
                ReturnVariance = Round(Math.Max(0m, x.Value.Variance - x.Value.LastVariance)),
                LastCraftReturn = Round(x.Value.LastReturn),
                SafetyMarginSigma = sigma,
                FishSauceNetQuantity = Round(x.Value.SauceNet),
                FishSauceGrossQuantity = Round(x.Value.SauceGross),
                FishSauceSafeQuantity = x.Value.SauceSafe
            })
            .Select(x => x.OwnedQuantity <= x.SafeQuantityToBuy ? x : CopyWithOwned(x, x.SafeQuantityToBuy))
            .ToList();

        // Same item types together (all milks, all fish...); the most expensive type first, then by tier.
        var groupCost = items
            .GroupBy(x => FoodItemGrouping.GetGroupKey(x.UniqueName))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.NetCost));
        items = items
            .OrderBy(x => x.IsForFishSauce)
            .ThenByDescending(x => groupCost[FoodItemGrouping.GetGroupKey(x.UniqueName)])
            .ThenBy(x => FoodItemGrouping.GetGroupKey(x.UniqueName), StringComparer.Ordinal)
            .ThenBy(x => FoodItemGrouping.GetTier(x.UniqueName))
            .ThenBy(x => FoodItemGrouping.GetEnchantment(x.UniqueName))
            .ToList();

        return new FoodShoppingResult { Items = items, FishSauceCrafts = sauceCrafts };
    }

    private static FoodShoppingItem CopyWithOwned(FoodShoppingItem item, int owned)
    {
        return new FoodShoppingItem
        {
            UniqueName = item.UniqueName,
            NetQuantity = item.NetQuantity,
            GrossQuantity = item.GrossQuantity,
            UnitPrice = item.UnitPrice,
            IsForFishSauce = item.IsForFishSauce,
            ReturnVariance = item.ReturnVariance,
            SafetyMarginSigma = item.SafetyMarginSigma,
            FishSauceNetQuantity = item.FishSauceNetQuantity,
            FishSauceGrossQuantity = item.FishSauceGrossQuantity,
            FishSauceSafeQuantity = item.FishSauceSafeQuantity,
            LastCraftReturn = item.LastCraftReturn,
            OwnedQuantity = owned
        };
    }

    /// <summary>Return chance implied by a line (net = gross × (1 − r)); variance of a binomial(gross, r).</summary>
    private static decimal GetReturnVariance(decimal net, decimal gross)
    {
        var returnRate = gross > 0m ? Math.Clamp(1m - net / gross, 0m, 1m) : 0m;
        return gross * returnRate * (1m - returnRate);
    }

    private static decimal Sqrt(decimal value) => (decimal) Math.Sqrt((double) Math.Max(0m, value));

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private sealed class ShoppingTotal
    {
        public decimal Net;
        public decimal Gross;
        public decimal Variance;
        public decimal Price;
        public bool ForSauce;
        public decimal SauceNet;
        public decimal SauceGross;
        public decimal SauceSafe;
        public decimal LastReturn;
        public decimal LastVariance;
    }
}
