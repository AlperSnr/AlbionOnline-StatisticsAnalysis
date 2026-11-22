using Serilog;
using StatisticsAnalysisTool.Common;
using System;
using System.Threading.Tasks;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public static class FoodCraftingSettingsStore
{
    private const string FileName = "FoodCrafting.json";

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

        return settings;
    }
}
