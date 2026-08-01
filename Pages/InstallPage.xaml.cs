using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Provisio.State;
using Windows.Storage.Pickers;

namespace Provisio.Pages;

/// <summary>Batch install screen: overall progress, per-item status, live log, retry, export.</summary>
public partial class InstallPage : Page
{
    private readonly ObservableCollection<InstallItem> _items = new();
    private readonly StringBuilder _log = new();
    private bool _running;

    public InstallPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _items.Clear();
        foreach (var item in AppState.BuildQueue()) _items.Add(item);
        ItemsList.ItemsSource = _items;

        ProgressText.Text = $"0 / {_items.Count}";
        StatusLine.Text = _items.Count == 0
            ? Loc.T("install.nothing")
            : Loc.T("install.ready");

        if (!InstallEngine.IsAdmin && _items.Any(i => i.Action.RequiresAdmin || i.Action.Type == "script"))
        {
            AdminBar.Visibility = Visibility.Visible;
            AdminBar.IsOpen = true;
        }
        UpdateOverall();
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

    private void UpdateOverall()
    {
        var done = _items.Count(i => i.Status is InstallStatus.Done or InstallStatus.Failed or InstallStatus.Skipped);
        OverallProgress.Value = _items.Count == 0 ? 0 : 100.0 * done / _items.Count;
        ProgressText.Text = $"{done} / {_items.Count}";
    }

    private async Task RunBatchAsync(List<InstallItem> batch)
    {
        _running = true;
        StartButton.IsEnabled = false;
        RetryButton.Visibility = Visibility.Collapsed;

        foreach (var item in batch)
        {
            item.Detail = "";
            item.Status = InstallStatus.Queued;
        }

        if (RestorePointToggle.IsOn)
        {
            StatusLine.Text = Loc.T("install.creatingrestore");
            await InstallEngine.CreateRestorePointAsync(Log);
            RestorePointToggle.IsOn = false; // only once per run
        }

        foreach (var item in batch)
        {
            StatusLine.Text = Loc.T("install.running", item.Name);
            await InstallEngine.RunItemAsync(item, Log);
            UpdateOverall();
        }

        var failed = _items.Where(i => i.Status == InstallStatus.Failed).ToList();
        StatusLine.Text = failed.Count == 0
            ? Loc.T("install.alldone")
            : Loc.T("install.failed", failed.Count);
        RetryButton.Visibility = failed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DoneButton.Visibility = Visibility.Visible;
        StartButton.Visibility = Visibility.Collapsed;
        _running = false;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        await RunBatchAsync(_items.ToList());
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        await RunBatchAsync(_items.Where(i => i.Status == InstallStatus.Failed).ToList());
    }

    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        InstallEngine.RestartAsAdmin();
    }

    private async void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"provisio-install-{DateTime.Now:yyyyMMdd-HHmmss}"
        };
        picker.FileTypeChoices.Add("Install report", new List<string> { ".log" });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var report = new StringBuilder();
        report.AppendLine($"Provisio install report — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine(new string('=', 60));
        foreach (var item in _items)
            report.AppendLine($"[{item.StatusText,-12}] {item.Name} ({item.KitName})  {item.Detail}");
        report.AppendLine();
        report.AppendLine("Log:");
        report.AppendLine(_log.ToString());
        await Windows.Storage.FileIO.WriteTextAsync(file, report.ToString());
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        AppState.Reset();
        App.MainWindow.NavigateTo(typeof(KitSelectionPage));
    }
}
