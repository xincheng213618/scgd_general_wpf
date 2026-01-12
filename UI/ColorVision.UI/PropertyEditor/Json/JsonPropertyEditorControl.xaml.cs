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
        private DockPanel? GenerateControlForProperty(string propertyName, JToken value)
        {
            var dockPanel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

            // Create label
            var label = new TextBlock
            {
                Text = FormatPropertyName(propertyName),
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
                    editor = CreateBoolEditor(propertyName, value);
                    break;
                    
                case JTokenType.Integer:
                case JTokenType.Float:
                    editor = CreateNumericEditor(propertyName, value);
                    break;
                    
                case JTokenType.String:
                    editor = CreateStringEditor(propertyName, value);
                    break;
                    
                case JTokenType.Array:
                    editor = CreateArrayEditor(propertyName, value as JArray);
                    break;
                    
                case JTokenType.Object:
                    editor = CreateObjectEditor(propertyName, value as JObject);
                    break;
                    
                default:
                    editor = CreateDefaultEditor(propertyName, value);
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
        /// Creates an array editor (displays as JSON text for now)
        /// </summary>
        private FrameworkElement CreateArrayEditor(string propertyName, JArray? array)
        {
            var textBox = new TextBox
            {
                Text = array?.ToString(Formatting.None) ?? "[]",
                MinWidth = 150
            };
            textBox.SetResourceReference(TextBox.StyleProperty, "TextBoxSmallStyle");

            textBox.LostFocus += (s, e) =>
            {
                try
                {
                    var newArray = JArray.Parse(textBox.Text);
                    UpdateJsonValue(propertyName, newArray);
                }
                catch
                {
                    // Invalid JSON, revert
                    textBox.Text = array?.ToString(Formatting.None) ?? "[]";
                }
            };

            return textBox;
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
        /// Supports nested property paths like "parent.child"
        /// </summary>
        private void UpdateJsonValue(string propertyPath, object? value)
        {
            if (_jObject == null) return;

            try
            {
                // Split property path for nested properties
                var parts = propertyPath.Split('.');
                
                if (parts.Length == 1)
                {
                    // Simple property
                    _jObject[propertyPath] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                }
                else
                {
                    // Nested property - navigate to parent
                    JToken current = _jObject;
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        current = current[parts[i]];
                        if (current == null)
                            return; // Path doesn't exist
                    }
                    
                    // Set the final property
                    if (current is JObject parentObj)
                    {
                        parentObj[parts[parts.Length - 1]] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
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
