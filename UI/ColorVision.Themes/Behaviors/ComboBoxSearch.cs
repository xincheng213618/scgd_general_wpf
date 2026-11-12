using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Themes;

/// <summary>Searches a short choice list without filtering out its selected value.</summary>
public static class ComboBoxSearch
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ComboBoxSearch), new PropertyMetadata(false, EnabledChanged));
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.RegisterAttached(
        "Watermark", typeof(string), typeof(ComboBoxSearch), new PropertyMetadata(string.Empty));
    private static readonly DependencyProperty SearchBoxProperty = DependencyProperty.RegisterAttached(
        "SearchBox", typeof(TextBox), typeof(ComboBoxSearch));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    public static string GetWatermark(DependencyObject obj) => (string)obj.GetValue(WatermarkProperty);
    public static void SetWatermark(DependencyObject obj, string value) => obj.SetValue(WatermarkProperty, value);

    private static void EnabledChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not ComboBox combo) return;
        if ((bool)args.OldValue)
        {
            combo.DropDownOpened -= Opened;
            combo.DropDownClosed -= Closed;
            combo.Unloaded -= Unloaded;
            Detach(combo);
        }
        if ((bool)args.NewValue)
        {
            combo.IsTextSearchEnabled = false;
            combo.DropDownOpened += Opened;
            combo.DropDownClosed += Closed;
            combo.Unloaded += Unloaded;
        }
    }

    private static void Opened(object? sender, EventArgs args)
    {
        if (sender is not ComboBox combo) return;
        Detach(combo);
        if (combo.Template.FindName("PART_SearchBox", combo) is not TextBox search) return;
        combo.SetValue(SearchBoxProperty, search);
        search.Tag = combo;
        search.Text = string.Empty;
        search.TextChanged += SearchChanged;
        combo.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (combo.IsDropDownOpen) search.Focus();
        }));
    }

    private static void SearchChanged(object sender, TextChangedEventArgs args)
    {
        if (sender is not TextBox { Tag: ComboBox combo } search) return;
        string query = search.Text.Trim();
        foreach (object item in combo.Items)
        {
            if (combo.ItemContainerGenerator.ContainerFromItem(item) is not ComboBoxItem container) continue;
            object? label = string.IsNullOrEmpty(combo.DisplayMemberPath) ? item : item.GetType().GetProperty(combo.DisplayMemberPath)?.GetValue(item);
            string text = Convert.ToString(label, CultureInfo.CurrentUICulture) ?? string.Empty;
            container.SetCurrentValue(UIElement.VisibilityProperty, text.Contains(query, StringComparison.CurrentCultureIgnoreCase) ? Visibility.Visible : Visibility.Collapsed);
        }
    }

    private static void Closed(object? sender, EventArgs args) { if (sender is ComboBox combo) Detach(combo); }
    private static void Unloaded(object sender, RoutedEventArgs args) { if (sender is ComboBox combo) Detach(combo); }
    private static void Detach(ComboBox combo)
    {
        if (combo.GetValue(SearchBoxProperty) is TextBox search)
        {
            search.TextChanged -= SearchChanged;
            search.Tag = null;
            search.Clear();
            combo.ClearValue(SearchBoxProperty);
        }
        foreach (object item in combo.Items)
            if (combo.ItemContainerGenerator.ContainerFromItem(item) is ComboBoxItem container)
                container.ClearValue(UIElement.VisibilityProperty);
    }
}
