using Serilog;
using StatisticsAnalysisTool.Cluster;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Trade.Market;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
// The game sends the sell orders (price + amount) of an item when its market page is opened.
// They are kept here so the shopping list can show what buying the needed quantity really costs.

public sealed class FoodOrderBookOrder
{
    public long Id { get; set; }
    public long UnitPrice { get; set; }
    public int Amount { get; set; }
}

public sealed class FoodOrderBook
{
    public string UniqueName { get; set; }
    public MarketLocation Location { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<FoodOrderBookOrder> Orders { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public int TotalAmount => Orders?.Sum(x => Math.Max(0, x.Amount)) ?? 0;
    [System.Text.Json.Serialization.JsonIgnore]
    public long MinPrice => Orders is { Count: > 0 } ? Orders.Min(x => x.UnitPrice) : 0;
}

public sealed class FoodOrderBookCost
{
    public int RequestedQuantity { get; init; }
    public int FilledQuantity { get; init; }

    /// <summary>Cost of the quantity that is listed.</summary>
    public decimal FilledCost { get; init; }

    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }

    public bool IsEnoughListed => FilledQuantity >= RequestedQuantity;
    public int MissingQuantity => Math.Max(0, RequestedQuantity - FilledQuantity);

    /// <summary>Filled cost + the missing quantity at the highest listed price (it can only be more expensive).</summary>
    public decimal EstimatedTotalCost => FilledCost + MissingQuantity * MaxPrice;

    public decimal AverageUnitPrice => RequestedQuantity > 0 ? EstimatedTotalCost / RequestedQuantity : MinPrice;
}

public static class FoodOrderBookCalculator
{
    /// <summary>Buys from the cheapest order upwards until the quantity is reached.</summary>
    public static FoodOrderBookCost GetCost(FoodOrderBook book, int quantity)
    {
        var orders = (book?.Orders ?? [])
            .Where(x => x != null && x.Amount > 0 && x.UnitPrice > 0)
            .OrderBy(x => x.UnitPrice)
            .ToList();

        if (orders.Count == 0)
        {
            return new FoodOrderBookCost { RequestedQuantity = Math.Max(0, quantity) };
        }

        var remaining = Math.Max(0, quantity);
        var filled = 0;
        var cost = 0m;
        long maxUsedPrice = orders[0].UnitPrice;

        foreach (var order in orders)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, order.Amount);
            filled += take;
            cost += take * (decimal) order.UnitPrice;
            remaining -= take;
            maxUsedPrice = order.UnitPrice;
        }

        return new FoodOrderBookCost
        {
            RequestedQuantity = Math.Max(0, quantity),
            FilledQuantity = filled,
            FilledCost = cost,
            MinPrice = orders[0].UnitPrice,
            MaxPrice = remaining > 0 ? orders[^1].UnitPrice : maxUsedPrice
        };
    }
}

public sealed class FoodOrderBookStore
{
    /// <summary>Pages received within this time are merged into one order book (scrolling, re-opening).</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(5);

    /// <summary>Order books older than this are dropped when loading.</summary>
    public static readonly TimeSpan KeepTime = TimeSpan.FromDays(7);

    private readonly object _lock = new();
    private readonly Dictionary<(string UniqueName, MarketLocation Location), FoodOrderBook> _books = new();

    public static FoodOrderBookStore Shared { get; } = new();

    public event Action Changed;

    /// <summary>Hook for the market packet handler (<c>AuctionGetOffersResponseHandler</c>). Never throws.</summary>
    public static void CaptureFromGame(IEnumerable<AuctionEntry> offers)
    {
        try
        {
            var location = GetCurrentMarketLocation();
            if (location == MarketLocation.Unknown || offers == null)
            {
                return;
            }

            Shared.AddOffers(
                offers
                    .Where(x => x is { IsFinished: false } && !string.IsNullOrWhiteSpace(x.ItemTypeId))
                    .Select(x => (x.ItemTypeId, x.Id, FixPoint.FromInternalValue(x.UnitPriceSilver).IntegerValue, x.Amount)),
                location,
                DateTime.UtcNow);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Food crafting could not store market offers");
        }
    }

