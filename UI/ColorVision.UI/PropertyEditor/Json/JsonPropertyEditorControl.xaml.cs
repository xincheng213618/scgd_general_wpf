using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// UserControl that provides a property editor interface for JSON data
    /// </summary>
    public partial class JsonPropertyEditorControl : UserControl
    {
        private JObject? _jObject;
        private string? _originalJson;

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
            try
            {
                _originalJson = json;
                ErrorBorder.Visibility = Visibility.Collapsed;

                // Parse JSON to JObject
                _jObject = JObject.Parse(json);

                // Clear and generate UI directly from JObject
                PropertyPanel.Children.Clear();
                GeneratePropertiesFromJObject(_jObject);
            }
            catch (JsonException ex)
            {
                ShowError($"JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"加载错误: {ex.Message}");
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
                    Text = "没有可编辑的属性",
                    Margin = new Thickness(10),
                    FontStyle = FontStyles.Italic
                };
                PropertyPanel.Children.Add(noPropsText);
                return;
            }

            // Create a border for all properties
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 5)
            };
            border.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var stackPanel = new StackPanel { Margin = new Thickness(5) };

            // Header
            var header = new TextBlock
            {
                Text = "JSON Properties",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            stackPanel.Children.Add(header);

            // Generate control for each property
            foreach (var prop in jObject.Properties())
            {
                try
                {
                    var control = GenerateControlForProperty(prop.Name, prop.Value);
                    if (control != null)
                        stackPanel.Children.Add(control);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating control for {prop.Name}: {ex.Message}");
                }
            }

            border.Child = stackPanel;
            PropertyPanel.Children.Add(border);
        }

        /// <summary>
        /// Generates an editor control for a single JSON property
        /// </summary>
        private DockPanel? GenerateControlForProperty(string propertyPath, JToken value)
        {
            var dockPanel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

            // Extract just the property name (last part after dot) for display
            var displayName = propertyPath.Contains('.') 
                ? propertyPath.Substring(propertyPath.LastIndexOf('.') + 1)
                : propertyPath;

            // Create label
            var label = new TextBlock
            {
                Text = FormatPropertyName(displayName),
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            dockPanel.Children.Add(label);
            DockPanel.SetDock(label, Dock.Left);

            // Create editor based on type
            FrameworkElement? editor = null;
            
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    editor = CreateBoolEditor(propertyPath, value);
                    break;
                    
                case JTokenType.Integer:
                case JTokenType.Float:
                    editor = CreateNumericEditor(propertyPath, value);
                    break;
                    
                case JTokenType.String:
                    editor = CreateStringEditor(propertyPath, value);
                    break;
                    
                case JTokenType.Array:
                    editor = CreateArrayEditor(propertyPath, value as JArray);
                    break;
                    
                case JTokenType.Object:
                    editor = CreateObjectEditor(propertyPath, value as JObject);
                    break;
                    
                default:
                    editor = CreateDefaultEditor(propertyPath, value);
                    break;
            }

            if (editor != null)
            {
                dockPanel.Children.Add(editor);
                return dockPanel;
            }

            return null;
        }

        /// <summary>
        /// Creates a boolean editor (CheckBox/ToggleSwitch)
        /// </summary>
        private FrameworkElement CreateBoolEditor(string propertyName, JToken value)
        {
            var checkBox = new CheckBox
            {
                IsChecked = value.Value<bool>(),
                VerticalAlignment = VerticalAlignment.Center
            };

            checkBox.Checked += (s, e) => UpdateJsonValue(propertyName, true);
            checkBox.Unchecked += (s, e) => UpdateJsonValue(propertyName, false);

            return checkBox;
        }

        /// <summary>
        /// Creates a numeric editor (TextBox with validation)
        /// </summary>
        private FrameworkElement CreateNumericEditor(string propertyName, JToken value)
        {
            var textBox = new TextBox
            {
                Text = value.ToString(),
                MinWidth = 150
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBoxSmallStyle");

            textBox.LostFocus += (s, e) =>
            {
                if (value.Type == JTokenType.Integer)
                {
                    if (int.TryParse(textBox.Text, out int intValue))
                        UpdateJsonValue(propertyName, intValue);
                }
                else if (value.Type == JTokenType.Float)
                {
                    if (double.TryParse(textBox.Text, out double doubleValue))
                        UpdateJsonValue(propertyName, doubleValue);
                }
            };

            return textBox;
        }

        /// <summary>
        /// Creates a string editor (TextBox)
        /// </summary>
        private FrameworkElement CreateStringEditor(string propertyName, JToken value)
        {
            var textBox = new TextBox
            {
                Text = value.Value<string>() ?? string.Empty,
                MinWidth = 150
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBoxSmallStyle");

            textBox.LostFocus += (s, e) => UpdateJsonValue(propertyName, textBox.Text);

            return textBox;
        }

        /// <summary>
        /// Creates an array editor - displays primitives inline, expands complex types
        /// </summary>
        private FrameworkElement CreateArrayEditor(string propertyPath, JArray? array)
        {
            if (array == null || array.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "[]",
                    VerticalAlignment = VerticalAlignment.Center
                };
                return emptyText;
            }

            // Check if all elements are primitive types (not objects or arrays)
            bool allPrimitives = array.All(item => 
                item.Type != JTokenType.Object && 
                item.Type != JTokenType.Array);

            if (allPrimitives)
            {
                // For primitive arrays, display as comma-separated inline text
                return CreatePrimitiveArrayEditor(propertyPath, array);
            }
            else
            {
                // For complex arrays, use expander with recursive expansion
                return CreateComplexArrayEditor(propertyPath, array);
            }
        }

        /// <summary>
        /// Creates an inline editor for arrays of primitive values
        /// </summary>
        private FrameworkElement CreatePrimitiveArrayEditor(string propertyPath, JArray array)
        {
            var textBox = new TextBox
            {
                Text = string.Join(", ", array.Select(item => item.ToString())),
                MinWidth = 150
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBoxSmallStyle");

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
        private FrameworkElement CreateComplexArrayEditor(string propertyPath, JArray array)
        {
            // Create an expander to show/hide array elements
            var expander = new System.Windows.Controls.Expander
            {
                Header = $"[{array.Count} items]",
                IsExpanded = false,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // Create a stack panel for array elements
            var elementsPanel = new StackPanel
            {
                Margin = new Thickness(20, 5, 0, 5) // Indent array elements
            };

            // Recursively generate controls for each array element
            for (int i = 0; i < array.Count; i++)
            {
                try
                {
                    var elementControl = GenerateControlForProperty(
                        $"{propertyPath}[{i}]",
                        array[i]
                    );
                    if (elementControl != null)
                        elementsPanel.Children.Add(elementControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating control for array element {i}: {ex.Message}");
                }
            }

            expander.Content = elementsPanel;
            return expander;
        }

        /// <summary>
        /// Creates an object editor - recursively expands nested properties
        /// </summary>
        private FrameworkElement CreateObjectEditor(string propertyName, JObject? obj)
        {
            if (obj == null || obj.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "{}",
                    VerticalAlignment = VerticalAlignment.Center
                };
                return emptyText;
            }

            // Create an expander to show/hide nested properties
            var expander = new System.Windows.Controls.Expander
            {
                Header = $"({obj.Count} properties)",
                IsExpanded = false,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // Create a stack panel for nested properties
            var nestedPanel = new StackPanel
            {
                Margin = new Thickness(20, 5, 0, 5) // Indent nested properties
            };

            // Recursively generate controls for nested properties
            foreach (var nestedProp in obj.Properties())
            {
                try
                {
                    var nestedControl = GenerateControlForProperty(
                        $"{propertyName}.{nestedProp.Name}", 
                        nestedProp.Value
                    );
                    if (nestedControl != null)
                        nestedPanel.Children.Add(nestedControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error generating nested control for {nestedProp.Name}: {ex.Message}");
                }
            }

            expander.Content = nestedPanel;
            return expander;
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
        /// Formats property name for display (camelCase -> Title Case)
        /// </summary>
        private static string FormatPropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            result.Append(char.ToUpper(name[0]));

            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0)
                    result.Append(' ');
                result.Append(name[i]);
            }

            return result.ToString();
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
                ShowError($"JSON 生成错误: {ex.Message}");
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
                ShowError($"验证失败: {ex.Message}");
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
