using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.GameFileData;
using StatisticsAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public sealed class FoodEvaluationContext
{
    public FoodCraftingSettings Settings { get; init; }

    /// <summary>Usable unit prices (sell order min) per item. Stale/missing prices are not in here.</summary>
    public IReadOnlyDictionary<string, decimal> UnitPrices { get; init; }

    public IReadOnlyDictionary<string, decimal> DailyVolumes { get; init; }

    /// <summary>
    /// Price the food is sold at. Instant sell: highest buy order (or average over the planned quantity).
    /// Null = use <see cref="UnitPrices"/> (lowest sell order).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> FoodSellPrices { get; init; }

    /// <summary>Resolved fish sauce price per level (1-3).</summary>
    public IReadOnlyDictionary<int, FishSauceCost> FishSauceCosts { get; init; }

    /// <summary>
    /// Per sauce level: the items whose missing price makes the sauce price unknown
    /// (the sauce itself and/or its ingredients, depending on buy/craft mode).
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> FishSauceMissingItems { get; init; }
}

/// <summary>Evaluates one food recipe for one craft run, reusing the existing crafting calculator.</summary>
public sealed class FoodCandidateEvaluator
{
    /// <summary>Cluster ids of the city crafting locations (craftingmodifiers.json).</summary>
    public static readonly IReadOnlyDictionary<MarketLocation, string> CityClusterIds = new Dictionary<MarketLocation, string>
    {
        [MarketLocation.ThetfordMarket] = "0000",
        [MarketLocation.LymhurstMarket] = "1000",
        [MarketLocation.BridgewatchMarket] = "2000",
        [MarketLocation.MartlockMarket] = "3004",
        [MarketLocation.FortSterlingMarket] = "4000",
        [MarketLocation.CaerleonMarket] = "3003",
        [MarketLocation.BrecilienMarket] = "5000"
    };

    private const decimal FallbackCityBonusPercent = 18m;

    private readonly CraftingCalculator _calculator = new();
    private readonly CraftingStationFeeService _stationFeeService = new();
    private readonly FoodRecipeCatalog _catalog;
    private readonly Dictionary<string, decimal> _bonusCache = new(StringComparer.OrdinalIgnoreCase);

