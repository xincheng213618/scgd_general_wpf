using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Settings
{
    /// <summary>A read-only projection; metadata is never treated as editable configuration.</summary>
    internal sealed class ImageSettingsInformationView : StackPanel
    {
        private readonly ImageView _view;
        private readonly bool _technical;
        private readonly StackPanel _rows = new();
        private string _copyText = string.Empty;

        public ImageSettingsInformationView(ImageView view, bool technical)
        {
            _view = view;
            _technical = technical;
            StackPanel actions = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 10, 14, 6) };
            Button refresh = new() { Content = SettingsText.Refresh, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 5, 10, 5) };
            Button copy = new() { Content = SettingsText.Copy, Padding = new Thickness(10, 5, 10, 5) };
            refresh.SetResourceReference(StyleProperty, "ButtonDefault");
            copy.SetResourceReference(StyleProperty, "ButtonDefault");
            refresh.Click += (_, _) => Refresh();
            copy.Click += (_, _) => { if (_copyText.Length > 0) Clipboard.SetText(_copyText); };
            actions.Children.Add(refresh);
            actions.Children.Add(copy);
            Children.Add(actions);
            Children.Add(_rows);
            Refresh();
        }

        private void Refresh()
        {
            _rows.Children.Clear();
            StringBuilder text = new();
            IEnumerable<(string Name, string Value)> items = _technical
                ? _view.Config.GetPropertyEntries().OrderBy(entry => entry.Scope).ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => ($"{entry.Key}\n{ImageViewConfig.GetScopeDisplayName(entry.Scope)} · {entry.Owner}", ImageViewConfig.FormatPropertyValue(entry.Value)))
                    .Concat(_view.IEditorToolFactory.IImageOpens.GroupBy(item => item.Value.GetType())
                        .Select(group => (group.Key.Name, string.Join(", ", group.Select(item => item.Key).OrderBy(key => key)))))
                : Summary();
            foreach (var item in items)
            {
                text.AppendLine($"{item.Name}: {item.Value}");
                Grid row = new() { Margin = new Thickness(14, 8, 14, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                TextBlock label = new() { Text = item.Name, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 16, 0) };
                label.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                TextBox value = new() { Text = item.Value, IsReadOnly = true, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Padding = new Thickness(0), TextWrapping = TextWrapping.Wrap, FontSize = 13 };
                value.SetResourceReference(TextBox.ForegroundProperty, "GlobalTextBrush");
                Grid.SetColumn(value, 1);
                row.Children.Add(label);
                row.Children.Add(value);
                _rows.Children.Add(row);
            }
            _copyText = text.ToString();
            if (_rows.Children.Count == 0) _rows.Children.Add(new TextBlock { Text = SettingsText.NoImage, Margin = new Thickness(14) });
        }

        private IEnumerable<(string, string)> Summary()
        {
            var config = _view.Config;
            string Value(string key) => config.Properties.TryGetValue(key, out var value) ? ImageViewConfig.FormatPropertyValue(value) : "—";
            yield return (SettingsText.ImageName, string.IsNullOrEmpty(config.FilePath) ? (_view.ViewBitmapSource == null ? SettingsText.NoImage : SettingsText.CurrentImage) : Value(ImageViewPropertyKeys.FileName));
            yield return (SettingsText.Dimensions, $"{Value(ImageViewPropertyKeys.ImageWidth)} × {Value(ImageViewPropertyKeys.ImageHeight)} px");
            yield return (SettingsText.Format, Value(ImageViewPropertyKeys.PixelFormat));
            yield return (SettingsText.Depth, Value(ImageViewPropertyKeys.Depth));
            yield return (SettingsText.Channels, Value(ImageViewPropertyKeys.Channel));
            yield return (SettingsText.FileSize, Value(ImageViewPropertyKeys.FileSize));
            yield return (SettingsText.FilePath, Value(ImageViewPropertyKeys.FilePath));
        }
    }
}
