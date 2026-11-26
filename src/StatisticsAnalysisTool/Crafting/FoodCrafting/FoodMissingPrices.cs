using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public enum FoodPriceStatus
{
    Ok,

    /// <summary>The API has no sell order for this item in the city.</summary>
    NoData,

    /// <summary>There is a sell price, but it is older than the max price age.</summary>
    Outdated
}

public static class FoodPriceCheck
{
    public static FoodPriceStatus GetStatus(FoodMarketPrice price, DateTime nowUtc, int maxPriceAgeHours)
    {
        if (price == null || price.SellPriceMin == 0)
        {
            return FoodPriceStatus.NoData;
        }

        if (maxPriceAgeHours > 0 && (nowUtc - price.SellPriceMinDateUtc).TotalHours > maxPriceAgeHours)
        {
            return FoodPriceStatus.Outdated;
        }

        return FoodPriceStatus.Ok;
    }

    /// <summary>Status of the highest buy order (what you get when selling instantly).</summary>
    public static FoodPriceStatus GetBuyStatus(FoodMarketPrice price, DateTime nowUtc, int maxPriceAgeHours)
    {
        if (price == null || price.BuyPriceMax == 0)
        {
            return FoodPriceStatus.NoData;
        }

        if (maxPriceAgeHours > 0 && (nowUtc - price.BuyPriceMaxDateUtc).TotalHours > maxPriceAgeHours)
        {
            return FoodPriceStatus.Outdated;
        }

        return FoodPriceStatus.Ok;
    }

    /// <summary>Highest buy order prices that are present and not older than the max price age.</summary>
    public static Dictionary<string, decimal> GetUsableBuyPrices(
        IReadOnlyDictionary<string, FoodMarketPrice> prices,
        DateTime nowUtc,
        int maxPriceAgeHours)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (uniqueName, price) in prices ?? new Dictionary<string, FoodMarketPrice>())
        {
            if (GetBuyStatus(price, nowUtc, maxPriceAgeHours) == FoodPriceStatus.Ok)
            {
                result[uniqueName] = price.BuyPriceMax;
            }
        }

        return result;
    }

    /// <summary>Sell order prices that are present and not older than the max price age.</summary>
    public static Dictionary<string, decimal> GetUsablePrices(
        IReadOnlyDictionary<string, FoodMarketPrice> prices,
        DateTime nowUtc,
        int maxPriceAgeHours)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (prices == null)
        {
            return result;
        }

        foreach (var (uniqueName, price) in prices)
        {
            if (GetStatus(price, nowUtc, maxPriceAgeHours) == FoodPriceStatus.Ok)
            {
                result[uniqueName] = price.SellPriceMin;
            }
        }

        return result;
    }
}

/// <summary>One item whose price is missing or too old, with the foods it keeps out of the plan.</summary>
public sealed class FoodMissingPriceItem
{
    public string UniqueName { get; init; }
    public FoodPriceStatus Status { get; init; }

    /// <summary>Last known sell price (only for <see cref="FoodPriceStatus.Outdated"/>).</summary>
    public decimal LastSellPrice { get; init; }

    public DateTime LastSeenUtc { get; init; }
    public double AgeHours { get; init; }

    /// <summary>True when this is a food's own sell price (not an ingredient).</summary>
    public bool IsFood { get; init; }

    public IReadOnlyList<string> BlockedFoodNames { get; init; } = [];
    public int BlockedFoodCount => BlockedFoodNames.Count;

    public string StatusText => Status switch
    {
        FoodPriceStatus.NoData => "No price",
        FoodPriceStatus.Outdated => "Old: " + FormatAge(AgeHours),
        _ => "OK"
    };

    public static string FormatMinutes(double minutes)
    {
        return minutes < 90 ? $"{Math.Max(0, Math.Floor(minutes)):0} min ago" : FormatAge(minutes / 60d) + " ago";
    }

    public static string FormatAge(double hours)
    {
        if (hours < 48)
        {
            return $"{Math.Floor(hours):0} h";
        }

        return $"{Math.Floor(hours / 24):0} days";
    }
}

public static class FoodMissingPriceListBuilder
{
    /// <summary>
    /// Groups the missing price items of all skipped foods. Items blocking the most foods come first.
    /// </summary>
    public static List<FoodMissingPriceItem> Build(
        IEnumerable<FoodCandidate> candidates,
        IReadOnlyDictionary<string, FoodMarketPrice> prices,
        DateTime nowUtc,
        int maxPriceAgeHours,
        bool foodUsesBuyOrders = false)
    {
        var blocked = new Dictionary<string, (bool IsFood, List<string> Foods)>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates ?? [])
        {
            if (candidate?.MissingPriceItems == null)
            {
                continue;
            }

            var foodName = $"{candidate.Name} ({candidate.TierText})";
            foreach (var uniqueName in candidate.MissingPriceItems.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!blocked.TryGetValue(uniqueName, out var entry))
                {
                    entry = (false, new List<string>());
                    blocked[uniqueName] = entry;
                }

                var isFood = string.Equals(uniqueName, candidate.UniqueName, StringComparison.OrdinalIgnoreCase);
                entry.Foods.Add(foodName);
                blocked[uniqueName] = (entry.IsFood || isFood, entry.Foods);
            }
        }

        return blocked
            .Select(x =>
            {
                var price = prices?.GetValueOrDefault(x.Key);
                var useBuy = foodUsesBuyOrders && x.Value.IsFood;
                var status = useBuy
                    ? FoodPriceCheck.GetBuyStatus(price, nowUtc, maxPriceAgeHours)
                    : FoodPriceCheck.GetStatus(price, nowUtc, maxPriceAgeHours);
                if (status == FoodPriceStatus.Ok)
                {
                    // Can happen when the price is fine but a crafted fish sauce lists it; treat as no data.
                    status = FoodPriceStatus.NoData;
                }

                var lastPrice = price == null ? 0m : useBuy ? price.BuyPriceMax : price.SellPriceMin;
                var lastDate = price == null ? default : useBuy ? price.BuyPriceMaxDateUtc : price.SellPriceMinDateUtc;
                var hasDate = lastPrice > 0m;
                return new FoodMissingPriceItem
                {
                    UniqueName = x.Key,
                    Status = status,
                    LastSellPrice = hasDate ? lastPrice : 0m,
                    LastSeenUtc = hasDate ? lastDate : default,
                    AgeHours = hasDate ? Math.Max(0d, (nowUtc - lastDate).TotalHours) : 0d,
                    IsFood = x.Value.IsFood,
                    BlockedFoodNames = x.Value.Foods.OrderBy(name => name, StringComparer.CurrentCulture).ToList()
                };
            })
            .OrderByDescending(x => x.BlockedFoodCount)
            .ThenBy(x => x.IsFood)
            .ThenBy(x => x.UniqueName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
