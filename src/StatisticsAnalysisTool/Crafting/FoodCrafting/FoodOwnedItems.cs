using Serilog;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.StorageHistory;
using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
// Reads what you already own so the shopping list only shows what is still missing.
// Bank contents come from the existing Storage History (filled whenever a bank chest is opened in-game);
// nothing in the upstream code is changed for this.

public sealed class FoodBankContents
{
    public Dictionary<string, int> Quantities { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int ContainerCount { get; init; }

    /// <summary>Oldest update time of the read chests (UTC), default when none.</summary>
    public DateTime OldestUpdateUtc { get; init; }
}

public static class FoodOwnedItems
{
    /// <summary>
    /// Personal bank chests of the city from Storage History. Guild chests are left out (shared with others).
    /// Must be called on the UI thread (Storage History collections are UI-bound).
    /// </summary>
    public static FoodBankContents ReadBank(MarketLocation city)
    {
        try
        {
            var vaults = ServiceLocator.Resolve<MainWindowViewModel>()?.VaultBindings?.Vaults;
            return ReadBank(vaults, city);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Food crafting could not read the bank contents from Storage History");
            return new FoodBankContents();
        }
    }

    public static FoodBankContents ReadBank(IEnumerable<Vault> vaults, MarketLocation city)
    {
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var containers = 0;
        var oldest = DateTime.MaxValue;

        foreach (var vault in (vaults ?? []).Where(x => x != null && IsInCity(x, city)))
        {
            foreach (var container in (vault.VaultContainer ?? []).Where(x => x is { IsGuildContainer: false }))
            {
                containers++;
                if (container.LastUpdate > DateTime.MinValue && container.LastUpdate < oldest)
                {
                    oldest = container.LastUpdate;
                }

                foreach (var item in container.Items ?? [])
                {
                    if (item == null || item.ItemIndex <= 0 || item.Quantity <= 0)
                    {
                        continue;
                    }

                    var uniqueName = item.Item?.UniqueName;
                    if (string.IsNullOrWhiteSpace(uniqueName))
                    {
                        continue;
                    }

                    quantities[uniqueName] = quantities.GetValueOrDefault(uniqueName) + item.Quantity;
                }
            }
        }

        return new FoodBankContents
        {
            Quantities = quantities,
            ContainerCount = containers,
            OldestUpdateUtc = oldest == DateTime.MaxValue ? default : oldest
        };
    }

    private static bool IsInCity(Vault vault, MarketLocation city)
    {
        if (FoodCandidateEvaluator.CityClusterIds.TryGetValue(city, out var clusterId)
            && string.Equals(vault.MainLocationIndex, clusterId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var cityName = Locations.GetDisplayName(city);
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return false;
        }

        return ContainsName(vault.MainLocation, cityName) || ContainsName(vault.Location, cityName);
    }

    private static bool ContainsName(string value, string cityName)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(cityName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Bank + manually entered quantities.</summary>
    public static Dictionary<string, int> Combine(IReadOnlyDictionary<string, int> bank, IReadOnlyDictionary<string, int> manual)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { bank, manual })
        {
            foreach (var (uniqueName, quantity) in source ?? new Dictionary<string, int>())
            {
                if (!string.IsNullOrWhiteSpace(uniqueName) && quantity > 0)
                {
                    result[uniqueName] = result.GetValueOrDefault(uniqueName) + quantity;
                }
            }
        }

        return result;
    }
}
