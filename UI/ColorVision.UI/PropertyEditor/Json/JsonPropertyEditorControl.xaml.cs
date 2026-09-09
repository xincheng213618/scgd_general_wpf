using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.UI.PropertyEditor.Json;

/// <summary>Edits existing JSON values with optional schema hints, without inventing fields or defaults.</summary>
public partial class JsonPropertyEditorControl : UserControl
{
    private JObject? _document;
    private string? _originalJson;
    private JsonEditorSchemaDocument? _schema;
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _expansion = new(StringComparer.Ordinal);
    private readonly List<ParameterGroup> _groups = [];
    private bool _loading;
    private readonly List<ParameterEntry> _entries = [];
    private readonly Dictionary<string, string> _drafts = new(StringComparer.Ordinal);
    private sealed record ParameterEntry(string Path, string Name, string Group, string GroupName, string SearchText);

    private sealed record ParameterGroup(Expander Element, string Path, string SearchText);

    public JsonPropertyEditorControl() => InitializeComponent();

    public event EventHandler<string>? JsonValueChanged;
    public event EventHandler? ValidationStateChanged;
    public void FocusSearch() => SearchTextBox.Focus();
    public bool CanEdit => _document != null;
    public bool HasValidationErrors => _errors.Count > 0 || !CanEdit;

    public void SetJson(string json) => SetJson(json, null, null);

