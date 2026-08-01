using Microsoft.UI.Xaml;
using Provisio.Services;

namespace Provisio;

public partial class App : Application
{
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SettingsService.Load();
        KitService.LoadAll();
        MainWindow = new MainWindow();
        MainWindow.Activate();
        _ = UpdateChecker.CheckAsync();
    }

    /// <summary>Apply the theme from settings: system default, light, or dark.</summary>
    public static void ApplyTheme()
    {
        if (MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = SettingsService.Theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
