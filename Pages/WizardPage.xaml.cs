using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Provisio.State;

namespace Provisio.Pages;

/// <summary>
/// One engine for every kit: walks the kit's pages, renders choices as
/// checkboxes (multi) or radio buttons (single), honors conditional pages.
/// Selections are only queued in AppState — nothing installs here.
///
/// Selections are committed the moment a box is ticked, so a branch condition
/// ("Do you want to host AI locally? → Yes") unlocks its follow-up page
/// immediately instead of only after leaving and re-entering the step.
/// </summary>
public partial class WizardPage : Page
{
    private int _kitIndex;
    private Kit _kit = null!;
    private List<KitPage> _pages = new();

    /// <summary>The step is tracked by page id, because the visible page list changes as answers change.</summary>
    private string _pageId = "";

    private bool _rendering;

    public WizardPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _kitIndex = e.Parameter is int i ? i : 0;
        if (_kitIndex < 0 || _kitIndex >= AppState.ActiveKits.Count)
        {
            App.MainWindow.NavigateTo(typeof(SummaryPage));
            return;
        }
        _kit = AppState.ActiveKits[_kitIndex];
        _pages = AppState.VisiblePages(_kit);
        _pageId = _pages.FirstOrDefault()?.Id ?? "";
        RenderPage();
    }

    private int CurrentIndex()
    {
        var index = _pages.FindIndex(p => p.Id == _pageId);
        return index < 0 ? 0 : index;
    }

    /// <summary>Recompute which pages apply; returns true if the visible set changed.</summary>
    private bool RefreshVisiblePages()
    {
        var updated = AppState.VisiblePages(_kit);
        var changed = updated.Count != _pages.Count
                      || !updated.Select(p => p.Id).SequenceEqual(_pages.Select(p => p.Id));
        _pages = updated;
        return changed;
    }

    private void RenderPage()
    {
        RefreshVisiblePages();
        if (_pages.Count == 0)
        {
            GoNext(); // kit has no applicable pages
            return;
        }

        var index = CurrentIndex();
        var page = _pages[index];
        _pageId = page.Id;

        KitHeader.Text = Loc.T("wizard.kitof", _kitIndex + 1, AppState.ActiveKits.Count, _kit.Name);
        QuestionText.Text = page.Question;
        StepText.Text = Loc.T("wizard.step", index + 1, _pages.Count);
        StepProgress.Value = 100.0 * (index + 1) / _pages.Count;
        NextButton.Content = index == _pages.Count - 1
            ? (_kitIndex == AppState.ActiveKits.Count - 1 ? Loc.T("wizard.review") : Loc.T("wizard.nextkit"))
            : Loc.T("common.next");

        _rendering = true;
        ChoicesList.Items.Clear();
        var selected = AppState.GetSelection(_kit.Id, page.Id);
        var radioGroup = "page_" + _kit.Id + "_" + page.Id;

        foreach (var choice in page.Choices)
        {
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = choice.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            if (!string.IsNullOrWhiteSpace(choice.Description))
                text.Children.Add(new TextBlock
                {
                    Text = choice.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });

            if (page.MultiSelect)
            {
                var cb = new CheckBox { Content = text, IsChecked = selected.Contains(choice.Id), Tag = choice.Id };
                cb.Checked += OnChoiceToggled;
                cb.Unchecked += OnChoiceToggled;
                ChoicesList.Items.Add(cb);
            }
            else
            {
                var rb = new RadioButton { Content = text, GroupName = radioGroup, IsChecked = selected.Contains(choice.Id), Tag = choice.Id };
                rb.Checked += OnChoiceToggled;
                rb.Unchecked += OnChoiceToggled;
                ChoicesList.Items.Add(rb);
            }
        }
        _rendering = false;
    }

    /// <summary>
    /// Commit immediately. Answering a branch question changes which pages exist,
    /// so the step counter and the Next button are refreshed straight away too.
    /// </summary>
    private void OnChoiceToggled(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;
        SaveCurrentPage();

        if (RefreshVisiblePages())
        {
            var index = CurrentIndex();
            StepText.Text = Loc.T("wizard.step", index + 1, _pages.Count);
            StepProgress.Value = 100.0 * (index + 1) / _pages.Count;
            NextButton.Content = index == _pages.Count - 1
                ? (_kitIndex == AppState.ActiveKits.Count - 1 ? Loc.T("wizard.review") : Loc.T("wizard.nextkit"))
                : Loc.T("common.next");
        }
    }

    private void SaveCurrentPage()
    {
        if (_pages.Count == 0) return;
        var page = _pages[CurrentIndex()];
        var chosen = new List<string>();
        foreach (var item in ChoicesList.Items)
        {
            switch (item)
            {
                case CheckBox { IsChecked: true } cb when cb.Tag is string id:
                    chosen.Add(id); break;
                case RadioButton { IsChecked: true } rb when rb.Tag is string id:
                    chosen.Add(id); break;
            }
        }
        AppState.SetSelection(_kit.Id, page.Id, chosen);
    }

    private void GoNext()
    {
        if (_kitIndex + 1 < AppState.ActiveKits.Count)
            App.MainWindow.NavigateTo(typeof(WizardPage), _kitIndex + 1);
        else
            App.MainWindow.NavigateTo(typeof(SummaryPage));
    }

    private void Step(int delta)
    {
        RefreshVisiblePages();
        var index = CurrentIndex() + delta;

        if (index >= _pages.Count) { GoNext(); return; }
        if (index < 0)
        {
            if (_kitIndex > 0) App.MainWindow.NavigateTo(typeof(WizardPage), _kitIndex - 1);
            else App.MainWindow.NavigateTo(typeof(KitSelectionPage));
            return;
        }

        _pageId = _pages[index].Id;
        RenderPage();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentPage();
        Step(+1);
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        // Skip = proceed with nothing selected on this page.
        if (_pages.Count > 0) AppState.ClearSelection(_kit.Id, _pages[CurrentIndex()].Id);
        Step(+1);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentPage();
        Step(-1);
    }
}
