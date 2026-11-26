using FluentAssertions;
using NUnit.Framework;
using StatisticsAnalysisTool.Crafting.FoodCrafting;

namespace StatisticsAnalysisTool.UnitTests.Crafting;

// [FORK] Food crafting: plan lock + shopping safety margin. Fork-only file.
[TestFixture]
public class FoodPlanLockAndSafetyTests
{
    private static FoodPlan PlanWith(int crafts, decimal grossPerCraft, decimal netPerCraft)
    {
        return new FoodPlan
        {
            Entries =
            [
                new FoodPlanEntry
                {
                    Crafts = crafts,
                    Candidate = new FoodCandidate
                    {
                        UniqueName = "T6_MEAL_SALAD",
                        AmountCrafted = 10,
                        Resources =
                        [
                            new FoodCandidateResource
                            {
                                UniqueName = "T6_POTATO", GrossQuantityPerCraft = grossPerCraft, NetQuantityPerCraft = netPerCraft, UnitPrice = 100m
                            }
                        ]
                    }
                }
            ]
        };
    }

    [Test]
    public void SafeQuantity_AddsStandardDeviationsOfReturns()
    {
        // 2 crafts × 24 = 48 gross, 43.5% RRR → net 27.12. The last craft's 10.44 returns come too late → 37.56.
        // Variance of the first craft only = 24 × 0.435 × 0.565 = 5.899 → σ ≈ 2.4287
        var item = FoodShoppingListBuilder.Build(PlanWith(2, 24m, 13.56m), null, null, 0m, 2m).Single();

        item.NetQuantityToBuy.Should().Be(28);
        item.ExpectedQuantityToBuy.Should().Be(38);
        item.LastCraftExtra.Should().Be(10);
        item.SafeQuantity.Should().BeApproximately(37.56m + 2m * 2.4287m, 0.01m);
        item.SafeQuantityToBuy.Should().Be(43);
        item.SafetyExtra.Should().Be(5);
        item.GrossQuantityToBuy.Should().Be(48);
    }

    [Test]
    public void SafeQuantity_IsNeverMoreThanNoReturnQuantity()
    {
        // 1 craft of 2: 10 σ would be way more than 2 → capped at gross.
        var item = FoodShoppingListBuilder.Build(PlanWith(1, 2m, 1.13m), null, null, 0m, 10m).Single();

        item.SafeQuantityToBuy.Should().Be(2);
    }

    [Test]
    public void SafeQuantity_ZeroMargin_EqualsExpected()
    {
        var item = FoodShoppingListBuilder.Build(PlanWith(5, 24m, 13.56m), null, null, 0m, 0m).Single();

        item.SafeQuantityToBuy.Should().Be(item.ExpectedQuantityToBuy);
    }

    [Test]
    public void SingleCraft_NeedsFullQuantity_EvenWithoutSafetyMargin()
    {
        // Crafting once: returns only come after the craft, so all 24 are needed to start it.
        var item = FoodShoppingListBuilder.Build(PlanWith(1, 24m, 13.56m), null, null, 0m, 0m).Single();

        item.NetQuantityToBuy.Should().Be(14);
        item.SafeQuantityToBuy.Should().Be(24);
        item.LastCraftExtra.Should().Be(10);
    }

    [Test]
    public void IngredientShared_ByTwoFoods_OnlyLastCraftLosesReturns()
    {
        static FoodPlanEntry Food(string name, decimal gross, decimal net) => new()
        {
            Crafts = 1,
            Candidate = new FoodCandidate
            {
                UniqueName = name,
                AmountCrafted = 10,
                Resources = [new FoodCandidateResource { UniqueName = "T6_MILK", GrossQuantityPerCraft = gross, NetQuantityPerCraft = net, UnitPrice = 1m }]
            }
        };

        // Net 10 + 20 = 30; the biggest single craft returns 20 → 50 expected (gross 60).
        var plan = new FoodPlan { Entries = [Food("T6_MEAL_A", 20m, 10m), Food("T6_MEAL_B", 40m, 20m)] };
        var item = FoodShoppingListBuilder.Build(plan, null, null, 0m, 0m).Single();

        item.SafeQuantityToBuy.Should().Be(50);
    }

