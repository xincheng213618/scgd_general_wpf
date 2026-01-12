using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.PropertyEditor.Json
{
    /// <summary>
    /// UserControl that provides a property editor interface for JSON data
    /// </summary>
    public partial class JsonPropertyEditorControl : UserControl
    {
        private object? _currentObject;
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
                var jObject = JObject.Parse(json);

                // Convert to editable object
                _currentObject = JsonPropertyEditorConverter.ToObject(jObject);

                // Generate PropertyEditor controls
                PropertyPanel.Children.Clear();
                
                // Special handling for JsonObjectWrapper - use its GetProperties method
                if (_currentObject is JsonObjectWrapper wrapper)
                {
                    GeneratePropertiesForJsonWrapper(wrapper);
                }
                else
                {
                    var control = PropertyEditorHelper.GenPropertyEditorControl(_currentObject);
                    PropertyPanel.Children.Add(control);
                }

                // Subscribe to property changes
                if (_currentObject is System.ComponentModel.INotifyPropertyChanged notifyObj)
                {
                    notifyObj.PropertyChanged += (s, e) => OnJsonModified();
                }
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
        /// Generates property editor controls for JsonObjectWrapper
        /// </summary>
        private void GeneratePropertiesForJsonWrapper(JsonObjectWrapper wrapper)
        {
            var properties = wrapper.GetProperties().ToList();
            
            if (properties.Count == 0)
            {
                var noPropsText = new TextBlock
                {
                    Text = "没有可编辑的属性",
                    Margin = new Thickness(10),
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                };
                PropertyPanel.Children.Add(noPropsText);
                return;
            }

            // Group properties by category
            var categoryGroups = new Dictionary<string, List<System.Reflection.PropertyInfo>>();
            foreach (var prop in properties)
            {
                var categoryAttr = prop.GetCustomAttributes(typeof(System.ComponentModel.CategoryAttribute), true)
                    .FirstOrDefault() as System.ComponentModel.CategoryAttribute;
                string category = categoryAttr?.Category ?? "JSON Properties";

                if (!categoryGroups.ContainsKey(category))
                    categoryGroups[category] = new List<System.Reflection.PropertyInfo>();
                    
                categoryGroups[category].Add(prop);
            }

            // Generate UI for each category
            foreach (var categoryGroup in categoryGroups)
            {
                // Category header border
                var categoryBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 5, 0, 2),
                    CornerRadius = new CornerRadius(3)
                };

                var categoryText = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };

                categoryBorder.Child = categoryText;
                PropertyPanel.Children.Add(categoryBorder);

                // Generate controls for each property
                foreach (var prop in categoryGroup.Value)
                {
                    try
                    {
                        // Debug: Log property type
                        System.Diagnostics.Debug.WriteLine($"Property: {prop.Name}, Type: {prop.PropertyType.FullName}");
                        
                        // Get the appropriate editor for this property type
                        var editorType = PropertyEditorHelper.GetEditorTypeForPropertyType(prop.PropertyType);
                        
                        System.Diagnostics.Debug.WriteLine($"  EditorType found: {editorType?.FullName ?? "NULL"}");
                        
                        if (editorType != null)
                        {
                            var editor = PropertyEditorHelper.GetOrCreateEditor(editorType);
                            var control = editor.GenProperties(prop, wrapper);
                            if (control != null)
                                PropertyPanel.Children.Add(control);
                        }
                        else
                        {
                            // Fallback to default rendering
                            var dockPanel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                            
                            var label = new TextBlock
                            {
                                Text = $"{prop.Name} ({prop.PropertyType.Name})",
                                Width = 120,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            dockPanel.Children.Add(label);
                            DockPanel.SetDock(label, Dock.Left);
                            
                            var valueText = new TextBlock
                            {
                                Text = prop.GetValue(wrapper)?.ToString() ?? "(null)",
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            dockPanel.Children.Add(valueText);
                            
                            PropertyPanel.Children.Add(dockPanel);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other properties
                        System.Diagnostics.Debug.WriteLine($"Error generating control for property {prop.Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current JSON string
        /// </summary>
        public string? GetJson()
        {
            try
            {
                if (_currentObject == null)
                    return _originalJson;

                // Convert back to JObject
                var jObject = JsonPropertyEditorConverter.ToJObject(_currentObject);

                // Format JSON
                return jObject.ToString(Formatting.Indented);
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
