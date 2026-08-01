using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Services;

namespace Provisio.Pages;

/// <summary>
/// "Recommended Setup": an interactive questionnaire spanning the kit domains
/// (everyday use, privacy, AI, development, office, gaming, creative, tweaks).
/// The answers are compiled into one curated cross-kit list of suggestions that
/// the user can trim before it is queued for installation.
/// </summary>
public partial class RecommendPage : Page
{
    private enum Stage { Intro, Questions, Results }

    private Stage _stage = Stage.Intro;
    private List<RecQuestion> _questions = new();
    private int _index;

    /// <summary>questionId -> selected option ids.</summary>
    private readonly Dictionary<string, HashSet<string>> _answers = new();

    private readonly Dictionary<CheckBox, Recommendation> _resultBoxes = new();
    private List<Recommendation> _results = new();

    public RecommendPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _questions = RecommendationService.Questions();
        ShowIntro();
    }

    private HashSet<string> AnswersFor(string questionId)
    {
        if (!_answers.TryGetValue(questionId, out var set))
        {
            set = new HashSet<string>();
            _answers[questionId] = set;
        }
        return set;
    }

    // ---------- intro ----------

    private void ShowIntro()
    {
        _stage = Stage.Intro;
        Host.Children.Clear();
        ProgressPanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        RestartButton.Visibility = Visibility.Collapsed;
        TitleText.Text = Loc.T("rec.title");
        SubtitleText.Text = Loc.T("rec.subtitle");
        PrimaryButton.Content = Loc.T("rec.start");
        PrimaryButton.IsEnabled = true;

        var preview = new StackPanel { Spacing = 4 };
        foreach (var question in _questions)
        {
            preview.Children.Add(new TextBlock
            {
                Text = "· " + question.Text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }
        Host.Children.Add(Card(preview));
    }

    // ---------- questions ----------

    private void ShowQuestion()
    {
        _stage = Stage.Questions;
        _index = Math.Clamp(_index, 0, _questions.Count - 1);
        var question = _questions[_index];
        var chosen = AnswersFor(question.Id);

        ProgressPanel.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        RestartButton.Visibility = Visibility.Collapsed;
        StepText.Text = Loc.T("rec.step", _index + 1, _questions.Count);
        StepProgress.Value = 100.0 * (_index + 1) / _questions.Count;

        TitleText.Text = question.Text;
        SubtitleText.Text = question.MultiSelect ? Loc.T("rec.multi") : Loc.T("rec.single");
        PrimaryButton.Content = _index == _questions.Count - 1 ? Loc.T("rec.finish") : Loc.T("common.next");
        PrimaryButton.IsEnabled = true;

        Host.Children.Clear();
        var group = "rec_" + question.Id;

        foreach (var option in question.Options)
        {
            var label = new TextBlock { Text = option.Text, TextWrapping = TextWrapping.Wrap };
            FrameworkElement control;

            if (question.MultiSelect)
            {
                var cb = new CheckBox { Content = label, IsChecked = chosen.Contains(option.Id), Tag = option.Id };
                control = cb;
            }
            else
            {
                var rb = new RadioButton { Content = label, GroupName = group, IsChecked = chosen.Contains(option.Id), Tag = option.Id };
                control = rb;
            }

            Host.Children.Add(Card(control));
        }
    }

    private void SaveAnswers()
    {
        if (_stage != Stage.Questions) return;
        var question = _questions[_index];
        var chosen = AnswersFor(question.Id);
        chosen.Clear();

        foreach (var child in Host.Children)
        {
            if (child is not Border { Child: FrameworkElement inner }) continue;
            switch (inner)
            {
                case CheckBox { IsChecked: true } cb when cb.Tag is string id:
                    chosen.Add(id); break;
                case RadioButton { IsChecked: true } rb when rb.Tag is string id:
                    chosen.Add(id); break;
            }
        }
    }

    // ---------- results ----------

    private void ShowResults()
    {
        _stage = Stage.Results;
        _results = RecommendationService.Build(_questions, _answers);
        _resultBoxes.Clear();

        ProgressPanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Visible;
        RestartButton.Visibility = Visibility.Visible;
        TitleText.Text = Loc.T("rec.results.title");

        Host.Children.Clear();

        if (_results.Count == 0)
        {
            SubtitleText.Text = Loc.T("rec.results.empty");
            PrimaryButton.Content = Loc.T("rec.add");
            PrimaryButton.IsEnabled = false;
            return;
        }

        var kitCount = _results.Select(r => r.Kit.Id).Distinct().Count();
        SubtitleText.Text = Loc.T("rec.results.subtitle", _results.Count, kitCount);
        PrimaryButton.Content = Loc.T("rec.add");
        PrimaryButton.IsEnabled = true;

        foreach (var group in _results.GroupBy(r => r.Kit))
        {
            var body = new StackPanel { Spacing = 8 };

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new FontIcon { Glyph = group.Key.Icon, FontSize = 18 });
            header.Children.Add(new TextBlock
            {
                Text = group.Key.Name,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center
            });
            body.Children.Add(header);

            foreach (var rec in group)
            {
                var text = new StackPanel { Spacing = 2 };
                text.Children.Add(new TextBlock
                {
                    Text = rec.Choice.Name,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                });
                if (!string.IsNullOrWhiteSpace(rec.Choice.Description))
                    text.Children.Add(new TextBlock
                    {
                        Text = rec.Choice.Description,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                text.Children.Add(new TextBlock
                {
                    Text = Loc.T("rec.why", string.Join(", ", rec.Reasons)),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
                });

                var box = new CheckBox { Content = text, IsChecked = true };
                _resultBoxes[box] = rec;
                body.Children.Add(box);
            }

            Host.Children.Add(Card(body));
        }
    }

    private async Task AddResultsAsync()
    {
        var accepted = _resultBoxes.Where(kv => kv.Key.IsChecked == true).Select(kv => kv.Value).ToList();
        if (accepted.Count == 0) return;

        var count = RecommendationService.Queue(accepted);

        var dlg = new ContentDialog
        {
            Title = Loc.T("rec.added.title"),
            Content = Loc.T("rec.added.body", count),
            PrimaryButtonText = Loc.T("kits.review"),
            CloseButtonText = Loc.T("common.close"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            App.MainWindow.NavigateTo(typeof(SummaryPage));
    }

    // ---------- navigation ----------

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        switch (_stage)
        {
            case Stage.Intro:
                _index = 0;
                ShowQuestion();
                break;

            case Stage.Questions:
                SaveAnswers();
                if (_index + 1 < _questions.Count)
                {
                    _index++;
                    ShowQuestion();
                }
                else
                {
                    ShowResults();
                }
                break;

            case Stage.Results:
                await AddResultsAsync();
                break;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stage == Stage.Results)
        {
            _index = _questions.Count - 1;
            ShowQuestion();
            return;
        }

        SaveAnswers();
        if (_index > 0)
        {
            _index--;
            ShowQuestion();
        }
        else
        {
            ShowIntro();
        }
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        _answers.Clear();
        _index = 0;
        ShowIntro();
    }

    private static Border Card(FrameworkElement content) => new()
    {
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(8),
        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        Child = content
    };
}
