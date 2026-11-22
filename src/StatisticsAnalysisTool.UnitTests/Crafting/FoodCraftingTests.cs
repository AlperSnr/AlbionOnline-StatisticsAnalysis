using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;
using StatisticsAnalysisTool.GameFileData;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting feature tests. Fork-only file.
[TestFixture]
public class FoodCraftingTests
{
    #region Focus

    [TestCase(0, 504)]
    [TestCase(10000, 252)]
    [TestCase(20000, 126)]
    public void GetFocusCost_HalvesEveryTenThousandFce(int fce, int expected)
    {
        FoodFocusCalculator.GetFocusCost(504m, fce).Should().Be(expected);
    }

    [Test]
    public void GetFocusCostEfficiency_UsesMasteryOwnAndOtherSpecializations()
    {
        var board = new FoodDestinyBoard
        {
            MasteryLevel = 100,
            Specializations =
            [
                new FoodSpecializationLevel { Category = FoodCategories.Salads, Level = 100 },
                new FoodSpecializationLevel { Category = FoodCategories.Pies, Level = 50 }
            ]
        };

        // mastery 100 × 30 + salads 100 × (250 + 30) + pies 50 × 30
        FoodFocusCalculator.GetFocusCostEfficiency(board, "salads").Should().Be(32500m);
        // mastery 100 × 30 + pies 50 × (250 + 30) + salads 100 × 30
        FoodFocusCalculator.GetFocusCostEfficiency(board, "pies").Should().Be(20000m);
    }

    [Test]
    public void GetFocusCostEfficiency_IngredientsAndMeatAddToEveryFood()
    {
        var board = new FoodDestinyBoard
        {
            MasteryLevel = 0,
            Specializations =
            [
                new FoodSpecializationLevel { Category = FoodCategories.Ingredients, Level = 100 },
                new FoodSpecializationLevel { Category = FoodCategories.Meat, Level = 100 }
            ]
        };

        FoodFocusCalculator.GetFocusCostEfficiency(board, FoodCategories.Stews).Should().Be(6000m);
        // Foods without a specialization (e.g. grilled fish) only get the shared part.
        FoodFocusCalculator.GetFocusCostEfficiency(board, "grilledfish").Should().Be(6000m);
    }

    [Test]
    public void GetFocusCostEfficiency_ClampsLevels()
    {
        var board = new FoodDestinyBoard
        {
            MasteryLevel = 150,
            Specializations = [new FoodSpecializationLevel { Category = FoodCategories.Soups, Level = -5 }]
        };

        FoodFocusCalculator.GetFocusCostEfficiency(board, FoodCategories.Soups).Should().Be(3000m);
    }

    [Test]
    public void GetImpliedFocusCostEfficiency_ReversesTheFormula()
    {
        // Reference sheet: T6 Potato Salad, base 504 focus, 28 focus spent.
        FoodFocusCalculator.GetImpliedFocusCostEfficiency(504m, 28m).Should().BeApproximately(41699m, 1m);

        var cost = FoodFocusCalculator.GetFocusCost(504m, 41699m);
        cost.Should().BeApproximately(28m, 0.05m);
    }

    [Test]
    public void EnsureAllCategories_AddsMissingRemovesDuplicatesAndUnknown()
    {
        var board = new FoodDestinyBoard
        {
            Specializations =
            [
                new FoodSpecializationLevel { Category = "SALADS", Level = 40 },
                new FoodSpecializationLevel { Category = "salads", Level = 10 },
                new FoodSpecializationLevel { Category = "grilledfish", Level = 50 },
                new FoodSpecializationLevel { Category = "other", Level = 50 }
            ]
        };

        board.EnsureAllCategories();

        board.Specializations.Should().HaveCount(FoodCategories.Specializations.Count);
        board.Specializations.Select(x => x.Category).Should().Equal(FoodCategories.Specializations.Select(x => x.Id));
        board.GetSpecializationLevel(FoodCategories.Salads).Should().Be(40);
        board.GetSpecializationLevel(FoodCategories.Meat).Should().Be(0);
    }

    #endregion

    #region Return rate

