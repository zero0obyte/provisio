using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Provisio.Pages;
using Provisio.Services;

namespace Provisio;

public partial class MainWindow : Window
{
    private bool _syncingThemeToggle;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Provisio";
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 760));

        App.ApplyTheme();
        SyncThemeToggle();

        UpdateChecker.UpdateAvailable += OnUpdateAvailable;
        PackageUpdateService.UpdatesFound += OnPackageUpdatesFound;
        Loc.LanguageChanged += OnLanguageChanged;

        // Every page is localized centrally when it lands in the frame, so pages
        // only need svc:L.* tags in XAML rather than their own wiring.
        ContentFrame.Navigated += (_, _) =>
        {
            if (ContentFrame.Content is FrameworkElement page)
            {
                L.Apply(page);
                page.Loaded += (_, _) => L.Apply(page);
            }
        };

        ContentFrame.Navigate(typeof(KitSelectionPage), null, new DrillInNavigationTransitionInfo());
        NavView.SelectedItem = NavView.MenuItems[0];

        NavView.Loaded += async (_, _) =>
        {
            L.Apply(NavView);
            await ShowWelcomeIfFirstRunAsync();
            StartPackageUpdateCheck();
        };
    }

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        ContentFrame.Navigate(pageType, parameter, new SlideNavigationTransitionInfo());
        SelectNavItem(pageType);
    }

    /// <summary>Keep the pane highlight in step with programmatic navigation.</summary>
    private void SelectNavItem(Type pageType)
    {
        var tag = pageType.Name switch
        {
            nameof(KitSelectionPage) => "kits",
            nameof(RecommendPage) => "recommend",
            nameof(SearchPage) => "search",
            nameof(ProfilesPage) => "profiles",
            nameof(KitEditorPage) => "editor",
            nameof(UpdatesPage) => "updates",
            nameof(SettingsPage) => "settings",
            _ => null
        };
        if (tag is null) return;

        foreach (var item in NavView.MenuItems.Concat(NavView.FooterMenuItems))
        {
            if (item is NavigationViewItem nvi && (nvi.Tag as string) == tag)
            {
                if (!ReferenceEquals(NavView.SelectedItem, nvi)) NavView.SelectedItem = nvi;
                return;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var page = (item.Tag as string) switch
        {
            "kits" => typeof(KitSelectionPage),
            "recommend" => typeof(RecommendPage),
            "search" => typeof(SearchPage),
            "profiles" => typeof(ProfilesPage),
            "editor" => typeof(KitEditorPage),
            "updates" => typeof(UpdatesPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(KitSelectionPage)
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page, null, new DrillInNavigationTransitionInfo());
    }

    // ---------------------------------------------------------------- theme

    private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingThemeToggle) return;
        SettingsService.Theme = ThemeToggle.IsOn ? "light" : "dark";
        SettingsService.Save();
        App.ApplyTheme();
    }

    /// <summary>Reflect the stored theme without re-entering the Toggled handler.</summary>
    public void SyncThemeToggle()
    {
        _syncingThemeToggle = true;
        ThemeToggle.IsOn = SettingsService.Theme == "light";
        _syncingThemeToggle = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            L.Apply(NavView);
            if (ContentFrame.Content is FrameworkElement page) L.Apply(page);
            RefreshInfoBars();
        });
    }

    // ------------------------------------------------------------- pop-ups

    /// <summary>One-time welcome shown right after a fresh installation.</summary>
    private async Task ShowWelcomeIfFirstRunAsync()
    {
        if (SettingsService.HasSeenWelcome) return;
        SettingsService.HasSeenWelcome = true;
        SettingsService.Save();

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = Loc.T("welcome.body"), TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text = Loc.T("welcome.hint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        var dlg = new ContentDialog
        {
            Title = Loc.T("welcome.title"),
            Content = new ScrollViewer { Content = body, MaxHeight = 420 },
            PrimaryButtonText = Loc.T("welcome.recommend"),
            CloseButtonText = Loc.T("welcome.start"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            NavigateTo(typeof(RecommendPage));
    }

    // ------------------------------------------------------- update banners

    private void OnUpdateAvailable(object? sender, UpdateInfo info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateInfoBar.Message = Loc.T("update.available", info.AppVersion, UpdateChecker.CurrentVersion, info.Notes);
            UpdateInfoBar.IsOpen = true;
            L.Apply(UpdateInfoBar);   // the action button is only realized once the bar opens
        });
    }

    private async void UpdateDownload_Click(object sender, RoutedEventArgs e)
    {
        var url = UpdateChecker.LastResult?.DownloadUrl;
        if (!string.IsNullOrWhiteSpace(url))
            await Launcher.LaunchUriAsync(new Uri(url));
    }

    /// <summary>Launch-time package update check (and optional silent install).</summary>
    public void StartPackageUpdateCheck()
    {
        if (!SettingsService.CheckPackageUpdates) return;
        _ = Task.Run(async () =>
        {
            if (SettingsService.AutoInstallPackageUpdates)
                await PackageUpdateService.UpgradeAllAsync(_ => { });
            else
                await PackageUpdateService.CheckAsync();
        });
    }

    private void OnPackageUpdatesFound(object? sender, List<PackageUpdate> updates)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var count = updates.Count;
            UpdatesBadge.Value = count;
            UpdatesBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PackageInfoBar.Message = Loc.T("updates.available.bar", count);
            PackageInfoBar.IsOpen = count > 0;
            L.Apply(PackageInfoBar);   // the action button is only realized once the bar opens
        });
    }

    private void RefreshInfoBars()
    {
        if (PackageInfoBar.IsOpen)
            PackageInfoBar.Message = Loc.T("updates.available.bar", PackageUpdateService.LastResult.Count);
        if (UpdateInfoBar.IsOpen && UpdateChecker.LastResult is { } info)
            UpdateInfoBar.Message = Loc.T("update.available", info.AppVersion, UpdateChecker.CurrentVersion, info.Notes);
    }

    private void ShowUpdates_Click(object sender, RoutedEventArgs e)
    {
        PackageInfoBar.IsOpen = false;
        NavigateTo(typeof(UpdatesPage));
    }
}

// Convenience alias so pages can call Windows.System.Launcher without long usings.
public static class Launcher
{
    public static Task<bool> LaunchUriAsync(Uri uri)
        => Windows.System.Launcher.LaunchUriAsync(uri).AsTask();
}
