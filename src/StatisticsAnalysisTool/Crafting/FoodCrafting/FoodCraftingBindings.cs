using Serilog;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Models;
using StatisticsAnalysisTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public sealed class FoodCraftingBindings : BaseViewModel
{
    private static readonly Lazy<FoodCraftingBindings> SharedInstance = new(() => new FoodCraftingBindings());

    private readonly FoodRecipeCatalog _catalog = new();
    private readonly FoodMarketApiClient _apiClient = new();
    private readonly FoodCandidateEvaluator _evaluator;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isLoaded;
    private Dictionary<string, FoodMarketPrice> _lastPrices;
    private Dictionary<string, decimal> _lastVolumes;
    private DateTime _lastDownloadUtc;

    private FoodCraftingBindings()
    {
        _evaluator = new FoodCandidateEvaluator(_catalog);
        Settings = FoodCraftingSettingsStore.Normalize(new FoodCraftingSettings());
    }

    public static FoodCraftingBindings Shared => SharedInstance.Value;

    public FoodCraftingSettings Settings
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FishSauceLevel1Mode));
            OnPropertyChanged(nameof(FishSauceLevel2Mode));
            OnPropertyChanged(nameof(FishSauceLevel3Mode));
        }
    }

    public KeyValuePair<MarketLocation, string>[] CityOptions { get; } = FoodCandidateEvaluator.CityClusterIds.Keys
        .Select(x => new KeyValuePair<MarketLocation, string>(x, Locations.GetDisplayName(x)))
        .OrderBy(x => x.Value)
        .ToArray();

    public decimal[] DailyBonusOptions { get; } = [0m, 10m, 20m];

    public FishSauceMode[] FishSauceModeOptions { get; } = [FishSauceMode.Cheapest, FishSauceMode.Buy, FishSauceMode.Craft];

    public FishSauceMode FishSauceLevel1Mode
    {
        get => Settings?.FishSauceLevel1Mode ?? FishSauceMode.Cheapest;
        set => SetFishSauceMode(1, value);
    }

    public FishSauceMode FishSauceLevel2Mode
    {
        get => Settings?.FishSauceLevel2Mode ?? FishSauceMode.Cheapest;
        set => SetFishSauceMode(2, value);
    }

    public FishSauceMode FishSauceLevel3Mode
    {
        get => Settings?.FishSauceLevel3Mode ?? FishSauceMode.Cheapest;
        set => SetFishSauceMode(3, value);
    }

    public ObservableCollection<FoodCandidateRow> Candidates { get; } = [];
    public ObservableCollection<FoodPlanRow> PlanRows { get; } = [];
    public ObservableCollection<FoodShoppingRow> ShoppingRows { get; } = [];
    public ObservableCollection<FishSauceRow> FishSauceRows { get; } = [];
    public ObservableCollection<FoodItemOption> FoodItemOptions { get; } = [];
    public ObservableCollection<FoodMissingPriceRow> MissingPriceRows { get; } = [];

    public string MissingPriceSummaryText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }
    = "Missing / outdated prices";

    public FoodPlan Plan
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }
    = "Press \"Load prices & plan\" to download prices and sales volume for all foods.";

    public string ReturnRateText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }
    = string.Empty;

    public bool IsBusy
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsBusy;

    #region Focus check (calibration)

    public FoodItemOption CalibrationItem
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateCalibration();
        }
    }

    public decimal CalibrationInGameFocus
    {
        get;
        set
        {
            field = Math.Max(0m, value);
            OnPropertyChanged();
            UpdateCalibration();
        }
    }

    public string CalibrationResultText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }
    = "Pick a food, type the focus cost the game shows, and compare.";

    #endregion

    public ICommand LoadAndPlanCommand => field ??= new CommandHandler(async _ => await LoadAndPlanAsync(), true);

    public ICommand RecalculateCommand => field ??= new CommandHandler(_ => Recalculate(), true);

    public ICommand CancelCommand => field ??= new CommandHandler(_ => _cancellationTokenSource?.Cancel(), true);

    public ICommand UpdateCalibrationCommand => field ??= new CommandHandler(_ => UpdateCalibration(), true);

    public ICommand CopyMissingPricesCommand => field ??= new CommandHandler(_ => CopyMissingPrices(), true);

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;

        try
        {
            Settings = await FoodCraftingSettingsStore.LoadAsync();
            LoadFoodItemOptions();
        }
        catch (Exception e)
        {
            Log.Error(e, "Food crafting tab could not be initialized");
        }
    }

    public Task SaveSettingsAsync()
    {
        return FoodCraftingSettingsStore.SaveAsync(Settings);
    }

    private void SetFishSauceMode(int level, FishSauceMode mode)
    {
        Settings?.SetFishSauceMode(level, mode);
        OnPropertyChanged("FishSauceLevel" + level + "Mode");
    }

    private void LoadFoodItemOptions()
    {
        FoodItemOptions.Clear();

        foreach (var item in _catalog.GetFoodItems().OrderBy(x => x.LocalizedName).ThenBy(x => x.Level))
        {
            FoodItemOptions.Add(new FoodItemOption
            {
                UniqueName = item.UniqueName,
                DisplayName = $"{item.LocalizedName} (T{item.Tier}.{item.Level})",
                Category = FoodRecipeCatalog.GetCategory(item),
                BaseFocus = _catalog.GetBaseFocus(item)
            });
        }
    }

    private void UpdateCalibration()
    {
        var option = CalibrationItem;
        if (option == null || Settings == null)
        {
            CalibrationResultText = "Pick a food, type the focus cost the game shows, and compare.";
            return;
        }

        var fce = FoodFocusCalculator.GetFocusCostEfficiency(Settings.DestinyBoard, option.Category);
        var calculated = FoodFocusCalculator.GetFocusCost(option.BaseFocus, fce);
        var text = $"Base focus {option.BaseFocus:N0} | Your FCE {fce:N0} ({FoodCategories.GetDisplayName(option.Category)}) | Calculated {calculated:N1}";

        if (CalibrationInGameFocus > 0m)
        {
            var implied = FoodFocusCalculator.GetImpliedFocusCostEfficiency(option.BaseFocus, CalibrationInGameFocus);
            var difference = calculated - CalibrationInGameFocus;
            text += $"\nIn-game {CalibrationInGameFocus:N1} → implied FCE {implied:N0} | Difference {difference:+0.0;-0.0;0}";
        }

        CalibrationResultText = text;
    }

    private async Task LoadAndPlanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            await SaveSettingsAsync();
            var settings = Settings;

            StatusText = "Reading recipes...";
            var (foods, names) = await Task.Run(() => CollectItemNames(settings), cancellationToken);
            if (foods.Count == 0)
            {
                StatusText = "No food recipes found in the game data.";
                return;
            }

            StatusText = $"Downloading prices for {names.Count} items in {Locations.GetDisplayName(settings.City)}...";
            var prices = await _apiClient.GetPricesAsync(names, settings.City, cancellationToken);

            StatusText = $"Downloading sales history for {foods.Count} foods...";
            var volumes = await _apiClient.GetDailyVolumesAsync(foods.Select(x => x.UniqueName).ToList(), settings.City, settings.VolumeDays, cancellationToken);

            _lastPrices = prices;
            _lastVolumes = volumes;
            _lastDownloadUtc = DateTime.UtcNow;

            await RecalculateAsync(foods);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        catch (Exception e)
        {
            Log.Error(e, "Food crafting plan could not be created");
            StatusText = "Error: " + e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void Recalculate()
    {
        if (IsBusy)
        {
            return;
        }

        if (_lastPrices == null)
        {
            StatusText = "No prices yet. Press \"Load prices & plan\" first.";
            return;
        }

        IsBusy = true;

        try
        {
            await SaveSettingsAsync();
            var settings = Settings;
            var (foods, _) = await Task.Run(() => CollectItemNames(settings));
            await RecalculateAsync(foods);
        }
        catch (Exception e)
        {
            Log.Error(e, "Food crafting plan could not be recalculated");
            StatusText = "Error: " + e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RecalculateAsync(List<Item> foods)
    {
        var settings = Settings;
        var prices = _lastPrices ?? new Dictionary<string, FoodMarketPrice>();
        var volumes = _lastVolumes ?? new Dictionary<string, decimal>();

        StatusText = "Calculating...";

        var result = await Task.Run(() =>
        {
            var nowUtc = DateTime.UtcNow;
            var unitPrices = FoodPriceCheck.GetUsablePrices(prices, nowUtc, settings.MaxPriceAgeHours);
            var sauceRecipes = _catalog.GetFishSauceRecipes();
            var sauceReturnRate = FoodCandidateEvaluator.FishSauceReturnRatePercent;
            var sauceCosts = new Dictionary<int, FishSauceCost>();
            var sauceMissing = new Dictionary<int, IReadOnlyList<string>>();

            for (var level = 1; level <= 3; level++)
            {
                var marketPrice = unitPrices.GetValueOrDefault(FishSauceRecipe.GetUniqueName(level));
                var craftedCost = sauceRecipes.TryGetValue(level, out var recipe)
                    ? FishSauceCalculator.GetCraftedCost(recipe, unitPrices, sauceReturnRate)
                    : 0m;
                sauceCosts[level] = FishSauceCalculator.Resolve(level, settings.GetFishSauceMode(level), marketPrice, craftedCost);
                sauceMissing[level] = FishSauceCalculator.GetMissingPriceItems(sauceCosts[level], recipe, unitPrices);
            }

            var context = new FoodEvaluationContext
            {
                Settings = settings,
                UnitPrices = unitPrices,
                DailyVolumes = volumes,
                FishSauceCosts = sauceCosts,
                FishSauceMissingItems = sauceMissing
            };

            var candidates = foods.Select(x => _evaluator.Evaluate(x, context)).ToList();
            var plan = FoodPlanner.Plan(candidates, new FoodPlanInput
            {
                FocusBudget = settings.FocusBudget,
                DailyVolumeDivisor = settings.DailyVolumeDivisor,
                MinProfitPerFocus = settings.MinProfitPerFocus
            });
            var shopping = FoodShoppingListBuilder.Build(plan, sauceRecipes, unitPrices, sauceReturnRate);
            var foodReturnRate = foods.Count > 0 ? _evaluator.GetFoodReturnRatePercent(foods[0], settings) : 0m;
            var missingPrices = FoodMissingPriceListBuilder.Build(candidates, prices, nowUtc, settings.MaxPriceAgeHours);

            return (candidates, plan, shopping, sauceCosts, sauceRecipes, sauceReturnRate, foodReturnRate, missingPrices);
        });

        ApplyResult(result.candidates, result.plan, result.shopping, result.sauceCosts, result.sauceReturnRate, result.foodReturnRate, settings);
        ApplyMissingPrices(result.missingPrices, result.candidates.Count(x => !x.IsValid));
    }

    private void ApplyResult(
        List<FoodCandidate> candidates,
        FoodPlan plan,
        IReadOnlyList<FoodShoppingItem> shopping,
        Dictionary<int, FishSauceCost> sauceCosts,
        decimal sauceReturnRate,
        decimal foodReturnRate,
        FoodCraftingSettings settings)
    {
        var plannedByName = plan.Entries.ToDictionary(x => x.Candidate.UniqueName, StringComparer.OrdinalIgnoreCase);

        Candidates.Clear();
        foreach (var candidate in FoodPlanner.GetEligible(candidates, settings.MinProfitPerFocus))
        {
            plannedByName.TryGetValue(candidate.UniqueName, out var planned);
            Candidates.Add(new FoodCandidateRow
            {
                Candidate = candidate,
                Icon = GetIcon(candidate.UniqueName),
                MaxCrafts = FoodPlanner.GetMaxCrafts(candidate, settings.DailyVolumeDivisor),
                PlannedCrafts = planned?.Crafts ?? 0
            });
        }

        PlanRows.Clear();
        foreach (var entry in plan.Entries)
        {
            PlanRows.Add(new FoodPlanRow
            {
                Entry = entry,
                Icon = GetIcon(entry.Candidate.UniqueName)
            });
        }

        ShoppingRows.Clear();
        foreach (var item in shopping)
        {
            ShoppingRows.Add(new FoodShoppingRow
            {
                Item = item,
                Name = ItemController.GetItemByUniqueName(item.UniqueName)?.LocalizedName ?? item.UniqueName,
                Icon = GetIcon(item.UniqueName)
            });
        }

        FishSauceRows.Clear();
        foreach (var sauce in sauceCosts.Values.OrderBy(x => x.Level))
        {
            FishSauceRows.Add(new FishSauceRow
            {
                Cost = sauce,
                Name = ItemController.GetItemByUniqueName(FishSauceRecipe.GetUniqueName(sauce.Level))?.LocalizedName ?? FishSauceRecipe.GetUniqueName(sauce.Level),
                Icon = GetIcon(FishSauceRecipe.GetUniqueName(sauce.Level))
            });
        }

        Plan = plan;
        ReturnRateText = $"Food RRR (with focus): {foodReturnRate:N2}% | Fish sauce RRR: {sauceReturnRate:N2}% (no returns when crafting sauce)";

        var valid = candidates.Count(x => x.IsValid);
        var missing = candidates.Count - valid;
        var age = _lastDownloadUtc == default ? string.Empty : $" Prices downloaded {_lastDownloadUtc.ToLocalTime():t}.";
        StatusText = $"{candidates.Count} foods checked: {valid} with prices, {missing} skipped (see \"Missing / outdated prices\"), "
                     + $"{Candidates.Count} profitable. Plan: {plan.Entries.Count} foods, {plan.TotalFocus:N0} of {plan.FocusBudget:N0} focus.{age}";
        UpdateCalibration();
    }

    private (List<Item> Foods, List<string> Names) CollectItemNames(FoodCraftingSettings settings)
    {
        var foods = _catalog.GetFoodItems()
            .Where(x => settings.IsEnchantmentIncluded(x.Level))
            .ToList();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var food in foods)
        {
            names.Add(food.UniqueName);
            foreach (var resource in _catalog.GetResources(food))
            {
                names.Add(resource.UniqueName);
            }
        }

        foreach (var recipe in _catalog.GetFishSauceRecipes().Values)
        {
            names.Add(recipe.UniqueName);
            foreach (var ingredient in recipe.Ingredients)
            {
                names.Add(ingredient.UniqueName);
            }
        }

        return (foods, names.ToList());
    }

    private void ApplyMissingPrices(IReadOnlyList<FoodMissingPriceItem> items, int skippedFoods)
    {
        MissingPriceRows.Clear();
        foreach (var item in items)
        {
            var gameItem = ItemController.GetItemByUniqueName(item.UniqueName);
            MissingPriceRows.Add(new FoodMissingPriceRow
            {
                Item = item,
                Name = gameItem?.LocalizedName ?? item.UniqueName,
                TierText = gameItem == null ? string.Empty : $"T{gameItem.Tier}.{gameItem.Level}",
                Icon = GetIcon(item.UniqueName)
            });
        }

        var noData = items.Count(x => x.Status == FoodPriceStatus.NoData);
        var outdated = items.Count(x => x.Status == FoodPriceStatus.Outdated);
        MissingPriceSummaryText = items.Count == 0
            ? "Missing / outdated prices (none)"
            : $"Missing / outdated prices: {items.Count} items ({noData} no price, {outdated} too old) keep {skippedFoods} foods out of the plan";
    }

    private void CopyMissingPrices()
    {
        if (MissingPriceRows.Count == 0)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, MissingPriceRows
            .Select(x => $"{x.Name} {x.TierText}	{x.Item.StatusText}	blocks {x.Item.BlockedFoodCount}"));

        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Missing food prices could not be copied to the clipboard");
        }
    }

    private static BitmapImage GetIcon(string uniqueName)
    {
        try
        {
            return ItemController.GetItemByUniqueName(uniqueName)?.Icon;
        }
        catch (Exception e)
        {
            Log.Debug(e, "Food crafting icon could not be loaded for {UniqueName}", uniqueName);
            return null;
        }
    }
}

