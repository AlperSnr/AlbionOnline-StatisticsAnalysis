using Serilog;
using StatisticsAnalysisTool.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public static class FoodCraftingSettingsStore
{
    private const string FileName = "FoodCrafting.json";
    private const string OrderBooksFileName = "FoodCraftingOrderBooks.json";

    public static async Task LoadOrderBooksAsync()
    {
        try
        {
            var books = await FileController.LoadAsync<List<FoodOrderBook>>(AppDataPaths.UserDataFile(OrderBooksFileName)).ConfigureAwait(false);
            FoodOrderBookStore.Shared.Load(books, DateTime.UtcNow);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Food crafting order books could not be loaded");
        }
    }

    public static async Task SaveOrderBooksAsync()
    {
        if (!AppDataPaths.TryEnsureUserDataDirectory())
        {
            return;
        }

        try
        {
            await FileController.SaveAsync(FoodOrderBookStore.Shared.GetAll(), AppDataPaths.UserDataFile(OrderBooksFileName)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Food crafting order books could not be saved");
        }
    }

    public static async Task<FoodCraftingSettings> LoadAsync()
    {
        try
        {
            var settings = await FileController.LoadAsync<FoodCraftingSettings>(AppDataPaths.UserDataFile(FileName)).ConfigureAwait(false);
            return Normalize(settings);
        }
        catch (Exception e)
        {
            Log.Error(e, "Food crafting settings could not be loaded");
            return Normalize(new FoodCraftingSettings());
        }
    }

    public static async Task SaveAsync(FoodCraftingSettings settings)
    {
        if (settings == null || !AppDataPaths.TryEnsureUserDataDirectory())
        {
            return;
        }

        try
        {
            await FileController.SaveAsync(settings, AppDataPaths.UserDataFile(FileName)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Food crafting settings could not be saved");
        }
    }

    public static FoodCraftingSettings Normalize(FoodCraftingSettings settings)
    {
        settings ??= new FoodCraftingSettings();
        settings.DestinyBoard ??= new FoodDestinyBoard();
        settings.DestinyBoard.EnsureAllCategories();

        if (!FoodCandidateEvaluator.CityClusterIds.ContainsKey(settings.City))
        {
            settings.City = MarketLocation.BrecilienMarket;
        }

        settings.MaxInvestment = Math.Max(0m, settings.MaxInvestment);
        settings.OrderBookMaxAgeMinutes = Math.Max(0, settings.OrderBookMaxAgeMinutes);
        settings.ShoppingSafetyMargin = Math.Clamp(settings.ShoppingSafetyMargin, 0m, 5m);
        settings.ManualOwned = (settings.ManualOwned ?? new Dictionary<string, int>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Value), StringComparer.OrdinalIgnoreCase);
        settings.LockedPlan ??= [];
        settings.LockedPlan.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.UniqueName) || x.Crafts <= 0);
        if (settings.LockedPlan.Count == 0)
        {
            settings.IsPlanLocked = false;
        }

        return settings;
    }
}
