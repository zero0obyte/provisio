using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Provisio.Models;
using Provisio.Services;
using Windows.Storage.Pickers;

namespace Provisio.Pages;

/// <summary>
/// Create kits from scratch or duplicate/edit existing ones, define pages,
/// choices and install actions, plus branching (condition page/choice).
/// Kits save as .provisio-kit JSON; import/export shares them as files.
/// </summary>
public partial class KitEditorPage : Page
{
    private Kit? _kit;
    private KitPage? _page;
    private KitChoice? _choice;

    /// <summary>Action types offered in the editor, in the order they appear in the dropdown.</summary>
    private static readonly string[] ActionTypes = { "none", "winget", "download", "msix", "shell", "script" };

    public KitEditorPage()
    {
        InitializeComponent();
        ChoiceTypeBox.ItemsSource = ActionTypes;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshKits(null);
        UpdateActionHint();
    }

    private void RefreshKits(string? selectId)
    {
        KitsList.ItemsSource = null;
        KitsList.ItemsSource = KitService.Kits;
        if (selectId is not null)
        {
            var kit = KitService.Find(selectId);
            if (kit is not null) KitsList.SelectedItem = kit;
        }
    }

    // ---------- kit level ----------

    private void KitsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _kit = KitsList.SelectedItem as Kit;
        EditorPanel.Visibility = _kit is null ? Visibility.Collapsed : Visibility.Visible;
        if (_kit is null) return;

