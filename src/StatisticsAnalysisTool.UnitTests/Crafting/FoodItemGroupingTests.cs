using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting: lists keep same item types together. Fork-only file.
[TestFixture]
public class FoodItemGroupingTests
{
    [TestCase("T4_MILK", "MILK", 4, 0)]
    [TestCase("T6_MEAL_SALAD@1", "MEAL_SALAD", 6, 1)]
    [TestCase("t1_fishsauce_level2", "FISHSAUCE_LEVEL2", 1, 0)]
    [TestCase("QUESTITEM", "QUESTITEM", 0, 0)]
    public void ParsesTypeTierAndEnchantment(string name, string key, int tier, int enchantment)
    {
        FoodItemGrouping.GetGroupKey(name).Should().Be(key);
        FoodItemGrouping.GetTier(name).Should().Be(tier);
        FoodItemGrouping.GetEnchantment(name).Should().Be(enchantment);
    }

    private static FoodPlanEntry Entry(string food, int crafts, params (string Name, decimal Quantity, decimal Price)[] resources) => new()
    {
        Crafts = crafts,
        Candidate = new FoodCandidate
        {
            UniqueName = food,
            AmountCrafted = 10,
            ProfitPerCraft = 100m,
            Resources = resources
                .Select(x => new FoodCandidateResource { UniqueName = x.Name, GrossQuantityPerCraft = x.Quantity, NetQuantityPerCraft = x.Quantity, UnitPrice = x.Price })
                .ToList()
        }
    };

    [Test]
    public void ShoppingList_KeepsSameTypesTogether_ByTier()
    {
        var plan = new FoodPlan
        {
            Entries =
            [
                Entry("T6_MEAL_A", 1, ("T6_MILK", 10m, 100m), ("T5_CABBAGE", 10m, 500m)),
                Entry("T4_MEAL_B", 1, ("T4_MILK", 10m, 100m), ("T7_CABBAGE", 10m, 1m))
            ]
        };

        var names = FoodShoppingListBuilder.Build(plan, null, null, 0m).Select(x => x.UniqueName).ToList();

        // Cabbage costs more in total (5010) than milk (2000), so cabbages come first.
        names.Should().Equal("T5_CABBAGE", "T7_CABBAGE", "T4_MILK", "T6_MILK");
    }

    [Test]
    public void PlanRows_KeepSameFoodTypesTogether_ByTier()
    {
        var entries = new[]
        {
            Entry("T5_MEAL_SOUP", 1),
            Entry("T6_MEAL_SALAD", 5),
            Entry("T3_MEAL_SOUP", 1),
            Entry("T6_MEAL_SALAD@1", 1)
        };

        var names = FoodPlanRowOrder.Sort(entries).Select(x => x.Candidate.UniqueName).ToList();

        names.Should().Equal("T6_MEAL_SALAD", "T6_MEAL_SALAD@1", "T3_MEAL_SOUP", "T5_MEAL_SOUP");
    }
}
