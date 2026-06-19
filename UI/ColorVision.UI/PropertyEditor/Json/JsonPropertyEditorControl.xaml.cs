#pragma warning disable CA1304,CA1310,CA1822,CA1859,CA1866
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.UI.Properties;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// UserControl that provides a property editor interface for JSON data
    /// </summary>
    public partial class JsonPropertyEditorControl : UserControl
    {
        private const double LabelColumnWidth = 160;
        private const double EditorMinWidth = 140;

        private JObject? _jObject;
        private string? _originalJson;
        private JsonEditorSchemaDocument? _schemaDocument;

        public JsonPropertyEditorControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Event raised when JSON value changes
        /// </summary>
        public event EventHandler<string>? JsonValueChanged;

        /// <summary>
        /// Sets the JSON string to be edited
        /// </summary>
        public void SetJson(string json)
        {
            SetJson(json, null, null);
        }

        /// <summary>
        /// Sets the JSON string and optional JSON Schema metadata to be edited
        /// </summary>
        public void SetJson(string json, string? schemaJson, string? schemaTitle = null)
        {
            try
            {
                _originalJson = json;
                _schemaDocument = JsonEditorSchemaDocument.TryParse(schemaJson, schemaTitle);
                ErrorBorder.Visibility = Visibility.Collapsed;
                UpdateSchemaInfo();

                // Parse JSON to JObject
                _jObject = JObject.Parse(json);

                // Clear and generate UI directly from JObject
                PropertyPanel.Children.Clear();
                UpdateSearchState();
                GeneratePropertiesFromJObject(_jObject);
            }
            catch (JsonException ex)
            {
                _schemaDocument = null;
                UpdateSchemaInfo();
                ShowError($"{Properties.Resources.PropEditor_JsonParseError} {ex.Message}");
            }
            catch (Exception ex)
            {
                _schemaDocument = null;
                UpdateSchemaInfo();
                ShowError($"{Properties.Resources.PropEditor_LoadError} {ex.Message}");
            }
        }

        private void UpdateSchemaInfo()
        {
            if (_schemaDocument == null)
            {
                SchemaInfoBorder.Visibility = Visibility.Collapsed;
                SchemaTitleText.Text = string.Empty;
                SchemaDetailText.Text = string.Empty;
                SchemaBadgeText.Text = string.Empty;
                return;
            }

            SchemaInfoBorder.Visibility = Visibility.Visible;
            SchemaTitleText.Text = _schemaDocument.Title;

            var detailParts = new List<string>();
            if (_schemaDocument.DescribedFieldCount > 0)
                detailParts.Add($"{_schemaDocument.DescribedFieldCount}/{_schemaDocument.FieldCount} 项有说明");
            if (!string.IsNullOrWhiteSpace(_schemaDocument.SourceSummary))
                detailParts.Add(_schemaDocument.SourceSummary);

            SchemaDetailText.Text = string.Join("  ·  ", detailParts);
            SchemaDetailText.ToolTip = string.IsNullOrWhiteSpace(_schemaDocument.Description)
                ? SchemaDetailText.Text
                : SchemaDetailText.Text + Environment.NewLine + _schemaDocument.Description;
            SchemaBadgeText.Text = _schemaDocument.ProviderMaintained ? "提供者" : "Schema";
            SchemaBadgeText.ToolTip = _schemaDocument.ProviderMaintained ? "提供者维护的参数 schema" : "JSON Schema";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_jObject == null || PropertyPanel == null)
                return;

            UpdateSearchState();
            PropertyPanel.Children.Clear();
            GeneratePropertiesFromJObject(_jObject);
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = string.Empty;
            SearchTextBox.Focus();
        }

        private void UpdateSearchState()
        {
            var hasSearchText = !string.IsNullOrWhiteSpace(SearchTextBox?.Text);
            SearchPlaceholder.Visibility = hasSearchText ? Visibility.Collapsed : Visibility.Visible;
            ClearSearchButton.Visibility = hasSearchText ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersExpanded(PropertyPanel, true);
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersExpanded(PropertyPanel, false);
        }

        private static void SetExpandersExpanded(DependencyObject root, bool isExpanded)
        {
            foreach (var expander in EnumerateExpanders(root))
                expander.IsExpanded = isExpanded;
        }

        private static IEnumerable<Expander> EnumerateExpanders(DependencyObject root)
        {
            if (root is Expander expander)
                yield return expander;

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                {
                    foreach (var nestedExpander in EnumerateExpanders(dependencyObject))
                        yield return nestedExpander;
                }
            }
        }

        /// <summary>
        /// Generates property editor controls directly from JObject
        /// </summary>
        private void GeneratePropertiesFromJObject(JObject jObject)
        {
            if (jObject == null || jObject.Count == 0)
            {
                var noPropsText = new TextBlock
                {
                    Text = Properties.Resources.PropEditor_NoEditableProperties,
                    Margin = new Thickness(10),
                    FontStyle = FontStyles.Italic
                };
                PropertyPanel.Children.Add(noPropsText);
                return;
            }

            var filterText = SearchTextBox?.Text?.Trim() ?? string.Empty;
            var controls = new List<FrameworkElement>();

            // Generate control for each property
            foreach (var prop in jObject.Properties())
            {
                try
                {
                    var control = GenerateControlForProperty(prop.Name, prop.Value, 0, filterText, false);
                    if (control != null)
                        controls.Add(control);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating control for {prop.Name}: {ex.Message}");
                }
            }

            var detail = string.IsNullOrEmpty(filterText)
                ? $"{jObject.Count} 项参数"
                : $"匹配 {controls.Count} 项";

            PropertyPanel.Children.Add(CreateSectionHeader("参数属性", detail));
            PropertyPanel.Children.Add(CreateDivider());

            if (controls.Count == 0)
            {
                PropertyPanel.Children.Add(CreateEmptyResultText(string.IsNullOrEmpty(filterText)
                    ? Properties.Resources.PropEditor_NoEditableProperties
                    : "未找到匹配参数"));
                return;
            }

            foreach (var control in controls)
                PropertyPanel.Children.Add(control);
        }

        /// <summary>
        /// Generates an editor control for a single JSON property
        /// </summary>
        private FrameworkElement? GenerateControlForProperty(string propertyPath, JToken value, int depth, string filterText, bool ancestorMatched)
        {
            // Extract just the property name (last part after dot) for display
            var schemaNode = _schemaDocument?.FindNode(propertyPath);
            var displayName = schemaNode?.GetTitleOrFallback(FormatPropertyName(GetDisplayNameFromPath(propertyPath)))
                ?? FormatPropertyName(GetDisplayNameFromPath(propertyPath));
            var selfMatched = MatchesFilter(filterText, propertyPath, displayName) || schemaNode?.Matches(filterText) == true;

            // Create editor based on type
            FrameworkElement? editor = null;

            if (schemaNode?.HasEnum == true)
            {
                editor = CreateEnumEditor(propertyPath, value, schemaNode);
            }
            else switch (value.Type)
            {
                case JTokenType.Boolean:
                    editor = CreateBoolEditor(propertyPath, value, schemaNode);
                    break;
                    
                case JTokenType.Integer:
                case JTokenType.Float:
                    editor = CreateNumericEditor(propertyPath, value, schemaNode);
                    break;
                    
                case JTokenType.String:
                    editor = CreateStringEditor(propertyPath, value, schemaNode);
                    break;
                    
                case JTokenType.Array:
                    return CreateArrayEditor(propertyPath, displayName, value as JArray, depth, filterText, ancestorMatched || selfMatched, schemaNode);
                    
                case JTokenType.Object:
                    return CreateObjectEditor(propertyPath, displayName, value as JObject, depth, filterText, ancestorMatched || selfMatched, schemaNode);
                    
                default:
                    editor = CreateDefaultEditor(propertyPath, value);
                    break;
            }

            if (editor != null)
            {
                var valueMatched = MatchesFilter(filterText, value.ToString());
                if (!ancestorMatched && !selfMatched && !valueMatched)
                    return null;

                return CreatePropertyRow(displayName, propertyPath, editor, schemaNode);
            }

            return null;
        }

        private static TextBlock CreateEmptyResultText(string text)
        {
            return new TextBlock
            {
                Text = text,
                Margin = new Thickness(8),
                Opacity = 0.72,
                FontStyle = FontStyles.Italic
            };
        }

        private FrameworkElement CreateSectionHeader(string title, string detail)
        {
            var header = new Grid
            {
                Margin = new Thickness(0, 0, 0, 5)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            var detailText = new TextBlock
            {
                Text = detail,
                Margin = new Thickness(8, 0, 0, 0),
                Opacity = 0.72,
                VerticalAlignment = VerticalAlignment.Center
            };
            detailText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(detailText, 1);
            header.Children.Add(titleText);
            header.Children.Add(detailText);

            return header;
        }

        private FrameworkElement CreateDivider()
        {
            var divider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            divider.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            return divider;
        }

        private static Grid CreatePropertyRow(string labelText, string propertyPath, FrameworkElement editor, JsonEditorSchemaNode? schemaNode)
        {
            ApplyEditorLayout(editor);
            if (schemaNode != null)
                editor.ToolTip ??= schemaNode.BuildHint(propertyPath);

            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 3),
                MinHeight = string.IsNullOrWhiteSpace(schemaNode?.Description) ? 28 : 42,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = labelText,
                FontWeight = schemaNode == null ? FontWeights.Normal : FontWeights.SemiBold,
                ToolTip = schemaNode?.BuildHint(propertyPath) ?? propertyPath
            };
            labelPanel.Children.Add(label);

            if (!string.IsNullOrWhiteSpace(schemaNode?.Description))
            {
                var description = new TextBlock
                {
                    Text = schemaNode.Description,
                    FontSize = 11,
                    Opacity = 0.68,
                    Margin = new Thickness(0, 1, 0, 0),
                    ToolTip = schemaNode.BuildHint(propertyPath)
                };
                labelPanel.Children.Add(description);
            }

            Grid.SetColumn(labelPanel, 0);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(labelPanel);
            grid.Children.Add(editor);

            return grid;
        }

        private static void ApplyEditorLayout(FrameworkElement editor)
        {
            switch (editor)
            {
                case TextBox textBox:
                    if (textBox.Tag is "Numeric")
                    {
                        textBox.Width = 120;
                        textBox.MinWidth = 100;
                        textBox.HorizontalAlignment = HorizontalAlignment.Left;
                        textBox.TextAlignment = TextAlignment.Right;
                    }
                    else
                    {
                        textBox.MinWidth = EditorMinWidth;
                        textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    }
                    textBox.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case CheckBox checkBox:
                    checkBox.HorizontalAlignment = HorizontalAlignment.Left;
                    checkBox.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case TextBlock textBlock:
                    textBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
                    textBlock.VerticalAlignment = VerticalAlignment.Center;
                    break;
                default:
                    editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                    editor.VerticalAlignment = VerticalAlignment.Stretch;
                    break;
            }
        }

        /// <summary>
        /// Creates a boolean editor (CheckBox/ToggleSwitch)
        /// </summary>
        private FrameworkElement CreateBoolEditor(string propertyName, JToken value, JsonEditorSchemaNode? schemaNode)
        {
            var checkBox = new CheckBox
            {
                IsChecked = value.Value<bool>(),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = schemaNode?.BuildHint(propertyName)
            };

            checkBox.Checked += (s, e) => UpdateJsonValue(propertyName, true);
            checkBox.Unchecked += (s, e) => UpdateJsonValue(propertyName, false);

            return checkBox;
        }

        /// <summary>
        /// Creates a numeric editor (TextBox with validation)
        /// </summary>
        private FrameworkElement CreateNumericEditor(string propertyName, JToken value, JsonEditorSchemaNode? schemaNode)
        {
            var textBox = new TextBox
            {
                Text = value.ToString(),
                MinWidth = 100,
                Tag = "Numeric",
                ToolTip = schemaNode?.BuildHint(propertyName)
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBox.Small");

            textBox.LostFocus += (s, e) =>
            {
                if (value.Type == JTokenType.Integer)
                {
                    if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    {
                        if (!ValidateSchemaRange(textBox, schemaNode, intValue))
                            return;

                        ClearFieldError(textBox);
                        UpdateJsonValue(propertyName, intValue);
                    }
                    else
                    {
                        ShowFieldError(textBox, "请输入整数");
                    }
                }
                else if (value.Type == JTokenType.Float)
                {
                    if (TryParseDouble(textBox.Text, out double doubleValue))
                    {
                        if (!ValidateSchemaRange(textBox, schemaNode, doubleValue))
                            return;

                        ClearFieldError(textBox);
                        UpdateJsonValue(propertyName, doubleValue);
                    }
                    else
                    {
                        ShowFieldError(textBox, "请输入数字");
                    }
                }
            };
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            };

            return textBox;
        }

        /// <summary>
        /// Creates a string editor (TextBox)
        /// </summary>
        private FrameworkElement CreateStringEditor(string propertyName, JToken value, JsonEditorSchemaNode? schemaNode)
        {
            var textBox = new TextBox
            {
                Text = value.Value<string>() ?? string.Empty,
                MinWidth = EditorMinWidth,
                ToolTip = schemaNode?.BuildHint(propertyName)
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBox.Small");

            textBox.LostFocus += (s, e) => UpdateJsonValue(propertyName, textBox.Text);

            return textBox;
        }

        private FrameworkElement CreateEnumEditor(string propertyName, JToken value, JsonEditorSchemaNode schemaNode)
        {
            var comboBox = new ComboBox
            {
                MinWidth = EditorMinWidth,
                ToolTip = schemaNode.BuildHint(propertyName)
            };
            comboBox.SetResourceReference(ComboBox.StyleProperty, "ComboBox.Small");

            var selectedIndex = -1;
            for (var i = 0; i < schemaNode.EnumItems.Count; i++)
            {
                var item = schemaNode.EnumItems[i];
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = item.DisplayText,
                    Tag = item
                });

                if (item.Matches(value))
                    selectedIndex = i;
            }

            if (selectedIndex >= 0)
                comboBox.SelectedIndex = selectedIndex;

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem is ComboBoxItem { Tag: JsonEditorSchemaEnumItem enumItem })
                    UpdateJsonValue(propertyName, enumItem.ToObject());
            };

            return comboBox;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static bool ValidateSchemaRange(TextBox textBox, JsonEditorSchemaNode? schemaNode, double value)
        {
            if (schemaNode == null || !schemaNode.HasRange || schemaNode.IsInRange(value))
                return true;

            var rangeText = schemaNode.BuildRangeText();
            ShowFieldError(textBox, string.IsNullOrWhiteSpace(rangeText) ? "数值超出范围" : rangeText);
            return false;
        }

        private static void ShowFieldError(TextBox textBox, string message)
        {
            textBox.ToolTip = message;
            textBox.BorderThickness = new Thickness(1);
            textBox.SetResourceReference(Control.BorderBrushProperty, "DangerBrush");
        }

        private static void ClearFieldError(TextBox textBox)
        {
            textBox.ToolTip = null;
            textBox.ClearValue(Control.BorderBrushProperty);
            textBox.ClearValue(Control.BorderThicknessProperty);
        }

        /// <summary>
        /// Creates an array editor - displays primitives inline, expands complex types
        /// </summary>
        private FrameworkElement? CreateArrayEditor(string propertyPath, string displayName, JArray? array, int depth, string filterText, bool groupMatched, JsonEditorSchemaNode? schemaNode)
        {
            if (array == null || array.Count == 0)
            {
                if (!groupMatched && !MatchesFilter(filterText, "[]") && schemaNode?.Matches(filterText) != true)
                    return null;

                var emptyText = new TextBlock
                {
                    Text = "[]",
                    VerticalAlignment = VerticalAlignment.Center
                };
                return CreatePropertyRow(displayName, propertyPath, emptyText, schemaNode);
            }

            // Check if all elements are primitive types (not objects or arrays)
            bool allPrimitives = array.All(item => 
                item.Type != JTokenType.Object && 
                item.Type != JTokenType.Array);

            if (allPrimitives)
            {
                var arrayText = string.Join(", ", array.Select(item => item.ToString()));
                if (!groupMatched && !MatchesFilter(filterText, arrayText) && schemaNode?.Matches(filterText) != true)
                    return null;

                // For primitive arrays, display as comma-separated inline text
                return CreatePropertyRow(displayName, propertyPath, CreatePrimitiveArrayEditor(propertyPath, array, schemaNode), schemaNode);
            }
            else
            {
                // For complex arrays, use expander with recursive expansion
                return CreateComplexArrayEditor(propertyPath, displayName, array, depth, filterText, groupMatched, schemaNode);
            }
        }

        /// <summary>
        /// Creates an inline editor for arrays of primitive values
        /// </summary>
        private FrameworkElement CreatePrimitiveArrayEditor(string propertyPath, JArray array, JsonEditorSchemaNode? schemaNode)
        {
            var textBox = new TextBox
            {
                Text = string.Join(", ", array.Select(item => item.ToString())),
                MinWidth = EditorMinWidth,
                ToolTip = schemaNode?.BuildHint(propertyPath)
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBox.Small");

            textBox.LostFocus += (s, e) =>
            {
                try
                {
                    // Parse comma-separated values back into array
                    var values = textBox.Text.Split(',').Select(v => v.Trim()).ToList();
                    
                    // Try to infer the type from the first element in the original array
                    if (array.Count > 0)
                    {
                        var newArray = new JArray();
                        var firstType = array[0].Type;
                        
                        foreach (var val in values)
                        {
                            if (string.IsNullOrWhiteSpace(val))
                                continue;
                                
                            switch (firstType)
                            {
                                case JTokenType.Integer:
                                    if (int.TryParse(val, out int intVal))
                                        newArray.Add(intVal);
                                    break;
                                case JTokenType.Float:
                                    if (double.TryParse(val, out double doubleVal))
                                        newArray.Add(doubleVal);
                                    break;
                                case JTokenType.Boolean:
                                    if (bool.TryParse(val, out bool boolVal))
                                        newArray.Add(boolVal);
                                    break;
                                case JTokenType.String:
                                default:
                                    newArray.Add(val);
                                    break;
                            }
                        }
                        
                        UpdateJsonValue(propertyPath, newArray);
                    }
                }
                catch
                {
                    // Invalid format, revert
                    textBox.Text = string.Join(", ", array.Select(item => item.ToString()));
                }
            };

            return textBox;
        }

        /// <summary>
        /// Creates an expander for arrays of complex types (objects/arrays)
        /// </summary>
        private FrameworkElement? CreateComplexArrayEditor(string propertyPath, string displayName, JArray array, int depth, string filterText, bool groupMatched, JsonEditorSchemaNode? schemaNode)
        {
            var elements = new List<FrameworkElement>();
            for (int i = 0; i < array.Count; i++)
            {
                try
                {
                    var elementControl = GenerateControlForProperty(
                        $"{propertyPath}[{i}]",
                        array[i],
                        depth + 1,
                        filterText,
                        groupMatched
                    );
                    if (elementControl != null)
                        elements.Add(elementControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating control for array element {i}: {ex.Message}");
                }
            }

            if (!groupMatched && elements.Count == 0)
                return null;

            var isFiltering = !string.IsNullOrEmpty(filterText);
            var detail = isFiltering && !groupMatched ? $"匹配 {elements.Count} 项" : $"{array.Count} 项";

            // Create an expander to show/hide array elements
            var expander = new System.Windows.Controls.Expander
            {
                Header = CreateGroupHeader(displayName, detail, propertyPath, schemaNode),
                IsExpanded = ShouldExpandGroup(depth, array.Count, isFiltering),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, depth == 0 ? 4 : 2, 0, 2)
            };

            // Create a stack panel for array elements
            var elementsPanel = new StackPanel
            {
                Margin = new Thickness(0, 1, 0, 1)
            };

            foreach (var element in elements)
                elementsPanel.Children.Add(element);

            expander.Content = CreateNestedHost(elementsPanel, depth);
            return expander;
        }

        /// <summary>
        /// Creates an object editor - recursively expands nested properties
        /// </summary>
        private FrameworkElement? CreateObjectEditor(string propertyName, string displayName, JObject? obj, int depth, string filterText, bool groupMatched, JsonEditorSchemaNode? schemaNode)
        {
            if (obj == null || obj.Count == 0)
            {
                if (!groupMatched && !MatchesFilter(filterText, "{}") && schemaNode?.Matches(filterText) != true)
                    return null;

                var emptyText = new TextBlock
                {
                    Text = "{}",
                    VerticalAlignment = VerticalAlignment.Center
                };
                return CreatePropertyRow(displayName, propertyName, emptyText, schemaNode);
            }

            var nestedControls = new List<FrameworkElement>();
            foreach (var nestedProp in obj.Properties())
            {
                try
                {
                    var nestedControl = GenerateControlForProperty(
                        $"{propertyName}.{nestedProp.Name}",
                        nestedProp.Value,
                        depth + 1,
                        filterText,
                        groupMatched
                    );
                    if (nestedControl != null)
                        nestedControls.Add(nestedControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating nested control for {nestedProp.Name}: {ex.Message}");
                }
            }

            if (!groupMatched && nestedControls.Count == 0)
                return null;

            var isFiltering = !string.IsNullOrEmpty(filterText);
            var detail = isFiltering && !groupMatched ? $"匹配 {nestedControls.Count} 项" : $"{obj.Count} 项参数";

            // Create an expander to show/hide nested properties
            var expander = new System.Windows.Controls.Expander
            {
                Header = CreateGroupHeader(displayName, detail, propertyName, schemaNode),
                IsExpanded = ShouldExpandGroup(depth, obj.Count, isFiltering),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, depth == 0 ? 4 : 2, 0, 2)
            };

            // Create a stack panel for nested properties
            var nestedPanel = new StackPanel
            {
                Margin = new Thickness(0, 1, 0, 1)
            };

            foreach (var nestedControl in nestedControls)
                nestedPanel.Children.Add(nestedControl);

            expander.Content = CreateNestedHost(nestedPanel, depth);
            return expander;
        }

        private FrameworkElement CreateGroupHeader(string title, string detail, string propertyPath, JsonEditorSchemaNode? schemaNode)
        {
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = schemaNode?.BuildHint(propertyPath) ?? propertyPath
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            var detailText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(schemaNode?.Description) ? detail : $"{detail} · {schemaNode.Description}",
                Margin = new Thickness(8, 0, 0, 0),
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = schemaNode?.BuildHint(propertyPath) ?? detail
            };
            detailText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(detailText, 1);
            grid.Children.Add(titleText);
            grid.Children.Add(detailText);

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = grid
            };
            border.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush1");
            border.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");

            return border;
        }

        private static bool ShouldExpandGroup(int depth, int count, bool isFiltering)
        {
            if (isFiltering)
                return true;

            if (depth == 0)
                return count <= 8;

            return count <= 4;
        }

        private static bool MatchesFilter(string filterText, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    value.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private FrameworkElement CreateNestedHost(UIElement content, int depth)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(depth == 0 ? 8 : 6, 2, 0, 1),
                Margin = new Thickness(0, 3, 0, 0),
                Child = content
            };
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            return border;
        }

        /// <summary>
        /// Creates a default editor for unknown types
        /// </summary>
        private FrameworkElement CreateDefaultEditor(string propertyName, JToken value)
        {
            var textBlock = new TextBlock
            {
                Text = value.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };

            return textBlock;
        }

        /// <summary>
        /// Updates a value in the JObject and triggers change event
        /// Supports nested property paths like "parent.child" and array indices like "items[0]"
        /// </summary>
        private void UpdateJsonValue(string propertyPath, object? value)
        {
            if (_jObject == null) return;

            try
            {
                JToken current = _jObject;
                
                // Parse the path which can contain dots and array indices
                // Examples: "config.timeout", "items[0]", "users[1].name"
                var pathParts = new List<string>();
                var currentPart = "";
                
                for (int i = 0; i < propertyPath.Length; i++)
                {
                    char c = propertyPath[i];
                    if (c == '.')
                    {
                        if (!string.IsNullOrEmpty(currentPart))
                        {
                            pathParts.Add(currentPart);
                            currentPart = "";
                        }
                    }
                    else if (c == '[')
                    {
                        if (!string.IsNullOrEmpty(currentPart))
                        {
                            pathParts.Add(currentPart);
                            currentPart = "";
                        }
                        // Find the closing bracket
                        int closingBracket = propertyPath.IndexOf(']', i);
                        if (closingBracket > i)
                        {
                            pathParts.Add(propertyPath.Substring(i, closingBracket - i + 1)); // Include [ and ]
                            i = closingBracket;
                        }
                    }
                    else
                    {
                        currentPart += c;
                    }
                }
                
                if (!string.IsNullOrEmpty(currentPart))
                    pathParts.Add(currentPart);
                
                // Navigate to the parent of the property we want to update
                for (int i = 0; i < pathParts.Count - 1; i++)
                {
                    var part = pathParts[i];
                    if (part.StartsWith("[") && part.EndsWith("]"))
                    {
                        // Array index
                        var indexStr = part.Substring(1, part.Length - 2);
                        if (int.TryParse(indexStr, out int index))
                        {
                            current = current[index];
                        }
                    }
                    else
                    {
                        // Object property
                        current = current[part];
                    }
                    
                    if (current == null)
                        return; // Path doesn't exist
                }
                
                // Update the final property
                var lastPart = pathParts[pathParts.Count - 1];
                if (lastPart.StartsWith("[") && lastPart.EndsWith("]"))
                {
                    // Array index
                    var indexStr = lastPart.Substring(1, lastPart.Length - 2);
                    if (int.TryParse(indexStr, out int index) && current is JArray arr)
                    {
                        arr[index] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                    }
                }
                else
                {
                    // Object property
                    if (current is JObject obj)
                    {
                        obj[lastPart] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                    }
                }
                
                OnJsonModified();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating {propertyPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats property name for display (camelCase/snake_case -> Title Case)
        /// </summary>
        private static string GetDisplayNameFromPath(string propertyPath)
        {
            var lastDotIndex = propertyPath.LastIndexOf('.');
            var name = lastDotIndex >= 0
                ? propertyPath.Substring(lastDotIndex + 1)
                : propertyPath;

            var bracketIndex = name.LastIndexOf('[');
            if (bracketIndex >= 0 && name.EndsWith("]"))
            {
                var indexText = name.Substring(bracketIndex + 1, name.Length - bracketIndex - 2);
                if (int.TryParse(indexText, out var index))
                    return $"第 {index + 1} 项";
            }

            return name;
        }

        private static string FormatPropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                var current = name[i];
                if (current == '_' || current == '-')
                {
                    AppendSpace(result);
                    continue;
                }

                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';
                if (ShouldInsertWordBreak(previous, current, next))
                    AppendSpace(result);

                result.Append(result.Length == 0 ? char.ToUpper(current) : current);
            }

            return result.ToString().Trim();
        }

        private static bool ShouldInsertWordBreak(char previous, char current, char next)
        {
            if (previous == '\0' || previous == '_' || previous == '-' || previous == ' ')
                return false;

            if (char.IsDigit(current) && char.IsLetter(previous))
                return true;

            if (!char.IsUpper(current))
                return false;

            return char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next));
        }

        private static void AppendSpace(System.Text.StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                builder.Append(' ');
        }

        /// <summary>
        /// Gets the current JSON string
        /// </summary>
        public string? GetJson()
        {
            try
            {
                if (_jObject == null)
                    return _originalJson;

                return _jObject.ToString(Formatting.Indented);
            }
            catch (Exception ex)
            {
                ShowError($"{Properties.Resources.PropEditor_JsonGenerateError} {ex.Message}");
                return _originalJson;
            }
        }

        /// <summary>
        /// Validates if the current state can be converted to valid JSON
        /// </summary>
        public bool ValidateJson()
        {
            try
            {
                var json = GetJson();
                if (string.IsNullOrEmpty(json))
                    return false;

                // Try to parse to verify it's valid JSON
                JObject.Parse(json);
                ErrorBorder.Visibility = Visibility.Collapsed;
                return true;
            }
            catch (Exception ex)
            {
                ShowError($"{Properties.Resources.PropEditor_ValidationError} {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called when any property is modified
        /// </summary>
        private void OnJsonModified()
        {
            try
            {
                var json = GetJson();
                if (json != null)
                {
                    JsonValueChanged?.Invoke(this, json);
                }
            }
            catch
            {
                // Ignore errors during modification
            }
        }

        /// <summary>
        /// Shows an error message
        /// </summary>
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;

            // Auto-hide after 5 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        /// <summary>
        /// Clears any error messages
        /// </summary>
        public void ClearError()
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
        }
    }
}
