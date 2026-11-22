using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting order book tests. Fork-only file.
[TestFixture]
public class FoodOrderBookTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private static FoodOrderBook Book(params (long Price, int Amount)[] orders)
    {
        return new FoodOrderBook
        {
            UniqueName = "T4_ALCHEMY_RARE_ENT",
            Location = MarketLocation.BrecilienMarket,
            UpdatedUtc = Now,
            Orders = orders.Select((x, i) => new FoodOrderBookOrder { Id = i + 1, UnitPrice = x.Price, Amount = x.Amount }).ToList()
        };
    }

    [Test]
    public void GetCost_BuysCheapestOrdersFirst()
    {
        // 5 @ 50, then 10 @ 80, then 100 @ 120 → buying 20 = 5×50 + 10×80 + 5×120 = 1,650
        var cost = FoodOrderBookCalculator.GetCost(Book((120, 100), (50, 5), (80, 10)), 20);

        cost.FilledQuantity.Should().Be(20);
        cost.FilledCost.Should().Be(1650m);
        cost.IsEnoughListed.Should().BeTrue();
        cost.MinPrice.Should().Be(50m);
        cost.MaxPrice.Should().Be(120m);
        cost.AverageUnitPrice.Should().Be(82.5m);
    }

    [Test]
    public void GetCost_NotEnoughListed_PricesRestAtHighestOrder()
    {
        var cost = FoodOrderBookCalculator.GetCost(Book((50, 5), (80, 10)), 20);

        cost.FilledQuantity.Should().Be(15);
        cost.IsEnoughListed.Should().BeFalse();
        cost.MissingQuantity.Should().Be(5);
        cost.EstimatedTotalCost.Should().Be(5 * 50 + 10 * 80 + 5 * 80);
    }

    [Test]
    public void GetCost_EmptyBook_IsZero()
    {
        var cost = FoodOrderBookCalculator.GetCost(Book(), 10);

        cost.FilledQuantity.Should().Be(0);
        cost.EstimatedTotalCost.Should().Be(0m);
    }

    [Test]
    public void Store_MergesPagesWithinWindow_AndReplacesOldBook()
    {
        var store = new FoodOrderBookStore();
        store.AddOffers([("T4_ALCHEMY_RARE_ENT", 1L, 50L, 5)], MarketLocation.BrecilienMarket, Now);
        store.AddOffers([("t4_alchemy_rare_ent", 2L, 80L, 10), ("T4_ALCHEMY_RARE_ENT", 1L, 50L, 3)], MarketLocation.BrecilienMarket, Now.AddMinutes(1));

        var merged = store.Get("T4_ALCHEMY_RARE_ENT", MarketLocation.BrecilienMarket, Now.AddMinutes(1), TimeSpan.Zero);
        merged.Orders.Should().HaveCount(2);
        merged.TotalAmount.Should().Be(13);

        // Re-opened much later: old orders may be gone, so the book starts fresh.
        store.AddOffers([("T4_ALCHEMY_RARE_ENT", 3L, 90L, 7)], MarketLocation.BrecilienMarket, Now.AddMinutes(30));
        var fresh = store.Get("T4_ALCHEMY_RARE_ENT", MarketLocation.BrecilienMarket, Now.AddMinutes(30), TimeSpan.Zero);
        fresh.Orders.Should().ContainSingle().Which.UnitPrice.Should().Be(90);
    }

    [Test]
    public void Store_Get_RespectsCityAndMaxAge()
    {
        var store = new FoodOrderBookStore();
        store.AddOffers([("T4_ALCHEMY_RARE_ENT", 1L, 50L, 5)], MarketLocation.BrecilienMarket, Now);

        store.Get("T4_ALCHEMY_RARE_ENT", MarketLocation.LymhurstMarket, Now, TimeSpan.Zero).Should().BeNull();
        store.Get("T4_ALCHEMY_RARE_ENT", MarketLocation.BrecilienMarket, Now.AddHours(3), TimeSpan.FromHours(2)).Should().BeNull();
        store.Get("T4_ALCHEMY_RARE_ENT", MarketLocation.BrecilienMarket, Now.AddHours(1), TimeSpan.FromHours(2)).Should().NotBeNull();
    }

    [Test]
    public void Store_Load_DropsBooksOlderThanKeepTime()
    {
        var store = new FoodOrderBookStore();
        var old = Book((50, 5));
        old.UpdatedUtc = Now.AddDays(-8);
        var recent = Book((60, 5));
        recent.UniqueName = "T1_SEAWEED";

        store.Load([old, recent], Now);

        store.GetAll().Select(x => x.UniqueName).Should().Equal("T1_SEAWEED");
    }
}