    public void SetJson(string json, string? schemaJson, string? schemaTitle = null)
    {
        bool hadNavigation = _entries.Count > 0 && GroupSelector.SelectedIndex >= 0;
        var previousGroup = (GroupSelector.SelectedItem as ComboBoxItem)?.Tag;
        _loading = true;
        try
        {
            _originalJson = json;
            _document = null;
            _schema = JsonEditorSchemaDocument.TryParse(schemaJson, schemaTitle);
            _errors.Clear();
            _drafts.Clear();
            _entries.Clear();
            _groups.Clear();
            PropertyPanel.Children.Clear();
            GroupSelector.Items.Clear();
            GroupSelector.Items.Add(new ComboBoxItem { Content = "全部分组" });
            GroupSelector.SelectedIndex = 0;
            _document = ParseToken(json) as JObject;
            if (_document == null)
                throw new JsonReaderException("属性视图需要 JSON 对象。数组等内容请使用 JSON 视图。");

            foreach (var property in _document.Properties())
            {
                var group = property.Value is JObject or JArray ? property.Value.Path : "";
                IndexEntries(property.Value, property.Name, group, group.Length > 0 ? Title(group, property.Name) : "常规参数");
            }
            foreach (var grouping in _entries.GroupBy(entry => entry.Group))
                GroupSelector.Items.Add(new ComboBoxItem { Content = $"{grouping.First().GroupName}  ·  {grouping.Count()}", Tag = grouping.Key });
            if (hadNavigation && GroupSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, previousGroup)) is { } previousSelection)
                GroupSelector.SelectedItem = previousSelection;
            DetailPathText.Text = _schema?.Title ?? "参数说明";
            DetailText.Text = _schema == null
                ? "选择参数查看路径。未提供字段说明时，仍可编辑现有参数。"
                : "选择参数查看完整说明、范围和示例。";
            DetailPathText.ToolTip = _schema?.SourceSummary;
            if (_document.Count == 0)
                PropertyPanel.Children.Add(new TextBlock { Text = "对象中还没有参数，请在 JSON 视图添加。", Margin = new Thickness(0, 16, 0, 16) });
        }
        catch (JsonException ex)
        {
            _document = null;
            PropertyPanel.Children.Clear();
            ShowError(ex.Message);
        }
        finally
        {
            _loading = false;
            ApplyFilter();
            UpdateErrors();
        }
    }

    public void ResetNavigation()
    {
        _loading = true;
        try { SearchTextBox.Clear(); GroupSelector.SelectedIndex = -1; _expansion.Clear(); PropertyScroller.ScrollToTop(); }
        finally { _loading = false; }
    }

    private static JToken ParseToken(string text)
    {
        try { using var syntax = System.Text.Json.JsonDocument.Parse(text); }
        catch (System.Text.Json.JsonException ex) { throw new JsonReaderException(ex.Message, ex); }
        using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.Load(reader);
        if (reader.Read()) throw new JsonReaderException("JSON 值后存在多余内容。");
        var values = token is JContainer container ? container.Descendants() : new[] { token };
        if (values.Any(value => value.Type == JTokenType.Float && !double.IsFinite((double)value)))
            throw new JsonReaderException("数值超出可编辑的有限数范围，请使用 JSON 视图。");
        return token;
    }

    private string Title(string path, string fallback) => _schema?.FindNode(path)?.GetTitleOrFallback(fallback) ?? fallback;

    private FrameworkElement CreateRow(JToken token, string name)
    {
        var path = token.Path;
        var schema = _schema?.FindNode(path);
        var title = Title(path, name);
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5), MinHeight = 32 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 115 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star), MinWidth = 130 });
        var label = new StackPanel { Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Medium });
        if (!string.IsNullOrWhiteSpace(schema?.Description))
            label.Children.Add(new TextBlock { Text = schema.Description, TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .62, Margin = new Thickness(0, 3, 0, 0) });
        if (SearchTextBox.Text.Length > 0 || path.Count(c => c == '.') > 1 || path.Contains('['))
            label.Children.Add(new TextBlock { Text = path, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 11, Opacity = .55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        label.ToolTip = schema?.BuildHint(path) ?? path;
        grid.Children.Add(label);

        var editorHost = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var errorText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        errorText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        var editor = CreateEditor(token, schema, errorText);
        editor.Tag = path;
        AutomationProperties.SetName(editor, title);
        editor.GotKeyboardFocus += (_, _) =>
        {
            DetailPathText.Text = path;
            DetailText.Text = schema?.BuildHint(path) ?? $"JSON 路径：{path}";
        };
        editorHost.Children.Add(editor);
        editorHost.Children.Add(errorText);
        if (!string.IsNullOrWhiteSpace(schema?.Unit))
            editorHost.Children.Add(new TextBlock { Text = schema.Unit, FontSize = 11, Opacity = .6, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(editorHost, 1);
        grid.Children.Add(editorHost);
        var border = new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    private FrameworkElement CreateEditor(JToken token, JsonEditorSchemaNode? schema, TextBlock error)
    {
        var path = token.Path;
        if (schema?.HasEnum == true)
        {
            var combo = new ComboBox { MinHeight = 30, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            bool found = false;
            foreach (var item in schema.EnumItems)
            {
                var option = new ComboBoxItem { Content = item.DisplayText, Tag = item };
                combo.Items.Add(option);
                if (item.Matches(token)) { combo.SelectedItem = option; found = true; }
            }
            if (!found)
            {
                var current = new ComboBoxItem { Content = $"{token}（当前值）", IsEnabled = false };
                combo.Items.Insert(0, current);
                combo.SelectedItem = current;
            }
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem { Tag: JsonEditorSchemaEnumItem item })
                    UpdateValue(path, item.ToObject() is { } value ? JToken.FromObject(value) : JValue.CreateNull());
            };
            return combo;
        }
        if (token.Type == JTokenType.Boolean)
        {
            var check = new CheckBox { IsChecked = token.Value<bool>(), Content = token.Value<bool>() ? "已启用" : "已关闭", MinHeight = 30, VerticalContentAlignment = VerticalAlignment.Center };
            check.Checked += (_, _) => { check.Content = "已启用"; UpdateValue(path, new JValue(true)); };
            check.Unchecked += (_, _) => { check.Content = "已关闭"; UpdateValue(path, new JValue(false)); };
            return check;
        }
        if (token is JArray booleans && booleans.Count > 0 && booleans.All(item => item.Type == JTokenType.Boolean))
        {
            var panel = new WrapPanel();
            for (int i = 0; i < booleans.Count; i++)
            {
                var itemPath = booleans[i].Path;
                var check = new CheckBox { Content = $"[{i}]", IsChecked = booleans[i].Value<bool>(), Margin = new Thickness(0, 4, 14, 4), ToolTip = itemPath };
                AutomationProperties.SetName(check, itemPath);
                check.Checked += (_, _) => UpdateValue(itemPath, new JValue(true));
                check.Unchecked += (_, _) => UpdateValue(itemPath, new JValue(false));
                panel.Children.Add(check);
            }
            return panel;
        }
        var box = new TextBox
        {
            Text = token.Type == JTokenType.String ? token.Value<string>() ?? "" : token.ToString(Formatting.None),
            MinHeight = 30, Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = token.Type is JTokenType.Integer or JTokenType.Float ? TextAlignment.Right : TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap, ToolTip = token is JArray ? "使用 JSON 数组格式，例如 [60, 40]。字符串需要双引号。" : schema?.BuildHint(path)
        };
        box.SetResourceReference(StyleProperty, "TextBox.Small");
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            box.MaxWidth = 200;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.MinWidth = 120;
        }
        box.TextChanged += (_, _) =>
        {
            _drafts[path] = box.Text;
            try
            {
                JToken replacement;
                if (token.Type == JTokenType.String) replacement = new JValue(box.Text);
                else if (token.Type == JTokenType.Integer)
                {
                    if (!BigInteger.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                        throw new FormatException("请输入整数。");
                    replacement = integer >= long.MinValue && integer <= long.MaxValue ? new JValue((long)integer) : new JValue(integer);
                }
                else if (token.Type == JTokenType.Float)
                {
                    if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                        throw new FormatException("请输入有效数字（小数点使用 .）。");
                    replacement = new JValue(number);
                }
                else
                {
                    replacement = ParseToken(box.Text);
                    if (token is JArray && replacement is not JArray) throw new FormatException("请输入完整数组，例如 [60, 40]。");
                    if (token is JObject && replacement is not JObject) throw new FormatException("请输入 JSON 对象。");
                }
                if (schema?.HasRange == true && replacement.Type is JTokenType.Integer or JTokenType.Float &&
                    !schema.IsInRange((double)replacement))
                    throw new FormatException($"数值范围：{schema.BuildRangeText()}");

                _errors.Remove(path);
                error.Visibility = Visibility.Collapsed;
                box.ClearValue(Control.BorderBrushProperty);
                UpdateValue(path, replacement);
                UpdateErrors();
            }
            catch (Exception ex) when (ex is FormatException or JsonException or OverflowException)
            {
                _errors[path] = ex.Message;
                error.Text = ex.Message;
                error.Visibility = Visibility.Visible;
                box.SetResourceReference(Control.BorderBrushProperty, "DangerBrush");
                UpdateErrors();
            }
        };
        if (_drafts.TryGetValue(path, out var draft)) box.Text = draft;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; }
        };
        return box;
    }

    private void UpdateValue(string path, JToken replacement)
    {
        var current = _document?.SelectToken(path);
        if (current == null || JToken.DeepEquals(current, replacement)) return;
        current.Replace(replacement);
        JsonValueChanged?.Invoke(this, _document!.ToString(Formatting.Indented));
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading && PropertyPanel != null) ApplyFilter();
    }

    private void IndexEntries(JToken token, string name, string group, string groupName)
    {
        if (token is JObject obj && obj.Count > 0)
        {
            foreach (var property in obj.Properties()) IndexEntries(property.Value, property.Name, group, groupName);
        }
        else if (token is JArray array && array.Any(item => item is JObject or JArray))
        {
            for (int i = 0; i < array.Count; i++) IndexEntries(array[i], $"[{i}]", group, groupName);
        }
        else
        {
            var node = _schema?.FindNode(token.Path);
            _entries.Add(new ParameterEntry(token.Path, name, group, groupName,
                $"{token.Path} {Title(token.Path, name)} {node?.BuildHint(token.Path)} {groupName}"));
        }
    }

    private void ApplyFilter()
    {
        var query = SearchTextBox.Text.Trim();
        var group = (GroupSelector.SelectedItem as ComboBoxItem)?.Tag as string;
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchButton.Visibility = query.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var entries = _entries.Where(entry => query.Length == 0
            ? group == null || entry.Group == group
            : entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
              (_document?.SelectToken(entry.Path)?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true)).ToList();
        PropertyPanel.Children.Clear();
        _groups.Clear();
        foreach (var section in entries.GroupBy(entry => entry.Group))
        {
            var panel = new StackPanel();
            foreach (var entry in section)
            {
                if (_document?.SelectToken(entry.Path) is not { } token) continue;
                var row = CreateRow(token, entry.Name);
                panel.Children.Add(row);
                // Paths disambiguate identically named fields in global search and nested arrays.
                if (query.Length > 0 || entry.Path.Count(c => c == '.') > 1 || entry.Path.Contains('['))
                    row.ToolTip = entry.Path;
            }
            var title = section.First().GroupName;
            var expander = new Expander
            {
                Header = CreateGroupHeader(title, section.Key, section.Count()),
                Content = panel, Style = (Style)FindResource("ParameterGroup"),
                IsExpanded = query.Length > 0 || _expansion.GetValueOrDefault(section.Key, true)
            };
            var sectionKey = section.Key;
            expander.Expanded += (_, _) => { if (SearchTextBox.Text.Length == 0) _expansion[sectionKey] = true; };
            expander.Collapsed += (_, _) => { if (SearchTextBox.Text.Length == 0) _expansion[sectionKey] = false; };
            _groups.Add(new ParameterGroup(expander, sectionKey, title));
            PropertyPanel.Children.Add(expander);
        }
        if (entries.Count == 0 && CanEdit)
            PropertyPanel.Children.Add(new TextBlock { Text = _entries.Count == 0 ? "对象中还没有参数，请在 JSON 视图添加。" : "没有找到匹配参数，试试字段名、说明或值。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 20), Opacity = .65 });
        ResultCountText.Text = $"{entries.Count} / {_entries.Count} 项";
        GroupSelector.IsEnabled = query.Length == 0;
    }

    private Grid CreateGroupHeader(string title, string path, int count)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel();
        labels.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold });
        var description = _schema?.FindNode(path)?.Description;
        if (!string.IsNullOrWhiteSpace(description))
            labels.Children.Add(new TextBlock { Text = description, FontSize = 12, Opacity = .65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 10, 0) });
        header.Children.Add(labels);
        var counter = new TextBlock { Text = $"{count} 项", Opacity = .55, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(counter, 1);
        header.Children.Add(counter);
        return header;
    }

    private void LocateError_Click(object sender, RoutedEventArgs e)
    {
        if (_errors.Keys.FirstOrDefault() is { } path) { SearchTextBox.Text = path; ApplyFilter(); }
    }

    private void GroupSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ApplyFilter();
        PropertyScroller.ScrollToTop();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e) { SearchTextBox.Clear(); SearchTextBox.Focus(); }
    private void GroupOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; }
    }
    private void ExpandAllButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);
    private void CollapseAllButton_Click(object sender, RoutedEventArgs e) => SetExpanded(false);
    private void SetExpanded(bool expanded)
    {
        foreach (var group in _groups) { _expansion[group.Path] = expanded; group.Element.IsExpanded = expanded; }
    }

    public string? GetJson() => _document?.ToString(Formatting.Indented) ?? _originalJson;
    public bool ValidateJson() { UpdateErrors(); return !HasValidationErrors; }
    public void ClearError() { if (!HasValidationErrors) ErrorText.Visibility = Visibility.Collapsed; }
    private void ShowError(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
    private void UpdateErrors()
    {
        if (_errors.Count > 0) ShowError($"有 {_errors.Count} 项输入需要修正，当前错误输入尚未应用。");
        else if (CanEdit) ErrorText.Visibility = Visibility.Collapsed;
        LocateErrorButton.Visibility = _errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ValidationStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
