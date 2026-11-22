using StatisticsAnalysisTool.Common;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file. Persisted as FoodCrafting.json in the user data folder.
public class FoodCraftingSettings
{
    public MarketLocation City { get; set; } = MarketLocation.BrecilienMarket;
    public decimal DailyBonusPercent { get; set; }
    public decimal StationFeePerHundredNutrition { get; set; }
    public decimal SalesTaxPercent { get; set; } = 4m;
    public decimal SetupFeePercent { get; set; } = 2.5m;

    public decimal FocusBudget { get; set; } = 10000m;
    public decimal DailyVolumeDivisor { get; set; } = 10m;
    public decimal MinProfitPerFocus { get; set; }
    public int MaxPriceAgeHours { get; set; } = 24;
    public int VolumeDays { get; set; } = 7;

    public bool IncludeEnchantment0 { get; set; } = true;
    public bool IncludeEnchantment1 { get; set; } = true;
    public bool IncludeEnchantment2 { get; set; } = true;
    public bool IncludeEnchantment3 { get; set; } = true;

    public FishSauceMode FishSauceLevel1Mode { get; set; } = FishSauceMode.Cheapest;
    public FishSauceMode FishSauceLevel2Mode { get; set; } = FishSauceMode.Cheapest;
    public FishSauceMode FishSauceLevel3Mode { get; set; } = FishSauceMode.Cheapest;

    public FoodDestinyBoard DestinyBoard { get; set; } = new();

    public bool IsEnchantmentIncluded(int level)
    {
        return level switch
        {
            0 => IncludeEnchantment0,
            1 => IncludeEnchantment1,
            2 => IncludeEnchantment2,
            3 => IncludeEnchantment3,
            _ => false
        };
    }

    public FishSauceMode GetFishSauceMode(int level)
    {
        return level switch
        {
            1 => FishSauceLevel1Mode,
            2 => FishSauceLevel2Mode,
            3 => FishSauceLevel3Mode,
            _ => FishSauceMode.Buy
        };
    }

    public void SetFishSauceMode(int level, FishSauceMode mode)
    {
        switch (level)
        {
            case 1:
                FishSauceLevel1Mode = mode;
                break;
            case 2:
                FishSauceLevel2Mode = mode;
                break;
            case 3:
                FishSauceLevel3Mode = mode;
                break;
        }
    }
}

public class FoodDestinyBoard
{
    /// <summary>Cooking mastery level (0-100).</summary>
    public int MasteryLevel { get; set; }

    /// <summary>One entry per food category (0-100).</summary>
    public List<FoodSpecializationLevel> Specializations { get; set; } = [];

    /// <summary>Focus cost efficiency per mastery level (verified in-game).</summary>
    public decimal FcePerMasteryLevel { get; set; } = 30m;

    /// <summary>
    /// Extra focus cost efficiency per level of the crafted food's own specialization,
    /// on top of <see cref="FcePerOtherSpecializationLevel"/> (own spec = 250 + 30 = 280 per level, verified in-game).
    /// </summary>
    public decimal FceExtraPerOwnSpecializationLevel { get; set; } = 250m;

    /// <summary>Focus cost efficiency every specialization level gives to all foods, including its own (verified in-game).</summary>
    public decimal FcePerOtherSpecializationLevel { get; set; } = 30m;

    public int GetSpecializationLevel(string category)
    {
        var id = FoodCategories.Normalize(category);
        return Specializations?.FirstOrDefault(x => FoodCategories.Normalize(x.Category) == id)?.Level ?? 0;
    }

    /// <summary>
    /// Makes sure every cooking specialization has exactly one entry and removes unknown ones
    /// (e.g. "grilledfish"/"other" saved by older versions of this tab).
    /// </summary>
    public void EnsureAllCategories()
    {
        Specializations ??= [];

        foreach (var (id, _) in FoodCategories.Specializations)
        {
            if (Specializations.All(x => FoodCategories.Normalize(x?.Category) != id))
            {
                Specializations.Add(new FoodSpecializationLevel { Category = id });
            }
        }

        Specializations = Specializations
            .Where(x => x != null && FoodCategories.IsSpecialization(x.Category))
            .GroupBy(x => FoodCategories.Normalize(x.Category))
            .Select(x => new FoodSpecializationLevel { Category = x.Key, Level = x.First().Level })
            .OrderBy(x => FoodCategories.GetSortIndex(x.Category))
            .ToList();
    }
}

public class FoodSpecializationLevel
{
    public string Category { get; set; }
    public int Level { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName => FoodCategories.GetDisplayName(Category);
}

public enum FishSauceMode
{
    Cheapest,
    Buy,
    Craft
}
