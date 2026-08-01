using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Provisio.State;

namespace Provisio.Pages;

/// <summary>Final review: everything queued, grouped by kit, with uncheck + dry-run preview.</summary>
public partial class SummaryPage : Page
{
    // checkbox -> (kitId, pageId, choiceId) so unchecking updates AppState
    private readonly Dictionary<CheckBox, (string kit, string page, string choice)> _map = new();

    public SummaryPage()
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
        GroupsHost.Children.Clear();
        _map.Clear();
        int total = 0;

        foreach (var kit in AppState.ActiveKits)
        {
            var kitPanel = new StackPanel { Spacing = 6 };
            bool any = false;

            foreach (var page in kit.Pages)
            {
                var set = AppState.GetSelection(kit.Id, page.Id);
                foreach (var choice in page.Choices)
                {
                    if (!set.Contains(choice.Id) || choice.Action.Type == "none") continue;
                    any = true;
                    total++;
                    var cb = new CheckBox { IsChecked = true };
                    var text = new StackPanel { Spacing = 2 };
                    text.Children.Add(new TextBlock { Text = choice.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
                    text.Children.Add(new TextBlock
                    {
                        Text = $"{page.Question} · {InstallEngine.DescribeCommand(choice.Action)}",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    cb.Content = text;
                    cb.Checked += OnToggled;
                    cb.Unchecked += OnToggled;
                    _map[cb] = (kit.Id, page.Id, choice.Id);
                    kitPanel.Children.Add(cb);
                }
            }

            if (!any) continue;

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new FontIcon { Glyph = kit.Icon, FontSize = 18 });
            header.Children.Add(new TextBlock { Text = kit.Name, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center });

            var card = new Border
            {
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 10, Children = { header, kitPanel } }
            };
            GroupsHost.Children.Add(card);
        }

        if (total == 0)
        {
            GroupsHost.Children.Add(new TextBlock
            {
                Text = Loc.T("summary.empty"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }
        InstallButton.IsEnabled = total > 0;
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || !_map.TryGetValue(cb, out var key)) return;
        var set = AppState.GetSelection(key.kit, key.page);
        if (cb.IsChecked == true) set.Add(key.choice);
        else set.Remove(key.choice);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.ActiveKits.Count > 0)
            App.MainWindow.NavigateTo(typeof(WizardPage), AppState.ActiveKits.Count - 1);
        else
            App.MainWindow.NavigateTo(typeof(KitSelectionPage));
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var commands = AppState.PreviewCommands();
        var view = new ScrollViewer
        {
            MaxHeight = 420,
            Content = new TextBlock
            {
                Text = commands.Count == 0 ? Loc.T("summary.nothingqueued") : string.Join(Environment.NewLine + Environment.NewLine, commands),
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            }
        };
        var dlg = new ContentDialog
        {
            Title = Loc.T("summary.previewtitle"),
            Content = view,
            CloseButtonText = Loc.T("common.close"),
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { PlaceholderText = Loc.T("summary.profilename"), Width = 300 };
        var dlg = new ContentDialog
        {
            Title = Loc.T("summary.profiletitle"),
            Content = box,
            PrimaryButtonText = Loc.T("common.save"),
            CloseButtonText = Loc.T("common.cancel"),
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
        {
            ProfileService.SaveCurrent(box.Text.Trim());
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindow.NavigateTo(typeof(InstallPage));
    }
}

