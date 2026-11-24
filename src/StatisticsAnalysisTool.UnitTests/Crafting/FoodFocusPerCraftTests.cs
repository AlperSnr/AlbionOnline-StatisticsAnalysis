using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Focus per craft = focus per item × amount crafted. Fork-only file.
[TestFixture]
public class FoodFocusPerCraftTests
{
    [Test]
    public void CabbageSoup_OneCraftOfTenCosts253()
    {
        // In-game: 25.3 focus per soup, one craft makes 10 soups → 253 (user's real batch used ~10x the old estimate).
        FoodFocusCalculator.GetFocusPerCraft(504m, 43140m, 10).Should().BeApproximately(253m, 0.5m);
    }

    [Test]
    public void SingleItemRecipe_IsUnchanged()
    {
        FoodFocusCalculator.GetFocusPerCraft(504m, 43140m, 1).Should().BeApproximately(25.3m, 0.05m);
    }

    [Test]
    public void ZeroAmount_CountsAsOne()
    {
        FoodFocusCalculator.GetFocusPerCraft(504m, 0m, 0).Should().Be(504m);
    }
}
