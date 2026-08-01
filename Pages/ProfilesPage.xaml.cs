using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Services;

namespace Provisio.Pages;

/// <summary>Saved selection profiles: apply in one click, or delete.</summary>
public partial class ProfilesPage : Page
{
    public ProfilesPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
    }

    private void Render()
    {
        ProfilesList.Items.Clear();
        var profiles = ProfileService.LoadAll();
        if (profiles.Count == 0)
        {
            ProfilesList.Items.Add(new TextBlock
            {
                Text = Loc.T("profiles.empty"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        foreach (var p in profiles)
        {
            var apply = new Button { Content = Loc.T("profiles.apply"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
            var delete = new Button { Content = Loc.T("common.delete") };
            var captured = p;
            apply.Click += (_, _) =>
            {
                ProfileService.Apply(captured);
                App.MainWindow.NavigateTo(typeof(SummaryPage));
            };
            delete.Click += async (_, _) =>
            {
                var dlg = new ContentDialog
                {
                    Title = Loc.T("profiles.deletetitle"),
                    Content = Loc.T("profiles.deletebody", captured.Name),
                    PrimaryButtonText = Loc.T("common.delete"),
                    CloseButtonText = Loc.T("common.cancel"),
                    XamlRoot = XamlRoot
                };
                if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    ProfileService.Delete(captured);
                    Render();
                }
            };

            var count = captured.Selections.Values.Sum(pages => pages.Values.Sum(c => c.Count));
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = captured.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            text.Children.Add(new TextBlock
            {
                Text = Loc.T("profiles.items", count, captured.Created.ToString("yyyy-MM-dd HH:mm")),
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { apply, delete } };
            var grid = new Grid { Padding = new Thickness(0, 6, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(text);
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            ProfilesList.Items.Add(grid);
        }
    }
}

