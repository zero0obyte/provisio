using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Services;

namespace Provisio.Pages;

/// <summary>Theme, language, package updates, app update feed, offline cache and stored data.</summary>
public partial class SettingsPage : Page
{
    private bool _loaded;

    /// <summary>Theme values in the order they appear in the dropdown.</summary>
    private static readonly string[] Themes = { "system", "light", "dark" };

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _loaded = false;

        ThemeBox.ItemsSource = Themes.Select(t => Loc.T($"settings.theme.{t}")).ToList();
        var themeIndex = Array.IndexOf(Themes, SettingsService.Theme);
        ThemeBox.SelectedIndex = themeIndex < 0 ? 0 : themeIndex;

        LanguageBox.ItemsSource = Loc.Languages;
        LanguageBox.SelectedItem = Loc.Languages.FirstOrDefault(l => l.Code == Loc.Current) ?? Loc.Languages[0];

        PackageCheckToggle.IsOn = SettingsService.CheckPackageUpdates;
        PackageAutoToggle.IsOn = SettingsService.AutoInstallPackageUpdates;
        CacheToggle.IsOn = SettingsService.CacheInstallers;
        UpdateUrlBox.Text = SettingsService.UpdateUrl;
        VersionText.Text = Loc.T("settings.version", UpdateChecker.CurrentVersion);
        CachePathText.Text = Loc.T("settings.cachepath", SettingsService.InstallerCacheFolder);
        DataStatusText.Text = "";

        _loaded = true;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        SettingsService.UpdateUrl = UpdateUrlBox.Text.Trim();
        SettingsService.Save();
    }

    // ---------- appearance & language ----------

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var index = ThemeBox.SelectedIndex;
        if (index < 0 || index >= Themes.Length) return;
        SettingsService.Theme = Themes[index];
        SettingsService.Save();
        App.ApplyTheme();
        App.MainWindow.SyncThemeToggle();
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (LanguageBox.SelectedItem is not Loc.Language language) return;
        SettingsService.Language = language.Code;
        SettingsService.Save();
        Loc.SetLanguage(language.Code);

        // Strings owned by this page's code (not by the XAML tags) need a refresh too.
        _loaded = false;
        var selected = ThemeBox.SelectedIndex;
        ThemeBox.ItemsSource = Themes.Select(t => Loc.T($"settings.theme.{t}")).ToList();
        ThemeBox.SelectedIndex = selected;
        VersionText.Text = Loc.T("settings.version", UpdateChecker.CurrentVersion);
        CachePathText.Text = Loc.T("settings.cachepath", SettingsService.InstallerCacheFolder);
        DataStatusText.Text = "";
        _loaded = true;
    }

    // ---------- package updates ----------

    private void PackageCheckToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SettingsService.CheckPackageUpdates = PackageCheckToggle.IsOn;
        SettingsService.Save();
        if (PackageCheckToggle.IsOn) App.MainWindow.StartPackageUpdateCheck();
    }

    private void PackageAutoToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SettingsService.AutoInstallPackageUpdates = PackageAutoToggle.IsOn;
        // Automatic installs are pointless without the launch-time check.
        if (PackageAutoToggle.IsOn && !PackageCheckToggle.IsOn) PackageCheckToggle.IsOn = true;
        SettingsService.Save();
    }

    private void OpenUpdates_Click(object sender, RoutedEventArgs e)
        => App.MainWindow.NavigateTo(typeof(UpdatesPage));

    // ---------- app updates ----------

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.UpdateUrl = UpdateUrlBox.Text.Trim();
        SettingsService.Save();
        await UpdateChecker.CheckAsync();
        var dlg = new ContentDialog
        {
            Title = Loc.T("settings.updatecheck.title"),
            Content = string.IsNullOrWhiteSpace(SettingsService.UpdateUrl)
                ? Loc.T("settings.updatecheck.nourl")
                : UpdateChecker.LastResult is not null
                    ? Loc.T("settings.updatecheck.found", UpdateChecker.LastResult.AppVersion)
                    : Loc.T("settings.updatecheck.none"),
            CloseButtonText = Loc.T("common.ok"),
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
    }

    // ---------- offline cache ----------

    private void CacheToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SettingsService.CacheInstallers = CacheToggle.IsOn;
        SettingsService.Save();
    }

    // ---------- data ----------

    private async void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(SettingsService.DataFolder);
        await Windows.System.Launcher.LaunchFolderAsync(folder);
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var removed = 0;
        try
        {
            foreach (var file in Directory.GetFiles(SettingsService.InstallerCacheFolder))
            {
                try { File.Delete(file); removed++; } catch { /* file in use */ }
            }
        }
        catch { /* folder missing */ }
        DataStatusText.Text = $"{Loc.T("settings.data.cleared")} ({removed})";
    }

    private void ResetPopups_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.HasSeenWelcome = false;
        SettingsService.HasSeenImportWarning = false;
        SettingsService.Save();
        DataStatusText.Text = Loc.T("settings.data.reset");
    }
}
