using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting: fish sauce crafts per level. Fork-only file.
[TestFixture]
public class FoodFishSauceCraftTests
{
    private static FishSauceRecipe Recipe(int level, int fish, int seaweed) => new()
    {
        Level = level,
        UniqueName = FishSauceRecipe.GetUniqueName(level),
        Ingredients =
        [
            new FishSauceIngredient { UniqueName = "T1_FISHCHOPS", Count = fish },
            new FishSauceIngredient { UniqueName = "T1_SEAWEED", Count = seaweed }
        ]
    };

    private static readonly Dictionary<int, FishSauceRecipe> Recipes = new()
    {
        [1] = Recipe(1, 15, 1),
        [2] = Recipe(2, 45, 3),
        [3] = Recipe(3, 135, 9)
    };

    private static FoodPlanEntry Entry(string name, int crafts, int sauceLevel, decimal grossSauce, decimal netSauce) => new()
    {
        Crafts = crafts,
        Candidate = new FoodCandidate
        {
            UniqueName = name,
            AmountCrafted = 10,
            Resources =
            [
                new FoodCandidateResource
                {
                    UniqueName = FishSauceRecipe.GetUniqueName(sauceLevel), GrossQuantityPerCraft = grossSauce, NetQuantityPerCraft = netSauce,
                    FishSauceLevel = sauceLevel, IsCraftedSauce = true
                }
            ]
        }
    };

    [Test]
    public void SauceCrafts_ArePerLevel_WithIngredientsPerLevel()
    {
        var plan = new FoodPlan
        {
            Entries =
            [
                Entry("A@1", 2, 1, 90m, 50m),
                Entry("B@1", 1, 1, 90m, 50m),
                Entry("C@2", 1, 2, 90m, 50m)
            ]
        };

        var result = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 0m);

        result.FishSauceCrafts.Should().HaveCount(2);
        var level1 = result.FishSauceCrafts.Single(x => x.Level == 1);
        level1.NetSauces.Should().Be(150m);
        level1.Crafts.Should().Be(150);
        level1.Ingredients.Single(x => x.UniqueName == "T1_FISHCHOPS").Quantity.Should().Be(2250);
        level1.Ingredients.Single(x => x.UniqueName == "T1_SEAWEED").Quantity.Should().Be(150);

        var level2 = result.FishSauceCrafts.Single(x => x.Level == 2);
        level2.Crafts.Should().Be(50);
        level2.Ingredients.Single(x => x.UniqueName == "T1_FISHCHOPS").Quantity.Should().Be(2250);

        // Shopping list = sum of both levels.
        result.Items.Single(x => x.UniqueName == "T1_FISHCHOPS").SafeQuantityToBuy.Should().Be(4500);
        result.Items.Single(x => x.UniqueName == "T1_SEAWEED").SafeQuantityToBuy.Should().Be(300);
    }

    [Test]
    public void SauceCrafts_IncludeSafetyMargin_AndShoppingMatchesSauceTable()
    {
        // 1 craft uses 90 sauces, ~50 expected after food returns. σ = sqrt(90 × 0.444 × 0.556) ≈ 4.71 → 2σ ≈ 9.4 → 60 sauces.
        var plan = new FoodPlan { Entries = [Entry("A@1", 1, 1, 90m, 50m)] };

        var result = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 2m);
        var sauce = result.FishSauceCrafts.Single();

        sauce.SaucesToCraft.Should().Be(60);
        sauce.Crafts.Should().Be(60);
        result.Items.Single(x => x.UniqueName == "T1_FISHCHOPS").SafeQuantityToBuy.Should().Be(60 * 15);
        result.Items.Single(x => x.UniqueName == "T1_FISHCHOPS").NetQuantityToBuy.Should().Be(50 * 15);
    }

    [Test]
    public void SauceCrafts_NeverMoreThanNoReturnNeed()
    {
        var plan = new FoodPlan { Entries = [Entry("A@1", 1, 1, 90m, 50m)] };

        var result = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 50m);

        result.FishSauceCrafts.Single().SaucesToCraft.Should().Be(90);
    }

    [Test]
    public void SauceIngredientAlsoUsedInFood_KeepsBothParts()
    {
        var sauceOnly = Entry("A@1", 1, 1, 90m, 50m);
        var food = new FoodPlanEntry
        {
            Crafts = 1,
            Candidate = new FoodCandidate
            {
                UniqueName = "A@1",
                AmountCrafted = 10,
                Resources = [..sauceOnly.Candidate.Resources, new FoodCandidateResource { UniqueName = "T1_SEAWEED", GrossQuantityPerCraft = 10m, NetQuantityPerCraft = 10m }]
            }
        };
        var plan = new FoodPlan { Entries = [food] };

        var item = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 0m).Items.Single(x => x.UniqueName == "T1_SEAWEED");

        item.SafeQuantityToBuy.Should().Be(10 + 50);
    }
}
