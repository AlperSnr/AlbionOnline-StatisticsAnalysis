using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting: subtract already owned items. Fork-only file.
[TestFixture]
public class FoodOwnedItemsTests
{
    private static readonly Dictionary<int, FishSauceRecipe> Recipes = new()
    {
        [1] = new FishSauceRecipe
        {
            Level = 1,
            UniqueName = "T1_FISHSAUCE_LEVEL1",
            Ingredients =
            [
                new FishSauceIngredient { UniqueName = "T1_FISHCHOPS", Count = 15 },
                new FishSauceIngredient { UniqueName = "T1_SEAWEED", Count = 1 }
            ]
        }
    };

    private static FoodPlan Plan(params FoodCandidateResource[] resources) => new()
    {
        Entries = [new FoodPlanEntry { Crafts = 1, Candidate = new FoodCandidate { UniqueName = "FOOD", AmountCrafted = 10, Resources = resources } }]
    };

    [Test]
    public void OwnedItems_AreSubtractedFromWhatToBuy()
    {
        var plan = Plan(new FoodCandidateResource { UniqueName = "T6_POTATO", GrossQuantityPerCraft = 100m, NetQuantityPerCraft = 100m, UnitPrice = 50m });

        var item = FoodShoppingListBuilder.Build(plan, Recipes, null, 0m, 0m, new Dictionary<string, int> { ["t6_potato"] = 30 }).Single();

        item.SafeQuantityToBuy.Should().Be(100);
        item.OwnedQuantity.Should().Be(30);
        item.QuantityToBuy.Should().Be(70);
        item.ToBuyCost.Should().Be(3500m);
    }

    [Test]
    public void OwnedMoreThanNeeded_BuysNothing()
    {
        var plan = Plan(new FoodCandidateResource { UniqueName = "T6_POTATO", GrossQuantityPerCraft = 100m, NetQuantityPerCraft = 100m });

        var item = FoodShoppingListBuilder.Build(plan, Recipes, null, 0m, 0m, new Dictionary<string, int> { ["T6_POTATO"] = 500 }).Single();

        item.OwnedQuantity.Should().Be(100);
        item.QuantityToBuy.Should().Be(0);
    }

    [Test]
    public void OwnedFishSauce_ReducesSauceCraftsAndIngredients()
    {
        var plan = Plan(new FoodCandidateResource
        {
            UniqueName = "T1_FISHSAUCE_LEVEL1", GrossQuantityPerCraft = 90m, NetQuantityPerCraft = 90m, FishSauceLevel = 1, IsCraftedSauce = true
        });

        var result = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 0m, new Dictionary<string, int> { ["T1_FISHSAUCE_LEVEL1"] = 40 });
        var sauce = result.FishSauceCrafts.Single();

        sauce.SaucesNeeded.Should().Be(90);
        sauce.OwnedSauces.Should().Be(40);
        sauce.SaucesToCraft.Should().Be(50);
        sauce.Crafts.Should().Be(50);
        result.Items.Single(x => x.UniqueName == "T1_FISHCHOPS").QuantityToBuy.Should().Be(750);
    }

    [Test]
    public void EnoughOwnedFishSauce_NoIngredientsToBuy()
    {
        var plan = Plan(new FoodCandidateResource
        {
            UniqueName = "T1_FISHSAUCE_LEVEL1", GrossQuantityPerCraft = 90m, NetQuantityPerCraft = 90m, FishSauceLevel = 1, IsCraftedSauce = true
        });

        var result = FoodShoppingListBuilder.BuildWithFishSauce(plan, Recipes, null, 0m, 0m, new Dictionary<string, int> { ["T1_FISHSAUCE_LEVEL1"] = 200 });

        result.FishSauceCrafts.Single().Crafts.Should().Be(0);
        result.Items.Where(x => x.UniqueName is "T1_FISHCHOPS" or "T1_SEAWEED").Sum(x => x.QuantityToBuy).Should().Be(0);
    }

    [Test]
    public void Combine_AddsBankAndManual()
    {
        var combined = FoodOwnedItems.Combine(
            new Dictionary<string, int> { ["T1_SEAWEED"] = 10, ["T6_POTATO"] = 0 },
            new Dictionary<string, int> { ["t1_seaweed"] = 5, ["T1_FISHCHOPS"] = 3 });

        combined["T1_SEAWEED"].Should().Be(15);
        combined["T1_FISHCHOPS"].Should().Be(3);
        combined.Should().NotContainKey("T6_POTATO");
    }

    [Test]
    public void SettingsNormalize_ManualOwnedIsCaseInsensitive()
    {
        var settings = FoodCraftingSettingsStore.Normalize(new FoodCraftingSettings
        {
            ManualOwned = new Dictionary<string, int> { ["T1_SEAWEED"] = 5, ["X"] = 0 }
        });

        settings.ManualOwned["t1_seaweed"].Should().Be(5);
        settings.ManualOwned.Should().NotContainKey("X");
    }
}
