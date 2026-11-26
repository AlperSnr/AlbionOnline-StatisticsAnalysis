using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting: instant sell to buy orders. Fork-only file.
[TestFixture]
public class FoodInstantSellTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static FoodOrderBook BuyBook(params (long Price, int Amount)[] orders) => new()
    {
        UniqueName = "T7_MEAL_PIE",
        Location = MarketLocation.BrecilienMarket,
        IsBuyOrders = true,
        UpdatedUtc = Now,
        Orders = orders.Select((x, i) => new FoodOrderBookOrder { Id = i + 1, UnitPrice = x.Price, Amount = x.Amount }).ToList()
    };

    [Test]
    public void GetSale_SellsToHighestBuyOrdersFirst()
    {
        // 10 @ 900, 20 @ 850, 100 @ 700 → selling 40 = 10×900 + 20×850 + 10×700 = 33,000
        var sale = FoodOrderBookCalculator.GetSale(BuyBook((700, 100), (900, 10), (850, 20)), 40);

        sale.FilledQuantity.Should().Be(40);
        sale.FilledRevenue.Should().Be(33000m);
        sale.BestPrice.Should().Be(900m);
        sale.WorstPrice.Should().Be(700m);
        sale.AverageUnitPrice.Should().Be(825m);
        sale.IsEnoughListed.Should().BeTrue();
    }

    [Test]
    public void GetSale_NotEnoughBuyOrders_IsMarkedShort()
    {
        var sale = FoodOrderBookCalculator.GetSale(BuyBook((900, 10)), 25);

        sale.FilledQuantity.Should().Be(10);
        sale.IsEnoughListed.Should().BeFalse();
        sale.MissingQuantity.Should().Be(15);
    }

    [Test]
    public void BuyBook_BestPriceIsHighest()
    {
        var book = BuyBook((700, 1), (900, 1));

        book.BestPrice.Should().Be(900);
        book.MinPrice.Should().Be(700);
    }

    [Test]
    public void Store_KeepsBuyAndSellOrdersApart()
    {
        var store = new FoodOrderBookStore();
        store.AddOffers([("T7_MEAL_PIE", 1L, 1000L, 5)], MarketLocation.BrecilienMarket, Now);
        store.AddOffers([("T7_MEAL_PIE", 2L, 800L, 7)], MarketLocation.BrecilienMarket, Now, isBuyOrders: true);

        store.Get("T7_MEAL_PIE", MarketLocation.BrecilienMarket, Now, TimeSpan.Zero).Orders.Single().UnitPrice.Should().Be(1000);
        var buy = store.Get("T7_MEAL_PIE", MarketLocation.BrecilienMarket, Now, TimeSpan.Zero, isBuyOrders: true);
        buy.IsBuyOrders.Should().BeTrue();
        buy.Orders.Single().UnitPrice.Should().Be(800);
        store.Count(MarketLocation.BrecilienMarket, Now, TimeSpan.Zero, isBuyOrders: true).Should().Be(1);
    }

    [Test]
    public void BuyPriceStatus_UsesBuyPriceAndDate()
    {
        var fresh = new FoodMarketPrice { SellPriceMin = 0, BuyPriceMax = 800, BuyPriceMaxDateUtc = Now.AddHours(-1) };
        var old = new FoodMarketPrice { SellPriceMin = 1000, SellPriceMinDateUtc = Now, BuyPriceMax = 800, BuyPriceMaxDateUtc = Now.AddDays(-3) };

        FoodPriceCheck.GetBuyStatus(fresh, Now, 24).Should().Be(FoodPriceStatus.Ok);
        FoodPriceCheck.GetBuyStatus(old, Now, 24).Should().Be(FoodPriceStatus.Outdated);
        FoodPriceCheck.GetBuyStatus(null, Now, 24).Should().Be(FoodPriceStatus.NoData);
        FoodPriceCheck.GetUsableBuyPrices(new Dictionary<string, FoodMarketPrice> { ["A"] = fresh, ["B"] = old }, Now, 24)
            .Should().ContainKey("A").And.NotContainKey("B");
    }

    [Test]
    public void MissingPrices_InstantSell_UsesFoodBuyPrice()
    {
        var prices = new Dictionary<string, FoodMarketPrice>
        {
            ["T7_MEAL_PIE"] = new() { SellPriceMin = 1000, SellPriceMinDateUtc = Now, BuyPriceMax = 0 }
        };
        var candidates = new[] { new FoodCandidate { UniqueName = "T7_MEAL_PIE", Name = "Pie", Tier = 7, InvalidReason = "x", MissingPriceItems = ["T7_MEAL_PIE"] } };

        var list = FoodMissingPriceListBuilder.Build(candidates, prices, Now, 24, foodUsesBuyOrders: true);

        list.Single().Status.Should().Be(FoodPriceStatus.NoData);
        list.Single().IsFood.Should().BeTrue();
    }

    [Test]
    public void SellModeDefaultsToSellOrder()
    {
        new FoodCraftingSettings().IsInstantSell.Should().BeFalse();
        new FoodCraftingSettings { SellMode = FoodSellMode.InstantSell }.IsInstantSell.Should().BeTrue();
    }
}