    [Test]
    public void BrecilienWithFocus_MatchesReferenceSheetReturnRate()
    {
        // 18% city bonus + 59% focus = 77% → 77 / 177 = 43.5%
        CraftingLocationData.GetExpectedReturnRatePercent(18m, true).Should().Be(43.5m);
        CraftingLocationData.GetExpectedReturnRatePercent(18m, false).Should().Be(15.25m);
    }

    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(8, true)]
    public void HasStationFee_OnlyFromTierThree(int tier, bool expected)
    {
        FoodCandidateEvaluator.HasStationFee(tier).Should().Be(expected);
    }

    #endregion

    #region Fish sauce

    private static FishSauceRecipe SauceLevel1 => new()
    {
        Level = 1,
        UniqueName = "T1_FISHSAUCE_LEVEL1",
        Ingredients =
        [
            new FishSauceIngredient { UniqueName = "T1_FISHCHOPS", Count = 15 },
            new FishSauceIngredient { UniqueName = "T1_SEAWEED", Count = 1 }
        ]
    };

    private static readonly Dictionary<string, decimal> SaucePrices = new()
    {
        ["T1_FISHCHOPS"] = 235m,
        ["T1_SEAWEED"] = 420m
    };

    [Test]
    public void GetCraftedCost_WithoutReturns_MatchesReferenceSheet()
    {
        // Sheet: Tier 1 sauce = 15 × 235 + 1 × 420 = 3945
        FishSauceCalculator.GetCraftedCost(SauceLevel1, SaucePrices, 0m).Should().Be(3945m);
    }

    [Test]
    public void GetCraftedCost_AppliesReturnRate()
    {
        FishSauceCalculator.GetCraftedCost(SauceLevel1, SaucePrices, 15.25m).Should().Be(3343.39m);
    }

    [Test]
    public void GetCraftedCost_MissingIngredientPrice_ReturnsZero()
    {
        var prices = new Dictionary<string, decimal> { ["T1_FISHCHOPS"] = 235m };
        FishSauceCalculator.GetCraftedCost(SauceLevel1, prices, 0m).Should().Be(0m);
    }

    [TestCase(FishSauceMode.Cheapest, 5000, 3000, true)]
    [TestCase(FishSauceMode.Cheapest, 2000, 3000, false)]
    [TestCase(FishSauceMode.Cheapest, 0, 3000, true)]
    [TestCase(FishSauceMode.Buy, 5000, 3000, false)]
    [TestCase(FishSauceMode.Buy, 0, 3000, true)]
    [TestCase(FishSauceMode.Craft, 2000, 3000, true)]
    [TestCase(FishSauceMode.Craft, 2000, 0, false)]
    public void Resolve_PicksBuyOrCraft(FishSauceMode mode, int marketPrice, int craftedCost, bool expectedCrafting)
    {
        var result = FishSauceCalculator.Resolve(1, mode, marketPrice, craftedCost);

        result.UsesCrafting.Should().Be(expectedCrafting);
        result.EffectivePrice.Should().Be(expectedCrafting ? craftedCost : marketPrice);
    }

    [TestCase("T1_FISHSAUCE_LEVEL1", 1)]
    [TestCase("T1_FISHSAUCE_LEVEL3", 3)]
    [TestCase("T6_POTATO", 0)]
    public void GetLevel_ParsesSauceLevel(string uniqueName, int expected)
    {
        FishSauceRecipe.GetLevel(uniqueName).Should().Be(expected);
    }

    #endregion

    #region Planner

    private static FoodCandidate Candidate(string name, decimal focusPerCraft, decimal profitPerCraft, decimal dailyVolume, int amountCrafted = 10)
    {
        return new FoodCandidate
        {
            UniqueName = name,
            Name = name,
            AmountCrafted = amountCrafted,
            FocusPerCraft = focusPerCraft,
            ProfitPerCraft = profitPerCraft,
            CostPerCraft = 1000m,
            DailyVolume = dailyVolume
        };
    }

    [Test]
    public void GetMaxCrafts_UsesDailyVolumeDivisorAndAmountCrafted()
    {
        // 305 / 10 = 30 items → 3 crafts of 10
        FoodPlanner.GetMaxCrafts(Candidate("A", 100m, 1000m, 305m), 10m).Should().Be(3);
        FoodPlanner.GetMaxCrafts(Candidate("B", 100m, 1000m, 305m, 1), 10m).Should().Be(30);
        FoodPlanner.GetMaxCrafts(Candidate("C", 100m, 1000m, 0m), 10m).Should().Be(0);
    }

    [Test]
    public void Plan_FillsBestProfitPerFocusFirst_AndRespectsSalesCap()
    {
        var best = Candidate("BEST", 100m, 5000m, 200m);   // 50 / focus, max 2 crafts
        var second = Candidate("SECOND", 200m, 6000m, 10000m); // 30 / focus, max 100 crafts

        var plan = FoodPlanner.Plan([second, best], new FoodPlanInput
        {
            FocusBudget = 1000m,
            DailyVolumeDivisor = 10m
        });

        plan.Entries.Should().HaveCount(2);
        plan.Entries[0].Candidate.UniqueName.Should().Be("BEST");
        plan.Entries[0].Crafts.Should().Be(2);
        plan.Entries[1].Candidate.UniqueName.Should().Be("SECOND");
        plan.Entries[1].Crafts.Should().Be(4); // (1000 - 200) / 200
        plan.TotalFocus.Should().Be(1000m);
        plan.RemainingFocus.Should().Be(0m);
        plan.TotalProfit.Should().Be(2 * 5000m + 4 * 6000m);
    }

    [Test]
    public void Plan_SkipsUnprofitableInvalidAndBelowMinimum()
    {
        var losing = Candidate("LOSING", 100m, -500m, 10000m);
        var lowMargin = Candidate("LOW", 100m, 500m, 10000m); // 5 / focus
        var invalid = new FoodCandidate
        {
            UniqueName = "INVALID",
            FocusPerCraft = 100m,
            ProfitPerCraft = 9000m,
            DailyVolume = 10000m,
            InvalidReason = "No price"
        };
        var good = Candidate("GOOD", 100m, 2000m, 10000m); // 20 / focus

        var plan = FoodPlanner.Plan([losing, lowMargin, invalid, good], new FoodPlanInput
        {
            FocusBudget = 500m,
            DailyVolumeDivisor = 10m,
            MinProfitPerFocus = 10m
        });

        plan.Entries.Should().ContainSingle();
        plan.Entries[0].Candidate.UniqueName.Should().Be("GOOD");
        plan.Entries[0].Crafts.Should().Be(5);
    }

    [Test]
    public void Plan_SkipsFoodTooExpensiveForRemainingFocus_AndUsesCheaperOne()
    {
        var expensive = Candidate("EXPENSIVE", 800m, 40000m, 10000m); // 50 / focus
        var cheap = Candidate("CHEAP", 50m, 1000m, 10000m);            // 20 / focus

        var plan = FoodPlanner.Plan([expensive, cheap], new FoodPlanInput
        {
            FocusBudget = 1000m,
            DailyVolumeDivisor = 10m
        });

        plan.Entries.Select(x => (x.Candidate.UniqueName, x.Crafts)).Should().Equal(("EXPENSIVE", 1), ("CHEAP", 4));
    }

    #endregion

    #region Shopping list

    [Test]
    public void ShoppingList_ReplacesCraftedSauceWithIngredients()
    {
        var candidate = new FoodCandidate
        {
            UniqueName = "T6_MEAL_SALAD@1",
            AmountCrafted = 10,
            FocusPerCraft = 100m,
            ProfitPerCraft = 1000m,
            DailyVolume = 1000m,
            Resources =
            [
                new FoodCandidateResource { UniqueName = "T6_POTATO", GrossQuantityPerCraft = 72m, NetQuantityPerCraft = 40m, UnitPrice = 500m },
                new FoodCandidateResource { UniqueName = "T1_FISHSAUCE_LEVEL1", GrossQuantityPerCraft = 90m, NetQuantityPerCraft = 50m, UnitPrice = 3000m, FishSauceLevel = 1, IsCraftedSauce = true }
            ]
        };

        var plan = new FoodPlan
        {
            FocusBudget = 1000m,
            Entries = [new FoodPlanEntry { Candidate = candidate, Crafts = 2, MaxCrafts = 10 }]
        };

        var list = FoodShoppingListBuilder.Build(plan, new Dictionary<int, FishSauceRecipe> { [1] = SauceLevel1 }, SaucePrices, 0m);

        list.Should().HaveCount(3);
        list.Single(x => x.UniqueName == "T6_POTATO").NetQuantityToBuy.Should().Be(80);
        list.Single(x => x.UniqueName == "T6_POTATO").GrossQuantityToBuy.Should().Be(144);
        // 100 sauces needed → 1500 chopped fish + 100 seaweed (no sauce returns in this test)
        list.Single(x => x.UniqueName == "T1_FISHCHOPS").NetQuantityToBuy.Should().Be(1500);
        list.Single(x => x.UniqueName == "T1_SEAWEED").NetQuantityToBuy.Should().Be(100);
        list.Single(x => x.UniqueName == "T1_SEAWEED").IsForFishSauce.Should().BeTrue();
        list.Should().NotContain(x => x.UniqueName == "T1_FISHSAUCE_LEVEL1");
    }

    #endregion
}