    public FoodCandidateEvaluator(FoodRecipeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>City production bonus (base + matching modifier) for an item, without daily bonus or focus.</summary>
    public decimal GetCityBonusPercent(Item item, MarketLocation city)
    {
        var cacheKey = city + "|" + (item?.FullItemInformation?.ShopCategory ?? string.Empty)
                       + "|" + (item?.FullItemInformation?.ShopSubCategory1 ?? string.Empty)
                       + "|" + (item?.FullItemInformation?.ShopSubCategory2 ?? string.Empty);

        if (_bonusCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var bonus = FallbackCityBonusPercent;
        if (CityClusterIds.TryGetValue(city, out var clusterId))
        {
            var location = CraftingLocationData.GetCraftingLocations(item)
                .FirstOrDefault(x => string.Equals(x.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase));
            if (location != null)
            {
                bonus = location.TotalProductionBonusPercent;
            }
        }

        _bonusCache[cacheKey] = bonus;
        return bonus;
    }

    public decimal GetFoodReturnRatePercent(Item item, FoodCraftingSettings settings)
    {
        var bonus = GetCityBonusPercent(item, settings.City) + Math.Max(0m, settings.DailyBonusPercent);
        return CraftingLocationData.GetExpectedReturnRatePercent(bonus, true);
    }

    /// <summary>
    /// Verified in-game: T1 and T2 recipes cost no station fee. Fish sauce is T1, so it is free to craft
    /// (its crafted cost never includes a fee).
    /// </summary>
    public static bool HasStationFee(int tier) => tier > 2;

    /// <summary>Verified in-game: crafting fish sauce has no focus option and returns nothing (0% RRR) in every city.</summary>
    public const decimal FishSauceReturnRatePercent = 0m;

    public FoodCandidate Evaluate(Item item, FoodEvaluationContext context)
    {
        var settings = context.Settings;
        var category = FoodRecipeCatalog.GetCategory(item);
        var amountCrafted = _catalog.RecipeResolver.GetAmountCrafted(item);
        var baseFocus = _catalog.GetBaseFocus(item);
        var fce = FoodFocusCalculator.GetFocusCostEfficiency(settings.DestinyBoard, category);
        var focusPerCraft = FoodFocusCalculator.GetFocusPerCraft(baseFocus, fce, amountCrafted);
        var returnRate = GetFoodReturnRatePercent(item, settings);
        var sellPrice = (context.FoodSellPrices ?? context.UnitPrices).GetValueOrDefault(item.UniqueName);
        var dailyVolume = context.DailyVolumes.GetValueOrDefault(item.UniqueName);

        var invalidReason = string.Empty;
        var missingPriceItems = new List<string>();
        if (sellPrice <= 0m)
        {
            invalidReason = settings.IsInstantSell ? "No recent buy order" : "No recent sell price";
            missingPriceItems.Add(item.UniqueName);
        }

        var resourceInputs = new List<CraftingResourceInput>();
        var sauceInfo = new Dictionary<string, (int Level, bool Crafted)>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in _catalog.GetResources(item))
        {
            var sauceLevel = FishSauceRecipe.GetLevel(resource.UniqueName);
            decimal unitPrice;
            var isCraftedSauce = false;

            if (sauceLevel > 0 && context.FishSauceCosts != null && context.FishSauceCosts.TryGetValue(sauceLevel, out var sauceCost))
            {
                unitPrice = sauceCost.EffectivePrice;
                isCraftedSauce = sauceCost.UsesCrafting;
            }
            else
            {
                unitPrice = context.UnitPrices.GetValueOrDefault(resource.UniqueName);
            }

            if (unitPrice <= 0m)
            {
                if (string.IsNullOrEmpty(invalidReason))
                {
                    invalidReason = "No price: " + (ItemController.GetItemByUniqueName(resource.UniqueName)?.LocalizedName ?? resource.UniqueName);
                }

                if (sauceLevel > 0 && context.FishSauceMissingItems != null
                                   && context.FishSauceMissingItems.TryGetValue(sauceLevel, out var sauceMissing)
                                   && sauceMissing.Count > 0)
                {
                    missingPriceItems.AddRange(sauceMissing);
                }
                else
                {
                    missingPriceItems.Add(resource.UniqueName);
                }
            }

            if (sauceLevel > 0)
            {
                sauceInfo[resource.UniqueName] = (sauceLevel, isCraftedSauce);
            }

            resourceInputs.Add(new CraftingResourceInput
            {
                UniqueName = resource.UniqueName,
                QuantityPerRun = resource.QuantityPerRun,
                UnitPrice = unitPrice,
                UnitWeight = resource.UnitWeight,
                IsReturnable = resource.IsReturnable,
                MaxReturnQuantityPerRun = resource.MaxReturnQuantityPerRun,
                ResourceKind = sauceLevel > 0 ? CraftingResourceKind.Special : CraftingResourceKind.Standard
            });
        }

        if (focusPerCraft <= 0m && string.IsNullOrEmpty(invalidReason))
        {
            invalidReason = "No focus cost in game data";
        }

        var result = _calculator.Calculate(new CraftingCalculationInput
        {
            ItemUniqueName = item.UniqueName,
            CraftingRuns = 1,
            AmountCrafted = amountCrafted,
            ReturnRatePercent = returnRate,
            UsesFocus = true,
            OutputUnitPrice = sellPrice,
            StationFee = settings.StationFeePerHundredNutrition,
            NutritionConsumedPerRun = HasStationFee(item.Tier) ? _stationFeeService.GetNutritionConsumedPerRun(item) : 0m,
            SalesTaxPercent = settings.SalesTaxPercent,
            SetupFeePercent = settings.IsInstantSell ? 0m : settings.SetupFeePercent,
            OutputUnitWeight = ItemController.GetWeight(item.FullItemInformation),
            Resources = resourceInputs
        });

        return new FoodCandidate
        {
            UniqueName = item.UniqueName,
            Name = item.LocalizedName,
            Category = category,
            Tier = item.Tier,
            EnchantmentLevel = item.Level,
            AmountCrafted = amountCrafted,
            SellPrice = sellPrice,
            ReturnRatePercent = returnRate,
            BaseFocusPerCraft = baseFocus * Math.Max(1, amountCrafted),
            FocusCostEfficiency = fce,
            FocusPerCraft = focusPerCraft,
            CostPerCraft = result.TotalCosts,
            RevenuePerCraft = result.SalesRevenueNet,
            ProfitPerCraft = result.Profit,
            RoiPercent = result.RoiPercent,
            DailyVolume = dailyVolume,
            InvalidReason = invalidReason,
            MissingPriceItems = missingPriceItems.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Resources = result.Resources
                .Select(x => new FoodCandidateResource
                {
                    UniqueName = x.UniqueName,
                    GrossQuantityPerCraft = x.GrossQuantity,
                    NetQuantityPerCraft = x.NetQuantity,
                    UnitPrice = x.UnitPrice,
                    FishSauceLevel = sauceInfo.TryGetValue(x.UniqueName ?? string.Empty, out var info) ? info.Level : 0,
                    IsCraftedSauce = sauceInfo.TryGetValue(x.UniqueName ?? string.Empty, out var info2) && info2.Crafted
                })
                .ToList()
        };
    }
}
