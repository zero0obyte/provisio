using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Provisio.Services;

/// <summary>
/// Attached properties that tag XAML elements with a <see cref="Loc"/> key, plus a
/// tree walker that (re-)applies the current language. Tagging in XAML keeps the
/// markup declarative — <c>svc:L.Key="nav.kits"</c> — and switching language is
/// then a single <see cref="Apply"/> call over the page root.
/// </summary>
public class L : DependencyObject
{
    /// <summary>Main text: TextBlock.Text, or Content for buttons and nav items.</summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetKey(DependencyObject o) => (string)o.GetValue(KeyProperty);
    public static void SetKey(DependencyObject o, string value) => o.SetValue(KeyProperty, value);

    /// <summary>Header of a TextBox, ComboBox, ToggleSwitch or Expander.</summary>
    public static readonly DependencyProperty HeaderKeyProperty =
        DependencyProperty.RegisterAttached("HeaderKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetHeaderKey(DependencyObject o) => (string)o.GetValue(HeaderKeyProperty);
    public static void SetHeaderKey(DependencyObject o, string value) => o.SetValue(HeaderKeyProperty, value);

    /// <summary>PlaceholderText of a text input.</summary>
    public static readonly DependencyProperty PlaceholderKeyProperty =
        DependencyProperty.RegisterAttached("PlaceholderKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetPlaceholderKey(DependencyObject o) => (string)o.GetValue(PlaceholderKeyProperty);
    public static void SetPlaceholderKey(DependencyObject o, string value) => o.SetValue(PlaceholderKeyProperty, value);

    /// <summary>InfoBar.Title.</summary>
    public static readonly DependencyProperty TitleKeyProperty =
        DependencyProperty.RegisterAttached("TitleKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetTitleKey(DependencyObject o) => (string)o.GetValue(TitleKeyProperty);
    public static void SetTitleKey(DependencyObject o, string value) => o.SetValue(TitleKeyProperty, value);

    /// <summary>InfoBar.Message.</summary>
    public static readonly DependencyProperty MessageKeyProperty =
        DependencyProperty.RegisterAttached("MessageKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetMessageKey(DependencyObject o) => (string)o.GetValue(MessageKeyProperty);
    public static void SetMessageKey(DependencyObject o, string value) => o.SetValue(MessageKeyProperty, value);

    /// <summary>ToggleSwitch.OnContent.</summary>
    public static readonly DependencyProperty OnKeyProperty =
        DependencyProperty.RegisterAttached("OnKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetOnKey(DependencyObject o) => (string)o.GetValue(OnKeyProperty);
    public static void SetOnKey(DependencyObject o, string value) => o.SetValue(OnKeyProperty, value);

    /// <summary>ToggleSwitch.OffContent.</summary>
    public static readonly DependencyProperty OffKeyProperty =
        DependencyProperty.RegisterAttached("OffKey", typeof(string), typeof(L), new PropertyMetadata(null));
    public static string GetOffKey(DependencyObject o) => (string)o.GetValue(OffKeyProperty);
    public static void SetOffKey(DependencyObject o, string value) => o.SetValue(OffKeyProperty, value);

    /// <summary>Walk a subtree and set every tagged string to the current language.</summary>
    public static void Apply(DependencyObject? root)
    {
        if (root is null) return;
        ApplyToOne(root);

        // Some controls hold tagged children in properties rather than in the visual tree
        // (menu items before the pane opens, an InfoBar's action button before it is shown).
        if (root is NavigationView nav)
        {
            foreach (var item in nav.MenuItems) if (item is DependencyObject d) Apply(d);
            foreach (var item in nav.FooterMenuItems) if (item is DependencyObject d) Apply(d);
            Apply(nav.PaneFooter as DependencyObject);
            Apply(nav.Content as DependencyObject);
        }
        else if (root is InfoBar infoBar)
        {
            Apply(infoBar.ActionButton as DependencyObject);
            Apply(infoBar.Content as DependencyObject);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++) Apply(VisualTreeHelper.GetChild(root, i));
    }

    private static void ApplyToOne(DependencyObject o)
    {
        var key = GetKey(o);
        if (!string.IsNullOrEmpty(key))
        {
            switch (o)
            {
                case TextBlock tb: tb.Text = Loc.T(key); break;
                case ContentControl cc: cc.Content = Loc.T(key); break;
                case TextBox box: box.Text = Loc.T(key); break;
            }
        }

        var header = GetHeaderKey(o);
        if (!string.IsNullOrEmpty(header))
        {
            switch (o)
            {
                case TextBox tb: tb.Header = Loc.T(header); break;
                case ComboBox cb: cb.Header = Loc.T(header); break;
                case ToggleSwitch ts: ts.Header = Loc.T(header); break;
                case Expander ex: ex.Header = Loc.T(header); break;
                case AutoSuggestBox asb: asb.Header = Loc.T(header); break;
                case NumberBox nb: nb.Header = Loc.T(header); break;
            }
        }

        var placeholder = GetPlaceholderKey(o);
        if (!string.IsNullOrEmpty(placeholder))
        {
            switch (o)
            {
                case TextBox tb: tb.PlaceholderText = Loc.T(placeholder); break;
                case AutoSuggestBox asb: asb.PlaceholderText = Loc.T(placeholder); break;
                case ComboBox cb: cb.PlaceholderText = Loc.T(placeholder); break;
            }
        }

        if (o is InfoBar bar)
        {
            var title = GetTitleKey(o);
            if (!string.IsNullOrEmpty(title)) bar.Title = Loc.T(title);
            var message = GetMessageKey(o);
            if (!string.IsNullOrEmpty(message)) bar.Message = Loc.T(message);
        }

        if (o is ToggleSwitch toggle)
        {
            var on = GetOnKey(o);
            if (!string.IsNullOrEmpty(on)) toggle.OnContent = Loc.T(on);
            var off = GetOffKey(o);
            if (!string.IsNullOrEmpty(off)) toggle.OffContent = Loc.T(off);
        }
    }
}
