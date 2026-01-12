using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Controls;

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

                // Create wrapper that treats JObject like a class
                _currentObject = new JObjectPropertyWrapper(jObject);

                // Generate PropertyEditor controls using standard method
                PropertyPanel.Children.Clear();
                var control = PropertyEditorHelper.GenPropertyEditorControl(_currentObject);
                PropertyPanel.Children.Add(control);

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
        /// <summary>
        /// Gets the current JSON string
        /// </summary>
        public string? GetJson()
        {
            try
            {
                if (_currentObject == null)
                    return _originalJson;

                // Get JObject from wrapper
                if (_currentObject is JObjectPropertyWrapper wrapper)
                {
                    var jObject = wrapper.GetJObject();
                    return jObject.ToString(Formatting.Indented);
                }

                return _originalJson;
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