    private static MarketLocation GetCurrentMarketLocation()
    {
        var cluster = ClusterController.CurrentCluster;
        var location = (cluster?.Index ?? string.Empty).GetMarketLocationByLocationNameOrId();
        if (location == MarketLocation.Unknown && !string.IsNullOrEmpty(cluster?.SourceClusterIndex))
        {
            location = cluster.SourceClusterIndex.GetMarketLocationByLocationNameOrId();
        }

        return location;
    }

    public void AddOffers(IEnumerable<(string UniqueName, long Id, long UnitPrice, int Amount)> offers, MarketLocation location, DateTime nowUtc)
    {
        var changed = false;

        lock (_lock)
        {
            foreach (var group in offers.GroupBy(x => x.UniqueName, StringComparer.OrdinalIgnoreCase))
            {
                var key = (group.Key.ToUpperInvariant(), location);
                if (!_books.TryGetValue(key, out var book) || nowUtc - book.UpdatedUtc > MergeWindow)
                {
                    book = new FoodOrderBook { UniqueName = group.Key, Location = location };
                    _books[key] = book;
                }

                foreach (var offer in group)
                {
                    book.Orders.RemoveAll(x => x.Id == offer.Id);
                    if (offer.Amount > 0 && offer.UnitPrice > 0)
                    {
                        book.Orders.Add(new FoodOrderBookOrder { Id = offer.Id, UnitPrice = offer.UnitPrice, Amount = offer.Amount });
                    }
                }

                book.UpdatedUtc = nowUtc;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>The order book of an item in a city, or null when none or older than the max age (0 = no limit).</summary>
    public FoodOrderBook Get(string uniqueName, MarketLocation location, DateTime nowUtc, TimeSpan maxAge)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            return null;
        }

        lock (_lock)
        {
            if (!_books.TryGetValue((uniqueName.ToUpperInvariant(), location), out var book) || book.Orders.Count == 0)
            {
                return null;
            }

            if (maxAge > TimeSpan.Zero && nowUtc - book.UpdatedUtc > maxAge)
            {
                return null;
            }

            return Copy(book);
        }
    }

    public int Count(MarketLocation location, DateTime nowUtc, TimeSpan maxAge)
    {
        lock (_lock)
        {
            return _books.Values.Count(x => x.Location == location && x.Orders.Count > 0
                                             && (maxAge <= TimeSpan.Zero || nowUtc - x.UpdatedUtc <= maxAge));
        }
    }

    public List<FoodOrderBook> GetAll()
    {
        lock (_lock)
        {
            return _books.Values.Select(Copy).ToList();
        }
    }

    public void Load(IEnumerable<FoodOrderBook> books, DateTime nowUtc)
    {
        lock (_lock)
        {
            foreach (var book in books ?? [])
            {
                if (book == null || string.IsNullOrWhiteSpace(book.UniqueName) || nowUtc - book.UpdatedUtc > KeepTime)
                {
                    continue;
                }

                var key = (book.UniqueName.ToUpperInvariant(), book.Location);
                if (!_books.TryGetValue(key, out var existing) || existing.UpdatedUtc < book.UpdatedUtc)
                {
                    _books[key] = Copy(book);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _books.Clear();
        }
    }

    private static FoodOrderBook Copy(FoodOrderBook book)
    {
        return new FoodOrderBook
        {
            UniqueName = book.UniqueName,
            Location = book.Location,
            UpdatedUtc = book.UpdatedUtc,
            Orders = (book.Orders ?? []).Select(x => new FoodOrderBookOrder { Id = x.Id, UnitPrice = x.UnitPrice, Amount = x.Amount }).ToList()
        };
    }
}
