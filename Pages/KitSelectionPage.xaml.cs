using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Provisio.State;

namespace Provisio.Pages;

/// <summary>Landing screen: all kits as cards; select one or more, then start the wizard.</summary>
public partial class KitSelectionPage : Page
{
    public KitSelectionPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
        KitService.KitsChanged += OnKitsChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        KitService.KitsChanged -= OnKitsChanged;
    }

    private void OnKitsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        var previouslyActive = AppState.ActiveKits.Select(k => k.Id).ToHashSet();
        KitsGrid.ItemsSource = KitService.Kits;
        KitsGrid.SelectedItems.Clear();
        foreach (var kit in KitService.Kits.Where(k => previouslyActive.Contains(k.Id)))
            KitsGrid.SelectedItems.Add(kit);
        UpdateCount();
    }

    private void KitsGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        // SelectionMode=Multiple + IsItemClickEnabled toggles selection automatically.
        SyncActiveKits();
    }

    private void SyncActiveKits()
    {
        AppState.ActiveKits.Clear();
        foreach (Kit kit in KitsGrid.SelectedItems)
            AppState.ActiveKits.Add(kit);
        UpdateCount();
    }

    private void UpdateCount()
    {
        var n = KitsGrid.SelectedItems.Count;
        SelectionCount.Text = n switch
        {
            0 => Loc.T("kits.none"),
            1 => Loc.T("kits.count.one"),
            _ => Loc.T("kits.count", n)
        };
    }

    private void Start_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SyncActiveKits();
        if (AppState.ActiveKits.Count == 0)
        {
            _ = ShowDialog(Loc.T("kits.nonetitle"), Loc.T("kits.nonebody"));
            return;
        }
        App.MainWindow.NavigateTo(typeof(WizardPage), 0); // start at first kit
    }

    private void Review_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SyncActiveKits();
        App.MainWindow.NavigateTo(typeof(SummaryPage));
    }

    private async Task ShowDialog(string title, string message)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Loc.T("common.ok"),
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
    }
}
