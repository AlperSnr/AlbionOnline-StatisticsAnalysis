using System.Windows;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
public partial class FoodCraftingControl
{
    public FoodCraftingControl()
    {
        InitializeComponent();
        DataContext = FoodCraftingBindings.Shared;
    }

    private async void FoodCraftingControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        await FoodCraftingBindings.Shared.EnsureLoadedAsync();
    }

    private async void FoodCraftingControl_OnUnloaded(object sender, RoutedEventArgs e)
    {
        await FoodCraftingBindings.Shared.SaveSettingsAsync();
    }
}