public sealed class FoodItemOption
{
    public string UniqueName { get; init; }
    public string DisplayName { get; init; }
    public string Category { get; init; }
    public decimal BaseFocus { get; init; }
}

public sealed class FoodCandidateRow
{
    public FoodCandidate Candidate { get; init; }
    public BitmapImage Icon { get; init; }
    public int MaxCrafts { get; init; }
    public int PlannedCrafts { get; init; }
    public string CategoryText => FoodCategories.GetDisplayName(Candidate?.Category);
}

public sealed class FoodMissingPriceRow
{
    public FoodMissingPriceItem Item { get; init; }
    public string Name { get; init; }
    public string TierText { get; init; }
    public BitmapImage Icon { get; init; }
    public string UsageText => Item?.IsFood == true ? "Food" : "Ingredient";
    public string LastPriceText => Item?.LastSellPrice > 0m ? Item.LastSellPrice.ToString("N0", CultureInfo.CurrentCulture) : "-";
    public string BlockedFoodsText => Item == null ? string.Empty : string.Join(", ", Item.BlockedFoodNames);
}

public sealed class FoodPlanRow
{
    public FoodPlanEntry Entry { get; init; }
    public BitmapImage Icon { get; init; }
}

public sealed class FoodShoppingRow
{
    public FoodShoppingItem Item { get; init; }
    public string Name { get; init; }
    public BitmapImage Icon { get; init; }
    public string UsageText => Item?.IsForFishSauce == true ? "Fish sauce" : "Food";
}

public sealed class FishSauceRow
{
    public FishSauceCost Cost { get; init; }
    public string Name { get; init; }
    public BitmapImage Icon { get; init; }

    public string MarketPriceText => Cost?.MarketPrice > 0m ? Cost.MarketPrice.ToString("N0", CultureInfo.CurrentCulture) : "-";
    public string CraftedCostText => Cost?.CraftedCost > 0m ? Cost.CraftedCost.ToString("N0", CultureInfo.CurrentCulture) : "-";
    public string UsedText => Cost == null || Cost.EffectivePrice <= 0m
        ? "No price"
        : (Cost.UsesCrafting ? "Craft" : "Buy") + " @ " + Cost.EffectivePrice.ToString("N0", CultureInfo.CurrentCulture);
}
