using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Models;
using StatisticsAnalysisTool.Models.ItemsJsonModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
/// <summary>Reads food and fish sauce recipes from the loaded game data.</summary>
public sealed class FoodRecipeCatalog
{
    private readonly CraftingRecipeResolver _recipeResolver = new();

    public CraftingRecipeResolver RecipeResolver => _recipeResolver;

    public static bool IsFood(Item item)
    {
        return item?.FullItemInformation is ConsumableItem consumableItem
               && string.Equals(consumableItem.CraftingCategory, "food", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetCategory(Item item)
    {
        return FoodCategories.Normalize(item?.FullItemInformation?.ShopSubCategory2);
    }

    /// <summary>All craftable food items including enchanted variants (e.g. T6_MEAL_SALAD@1).</summary>
    public List<Item> GetFoodItems()
    {
        return ItemController.Items
            .Where(x => IsFood(x) && _recipeResolver.IsCraftable(x))
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.UniqueName, StringComparer.Ordinal)
            .ToList();
    }

    public decimal GetBaseFocus(Item item)
    {
        var requirements = _recipeResolver.GetCraftingRequirements(item);
        return decimal.TryParse(requirements?.CraftingFocus, NumberStyles.Number, CultureInfo.InvariantCulture, out var focus)
            ? Math.Max(0m, focus)
            : 0m;
    }

    /// <summary>
    /// Same grouping as <see cref="CraftingRecipeResolver.GetResources"/>, but without loading icons
    /// (that would call the UI dispatcher for every resource of every food).
    /// </summary>
    public List<FoodRecipeResource> GetResources(Item item)
    {
        var requirements = _recipeResolver.GetCraftingRequirements(item);
        if (requirements?.CraftResource == null)
        {
            return [];
        }

        return requirements.CraftResource
            .Where(x => !string.IsNullOrWhiteSpace(x?.UniqueName))
            .GroupBy(x => (x.UniqueName, EnchantmentLevel: ParseEnchantmentLevel(x.EnchantmentLevel)))
            .Select(group =>
            {
                var resourceItem = CraftingRecipeResolver.GetSuitableResourceItem(group.Key.UniqueName, group.Key.EnchantmentLevel, item.Level);
                if (resourceItem == null)
                {
                    return null;
                }

                var returnPolicy = CraftingRecipeResolver.GetReturnPolicy(group);
                return new FoodRecipeResource
                {
                    UniqueName = resourceItem.UniqueName,
                    QuantityPerRun = group.Sum(x => x.Count),
                    UnitWeight = ItemController.GetWeight(resourceItem.FullItemInformation),
                    IsReturnable = returnPolicy.IsReturnable,
                    MaxReturnQuantityPerRun = returnPolicy.MaxReturnQuantityPerRun
                };
            })
            .Where(x => x != null)
            .ToList();
    }

    private static int? ParseEnchantmentLevel(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level > 0
            ? level
            : null;
    }

    public Dictionary<int, FishSauceRecipe> GetFishSauceRecipes()
    {
        var result = new Dictionary<int, FishSauceRecipe>();

        for (var level = 1; level <= 3; level++)
        {
            var uniqueName = FishSauceRecipe.GetUniqueName(level);
            var item = ItemController.GetItemByUniqueName(uniqueName);
            if (item?.FullItemInformation is not SimpleItem simpleItem)
            {
                continue;
            }

            var requirements = simpleItem.CraftingRequirements?.FirstOrDefault(x => x?.CraftResource?.Count > 0);
            if (requirements == null)
            {
                continue;
            }

            var amountCrafted = int.TryParse(requirements.AmountCrafted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) && amount > 0
                ? amount
                : 1;

            result[level] = new FishSauceRecipe
            {
                Level = level,
                UniqueName = uniqueName,
                AmountCrafted = amountCrafted,
                Ingredients = requirements.CraftResource
                    .Where(x => !string.IsNullOrWhiteSpace(x?.UniqueName) && x.Count > 0)
                    .GroupBy(x => x.UniqueName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new FishSauceIngredient { UniqueName = x.Key, Count = x.Sum(y => y.Count) })
                    .ToList()
            };
        }

        return result;
    }
}

public sealed class FoodRecipeResource
{
    public string UniqueName { get; init; }
    public decimal QuantityPerRun { get; init; }
    public double UnitWeight { get; init; }
    public bool IsReturnable { get; init; }
    public decimal? MaxReturnQuantityPerRun { get; init; }
}