    [Test]
    public void SafeQuantity_NoReturns_NeedsNoExtra()
    {
        // Fish sauce ingredients: 0% RRR, net = gross → no randomness.
        var item = FoodShoppingListBuilder.Build(PlanWith(3, 15m, 15m), null, null, 0m, 2m).Single();

        item.SafeQuantityToBuy.Should().Be(45);
        item.SafetyExtra.Should().Be(0);
    }

    [Test]
    public void FromLocked_KeepsFoodsAndCrafts_WithNewPrices()
    {
        var candidates = new[]
        {
            new FoodCandidate { UniqueName = "T6_MEAL_SALAD", AmountCrafted = 10, FocusPerCraft = 100m, ProfitPerCraft = -50m, CostPerCraft = 1000m, DailyVolume = 500m },
            new FoodCandidate { UniqueName = "T7_MEAL_PIE", AmountCrafted = 10, FocusPerCraft = 100m, ProfitPerCraft = 9000m, CostPerCraft = 1000m, DailyVolume = 500m }
        };
        var locked = new[]
        {
            new FoodLockedPlanEntry { UniqueName = "t6_meal_salad", Crafts = 4 },
            new FoodLockedPlanEntry { UniqueName = "T8_GONE", Crafts = 2 }
        };

        var plan = FoodPlanner.FromLocked(candidates, locked, new FoodPlanInput { FocusBudget = 10000m, DailyVolumeDivisor = 10m });

        // Salad stays even though it now loses money; the better pie is NOT added; unknown food skipped.
        plan.IsLocked.Should().BeTrue();
        plan.Entries.Should().ContainSingle();
        plan.Entries[0].Candidate.UniqueName.Should().Be("T6_MEAL_SALAD");
        plan.Entries[0].Crafts.Should().Be(4);
        plan.Entries[0].MaxCrafts.Should().Be(5);
        plan.TotalProfit.Should().Be(-200m);
    }

    [Test]
    public void SettingsNormalize_UnlocksEmptyLockedPlan()
    {
        var settings = FoodCraftingSettingsStore.Normalize(new FoodCraftingSettings
        {
            IsPlanLocked = true,
            LockedPlan = [new FoodLockedPlanEntry { UniqueName = "", Crafts = 3 }],
            ShoppingSafetyMargin = 99m
        });

        settings.IsPlanLocked.Should().BeFalse();
        settings.ShoppingSafetyMargin.Should().Be(5m);
    }

    [Test]
    public void LockedPlan_ReportsFocusOverBudget()
    {
        var candidates = new[] { new FoodCandidate { UniqueName = "PIE", AmountCrafted = 10, FocusPerCraft = 500m, DailyVolume = 1000m } };

        var plan = FoodPlanner.FromLocked(candidates, [new FoodLockedPlanEntry { UniqueName = "PIE", Crafts = 30 }],
            new FoodPlanInput { FocusBudget = 10000m, DailyVolumeDivisor = 10m });

        plan.TotalFocus.Should().Be(15000m);
        plan.IsOverFocusBudget.Should().BeTrue();
        plan.FocusOverBudget.Should().Be(5000m);
        plan.RemainingFocus.Should().Be(0m);
    }

    [Test]
    public void Planner_NeverGoesOverFocusBudget()
    {
        var candidates = new[] { new FoodCandidate { UniqueName = "PIE", AmountCrafted = 10, FocusPerCraft = 253.4m, ProfitPerCraft = 1000m, DailyVolume = 100000m } };

        var plan = FoodPlanner.Plan(candidates, new FoodPlanInput { FocusBudget = 10000m, DailyVolumeDivisor = 10m });

        plan.TotalFocus.Should().BeLessThanOrEqualTo(10000m);
        plan.IsOverFocusBudget.Should().BeFalse();
    }
}
