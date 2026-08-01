using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Services;

namespace Provisio.Pages;

/// <summary>
/// Automatic package updates: lists installed software with a newer version available
/// (via winget) and updates the selected entries, one at a time, with a live log.
/// </summary>
public partial class UpdatesPage : Page
{
    private readonly ObservableCollection<PackageUpdate> _items = new();
    private readonly StringBuilder _log = new();
    private bool _running;

    public UpdatesPage()
    {
        InitializeComponent();
        UpdatesList.ItemsSource = _items;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (!PackageUpdateService.WingetAvailable())
        {
            NoWingetBar.IsOpen = true;
            SetBusy(false);
            CheckButton.IsEnabled = false;
            UpdateAllButton.IsEnabled = false;
            UpdateSelectedButton.IsEnabled = false;
            return;
        }

        // Show whatever the launch-time check already found, then refresh if it never ran.
        Fill(PackageUpdateService.LastResult);
        if (PackageUpdateService.LastChecked is null)
            await CheckAsync();
        else
            StatusText.Text = Loc.T(_items.Count == 0 ? "updates.none" : "updates.found", _items.Count);
    }

    private void Fill(IEnumerable<PackageUpdate> updates)
    {
        _items.Clear();
        foreach (var update in updates) _items.Add(update);
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        var any = _items.Count > 0;
        UpdateAllButton.IsEnabled = any && !_running;
        UpdateSelectedButton.IsEnabled = any && !_running;
        SelectAllBox.IsEnabled = any && !_running;
        CheckButton.IsEnabled = !_running;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsActive = busy;
        _running = busy;
        UpdateButtonState();
    }

    private void Log(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _log.AppendLine(line);
            LogText.Text = _log.ToString();
            LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
        });
    }

    // ---------- checking ----------

    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync();

    private async Task CheckAsync()
    {
        SetBusy(true);
        StatusText.Text = Loc.T("updates.checking");
        Log(Loc.T("updates.checking"));

        var updates = await PackageUpdateService.CheckAsync();

        Fill(updates);
        StatusText.Text = Loc.T(updates.Count == 0 ? "updates.none" : "updates.found", updates.Count);
        Log(StatusText.Text);
        SetBusy(false);
    }

    private void SelectAll_Toggled(object sender, RoutedEventArgs e)
    {
        var value = SelectAllBox.IsChecked == true;
        foreach (var item in _items) item.Selected = value;
    }

    // ---------- updating ----------

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
        => await RunAsync(_items.ToList());

    private async void UpdateSelected_Click(object sender, RoutedEventArgs e)
        => await RunAsync(_items.Where(i => i.Selected).ToList());

    private async Task RunAsync(List<PackageUpdate> batch)
    {
        if (_running || batch.Count == 0) return;
        SetBusy(true);

        var updated = 0;
        var failed = 0;
        foreach (var package in batch)
        {
            StatusText.Text = Loc.T("updates.running", package.Name);
            package.Status = Loc.T("common.updating");
            var ok = await PackageUpdateService.UpgradeAsync(package, Log);
            package.Status = ok ? Loc.T("common.done") : Loc.T("common.failed");
            if (ok) updated++; else failed++;
        }

        StatusText.Text = Loc.T("updates.finished", updated, failed);
        Log(StatusText.Text);
        SetBusy(false);

        // Re-check so the list reflects what is actually left.
        await CheckAsync();
    }
}
