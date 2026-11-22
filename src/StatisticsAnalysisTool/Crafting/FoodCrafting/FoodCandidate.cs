using System.Collections.Generic;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
/// <summary>One craftable food (incl. enchantment) evaluated for a single craft run.</summary>
public sealed class FoodCandidate
{
    public string UniqueName { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
    public int Tier { get; init; }
    public int EnchantmentLevel { get; init; }
    public int AmountCrafted { get; init; } = 1;

    public decimal SellPrice { get; init; }
    public decimal ReturnRatePercent { get; init; }

    public decimal BaseFocusPerCraft { get; init; }
    public decimal FocusCostEfficiency { get; init; }
    public decimal FocusPerCraft { get; init; }

    /// <summary>Material costs + station fee per craft (after expected returns).</summary>
    public decimal CostPerCraft { get; init; }

    /// <summary>Net sales revenue (after tax + setup fee) per craft.</summary>
    public decimal RevenuePerCraft { get; init; }

    public decimal ProfitPerCraft { get; init; }
    public decimal RoiPercent { get; init; }

    /// <summary>Average number of items sold per day in the selected city.</summary>
    public decimal DailyVolume { get; init; }

    public IReadOnlyList<FoodCandidateResource> Resources { get; init; } = [];

    /// <summary>Why this food cannot be evaluated (missing price etc.). Empty when valid.</summary>
    public string InvalidReason { get; init; } = string.Empty;

    /// <summary>Unique names of all items (the food itself or ingredients) whose price is missing or too old.</summary>
    public IReadOnlyList<string> MissingPriceItems { get; init; } = [];

    public bool IsValid => string.IsNullOrEmpty(InvalidReason);
    public decimal FocusPerItem => AmountCrafted > 0 ? FocusPerCraft / AmountCrafted : 0m;
    public decimal ProfitPerItem => AmountCrafted > 0 ? ProfitPerCraft / AmountCrafted : 0m;
    public decimal CostPerItem => AmountCrafted > 0 ? CostPerCraft / AmountCrafted : 0m;
    public decimal ProfitPerFocus => FocusPerCraft > 0m ? ProfitPerCraft / FocusPerCraft : 0m;
    public string TierText => $"T{Tier}.{EnchantmentLevel}";
}

public sealed class FoodCandidateResource
{
    public string UniqueName { get; init; }
    public decimal GrossQuantityPerCraft { get; init; }
    public decimal NetQuantityPerCraft { get; init; }
    public decimal UnitPrice { get; init; }

    /// <summary>Fish sauce level (1-3) when the resource is a fish sauce, otherwise 0.</summary>
    public int FishSauceLevel { get; init; }

    /// <summary>True when the sauce is crafted instead of bought.</summary>
    public bool IsCraftedSauce { get; init; }
}