        KitNameBox.Text = _kit.Name;
        KitIconBox.Text = _kit.Icon;
        KitDescBox.Text = _kit.Description;
        SavedText.Text = _kit.Builtin ? Loc.T("editor.builtin") : "";
        RefreshPages(null);
    }

    private void CommitKitFields()
    {
        if (_kit is null) return;
        _kit.Name = KitNameBox.Text.Trim();
        _kit.Icon = string.IsNullOrWhiteSpace(KitIconBox.Text) ? "" : KitIconBox.Text;
        _kit.Description = KitDescBox.Text.Trim();
    }

    private void NewKit_Click(object sender, RoutedEventArgs e)
    {
        var kit = new Kit
        {
            Id = "custom-" + Guid.NewGuid().ToString("N")[..6],
            Name = "New Kit",
            Icon = "",
            Description = "My custom kit",
            Pages = { new KitPage { Id = "page1", Question = "What do you want to install?" } }
        };
        KitService.SaveKit(kit);
        RefreshKits(kit.Id);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null) return;
        var copy = KitService.DuplicateKit(_kit);
        KitService.SaveKit(copy);
        RefreshKits(copy.Id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null) return;
        if (_kit.Builtin)
        {
            await Dialog(Loc.T("editor.builtin.title"), Loc.T("editor.builtin.body"));
            return;
        }
        var dlg = new ContentDialog
        {
            Title = Loc.T("editor.deletetitle"),
            Content = Loc.T("editor.deletebody", _kit.Name),
            PrimaryButtonText = Loc.T("common.delete"),
            CloseButtonText = Loc.T("common.cancel"),
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            KitService.DeleteKit(_kit);
            RefreshKits(null);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null) return;
        CommitKitFields();
        CommitPageFields();
        CommitChoiceFields();
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = _kit.Id
        };
        picker.FileTypeChoices.Add("Provisio kit", new List<string> { ".provisio-kit" });
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSaveFileAsync();
        if (file is not null)
            KitService.ExportKit(_kit, file.Path);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        // Community kits can run arbitrary commands — warn once, before the first import.
        if (!await ConfirmImportRiskAsync()) return;

        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".provisio-kit");
        picker.FileTypeFilter.Add(".json");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        var kit = KitService.ImportKit(file.Path);
        if (kit is null)
            await Dialog(Loc.T("editor.importfail.title"), Loc.T("editor.importfail.body"));
        else
            RefreshKits(kit.Id);
    }

    /// <summary>One-time security warning about community-made kits. Returns false if the user backs out.</summary>
    private async Task<bool> ConfirmImportRiskAsync()
    {
        if (SettingsService.HasSeenImportWarning) return true;

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = Loc.T("security.body"), TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock
        {
            Text = Loc.T("security.info"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        var dlg = new ContentDialog
        {
            Title = Loc.T("security.title"),
            Content = new ScrollViewer { Content = body, MaxHeight = 400 },
            PrimaryButtonText = Loc.T("security.ack"),
            CloseButtonText = Loc.T("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;

        SettingsService.HasSeenImportWarning = true;
        SettingsService.Save();
        return true;
    }

    // ---------- page level ----------

    private void RefreshPages(string? selectId)
    {
        if (_kit is null) return;
        PagesList.ItemsSource = null;
        PagesList.ItemsSource = _kit.Pages;
        _page = null;
        PagePanel.Visibility = Visibility.Collapsed;
        if (selectId is not null)
        {
            var page = _kit.Pages.FirstOrDefault(p => p.Id == selectId);
            if (page is not null) PagesList.SelectedItem = page;
        }
    }

    private void CommitPageFields()
    {
        if (_page is null) return;
        _page.Id = PageIdBox.Text.Trim();
        _page.Question = PageQuestionBox.Text.Trim();
        _page.MultiSelect = PageMultiBox.IsChecked == true;
        _page.ConditionPageId = NullIfEmpty(PageCondPageBox.Text);
        _page.ConditionChoiceId = NullIfEmpty(PageCondChoiceBox.Text);
    }

    private void PagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitPageFields();
        _page = PagesList.SelectedItem as KitPage;
        PagePanel.Visibility = _page is null ? Visibility.Collapsed : Visibility.Visible;
        if (_page is null) return;

        PageIdBox.Text = _page.Id;
        PageQuestionBox.Text = _page.Question;
        PageMultiBox.IsChecked = _page.MultiSelect;
        PageCondPageBox.Text = _page.ConditionPageId ?? "";
        PageCondChoiceBox.Text = _page.ConditionChoiceId ?? "";
        RefreshChoices(null);
    }

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null) return;
        var page = new KitPage { Id = "page" + (_kit.Pages.Count + 1), Question = "New question?" };
        _kit.Pages.Add(page);
        RefreshPages(page.Id);
    }

    private void RemovePage_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null || _page is null) return;
        _kit.Pages.Remove(_page);
        RefreshPages(null);
    }

    // ---------- choice level ----------

    private void RefreshChoices(string? selectId)
    {
        if (_page is null) return;
        ChoicesListView.ItemsSource = null;
        ChoicesListView.ItemsSource = _page.Choices;
        _choice = null;
        ChoicePanel.Visibility = Visibility.Collapsed;
        if (selectId is not null)
        {
            var choice = _page.Choices.FirstOrDefault(c => c.Id == selectId);
            if (choice is not null) ChoicesListView.SelectedItem = choice;
        }
    }

    private void CommitChoiceFields()
    {
        if (_choice is null) return;
        _choice.Id = ChoiceIdBox.Text.Trim();
        _choice.Name = ChoiceNameBox.Text.Trim();
        _choice.Description = ChoiceDescBox.Text.Trim();
        _choice.Action.Type = ChoiceTypeBox.SelectedItem as string ?? "none";
        _choice.Action.Target = ChoiceTargetBox.Text.Trim();
        _choice.Action.Args = ChoiceArgsBox.Text.Trim();
        _choice.Action.Source = ChoiceSourceBox.Text.Trim();
        _choice.Action.RequiresAdmin = ChoiceAdminBox.IsChecked == true;
        _choice.DependsOn = ChoiceDependsBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private void ChoicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitChoiceFields();
        _choice = ChoicesListView.SelectedItem as KitChoice;
        ChoicePanel.Visibility = _choice is null ? Visibility.Collapsed : Visibility.Visible;
        if (_choice is null) return;

        ChoiceIdBox.Text = _choice.Id;
        ChoiceNameBox.Text = _choice.Name;
        ChoiceDescBox.Text = _choice.Description;
        ChoiceTypeBox.SelectedItem = ActionTypes.Contains(_choice.Action.Type) ? _choice.Action.Type : "none";
        ChoiceTargetBox.Text = _choice.Action.Target;
        ChoiceArgsBox.Text = _choice.Action.Args;
        ChoiceSourceBox.Text = _choice.Action.Source;
        ChoiceDependsBox.Text = string.Join(", ", _choice.DependsOn);
        ChoiceAdminBox.IsChecked = _choice.Action.RequiresAdmin;
        UpdateActionHint();
    }

    private void AddChoice_Click(object sender, RoutedEventArgs e)
    {
        if (_page is null) return;
        var choice = new KitChoice { Id = "choice" + (_page.Choices.Count + 1), Name = "New choice" };
        _page.Choices.Add(choice);
        RefreshChoices(choice.Id);
    }

    private void RemoveChoice_Click(object sender, RoutedEventArgs e)
    {
        if (_page is null || _choice is null) return;
        _page.Choices.Remove(_choice);
        RefreshChoices(null);
    }

    private void ChoiceTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionHint();

    /// <summary>
    /// Show what the Target/Args fields mean for the selected action type — including which
    /// file formats a download URL may point at (.exe, .msi and the MSIX bundle family).
    /// </summary>
    private void UpdateActionHint()
    {
        var type = ChoiceTypeBox.SelectedItem as string ?? "none";

        ActionHint.Message = Loc.T($"editor.hint.{type}");
        ActionHint.Severity = type is "shell" or "script" ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;

        ChoiceTargetBox.Header = type switch
        {
            "winget" => Loc.T("editor.target.winget"),
            "download" => Loc.T("editor.target.download"),
            "msix" => Loc.T("editor.target.msix"),
            "shell" => Loc.T("editor.target.shell"),
            "script" => Loc.T("editor.target.script"),
            _ => Loc.T("editor.target")
        };

        ChoiceArgsBox.Header = type switch
        {
            "download" => Loc.T("editor.args.download"),
            "shell" => Loc.T("editor.args.shell"),
            _ => Loc.T("editor.args")
        };

        ChoiceSourceBox.Visibility = type == "winget" ? Visibility.Visible : Visibility.Collapsed;
        ChoiceArgsBox.Visibility = type is "download" or "shell" ? Visibility.Visible : Visibility.Collapsed;
        ActionHint.Visibility = Visibility.Visible;
    }

    // ---------- save ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_kit is null) return;
        CommitKitFields();
        CommitPageFields();
        CommitChoiceFields();
        if (string.IsNullOrWhiteSpace(_kit.Name)) _kit.Name = _kit.Id;
        KitService.SaveKit(_kit);
        SavedText.Text = Loc.T("editor.saved", _kit.Name, _kit.Pages.Count);
        RefreshKits(_kit.Id);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task Dialog(string title, string message)
    {
        await new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Loc.T("common.ok"),
            XamlRoot = XamlRoot
        }.ShowAsync();
    }
}
