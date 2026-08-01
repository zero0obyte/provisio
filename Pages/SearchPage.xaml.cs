using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Provisio.State;

namespace Provisio.Pages;

/// <summary>Quick-filter across all kits/choices; results can be queued directly.</summary>
public partial class SearchPage : Page
{
    private record Result(Kit Kit, KitPage Page, KitChoice Choice)
    {
        public string Title => Choice.Name;
        public string Sub => $"{Kit.Name} · {Page.Question}" + (string.IsNullOrWhiteSpace(Choice.Description) ? "" : $" · {Choice.Description}");
        public bool Queued => AppState.IsSelected(Kit.Id, Page.Id, Choice.Id);
    }

    public SearchPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render("");
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            Render(sender.Text);
    }

    private void Render(string query)
    {
        ResultsList.Items.Clear();
        query = (query ?? "").Trim();
        var matches = KitService.Kits
            .SelectMany(k => k.Pages.SelectMany(p => p.Choices
                .Where(c => c.Action.Type != "none")
                .Select(c => new Result(k, p, c))))
            .Where(r => query.Length == 0
                        || r.Choice.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || r.Choice.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || r.Kit.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(80);

        foreach (var r in matches)
        {
            var btn = new Button
            {
                Content = r.Queued ? Loc.T("search.queued") + " ✓" : Loc.T("search.queue"),
                MinWidth = 90,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = !r.Queued
            };
            var captured = r;
            btn.Click += (_, _) =>
            {
                AppState.QueueChoice(captured.Kit, captured.Page.Id, captured.Choice.Id);
                btn.Content = Loc.T("search.queued") + " ✓";
                btn.IsEnabled = false;
            };

            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = r.Title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            text.Children.Add(new TextBlock
            {
                Text = r.Sub,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            var grid = new Grid { Padding = new Thickness(0, 6, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(text);
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);
            ResultsList.Items.Add(grid);
        }
    }
}

