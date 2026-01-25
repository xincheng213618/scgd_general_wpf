using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Templates.Jsons
{

    public class EditTemplateJsonConfig :IConfig
    {
        public static EditTemplateJsonConfig Instance { get; set; } = ConfigService.Instance.GetRequiredService<EditTemplateJsonConfig>();

        public double Width { get => _Width; set { _Width = value; } }
        private double _Width = double.NaN;

        public bool UsePropertyEditor { get; set; } = false; // Default to text editor mode
    }

    public partial class EditTemplateJson : UserControl, ITemplateUserControl
    {

        private string Description { get; set; }
        private bool _isInPropertyEditorMode = false;
        private bool _isSyncingFromPropertyEditor = false;

        public EditTemplateJson(string description)
        {
            Description = description;
            InitializeComponent();
            this.Width = EditTemplateJsonConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateJsonConfig.Instance.Width = this.ActualWidth;
            };
            textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            textEditor.ShowLineNumbers = true;
            textEditor.TextChanged += TextEditor_TextChanged;

            // Set initial mode from config
            _isInPropertyEditorMode = EditTemplateJsonConfig.Instance.UsePropertyEditor;
            EditorModeToggle.IsChecked = _isInPropertyEditorMode;
            UpdateEditorMode();

            // Subscribe to property editor changes
            propertyEditor.JsonValueChanged += PropertyEditor_JsonValueChanged;
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            DebounceTimer.AddOrResetTimer("EditTemplateJsonChanged", 50, EditTemplateJsonChanged);
        }

        public void EditTemplateJsonChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IEditTemplateJson != null)
                {
                    IEditTemplateJson.JsonValue = textEditor.Text;
                }
            });
        }


        private IEditTemplateJson IEditTemplateJson;


        public void SetParam(object param)
        {
            if (param is IEditTemplateJson editTemplateJson)
            {
                this.DataContext = param; 
                if (IEditTemplateJson !=null)
                    IEditTemplateJson.JsonValueChanged -= IEditTemplateJson_JsonValueChanged;
                IEditTemplateJson = editTemplateJson;
                textEditor.Text = IEditTemplateJson.JsonValue;
                IEditTemplateJson.JsonValueChanged += IEditTemplateJson_JsonValueChanged;

                textEditor.TextChanged -= TextEditor_TextChanged;
                textEditor.TextChanged += TextEditor_TextChanged;

                // If in property editor mode, refresh the property editor with new data
                if (_isInPropertyEditorMode)
                {
                    try
                    {
                        propertyEditor.SetJson(IEditTemplateJson.JsonValue);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing property editor: {ex.Message}");
                    }
                }
            }
            DescriptionButton.IsChecked = false;
        }

        private void IEditTemplateJson_JsonValueChanged(object? sender, EventArgs e)
        {
            textEditor.Text = IEditTemplateJson.JsonValue;
        }

        private string texttemp;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(sender is ToggleButton toggleButton)
            {
                if (toggleButton.IsChecked == true)
                {
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    texttemp = textEditor.Text;
                    textEditor.Text = Description;
                }
                else
                {

                    textEditor.Text = texttemp;
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    textEditor.TextChanged += TextEditor_TextChanged;
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.json.cn/",
                UseShellExecute = true
            });
            Common.NativeMethods.Clipboard.SetText(IEditTemplateJson.JsonValue);
        }

        private void EditorModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                _isInPropertyEditorMode = toggleButton.IsChecked == true;
                EditTemplateJsonConfig.Instance.UsePropertyEditor = _isInPropertyEditorMode;

                if (_isInPropertyEditorMode)
                {
                    // Switch to property editor mode
                    SwitchToPropertyEditorMode();
                }
                else
                {
                    // Switch back to text mode
                    SwitchToTextMode();
                }
            }
        }

        private void SwitchToPropertyEditorMode()
        {
            try
            {
                // Load current JSON into property editor
                propertyEditor.SetJson(textEditor.Text);

                // Show property editor, hide text editor
                textEditor.Visibility = Visibility.Collapsed;
                propertyEditor.Visibility = Visibility.Visible;

                // Update toggle button text
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.TextEdit;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换到属性编辑器失败: {ex.Message}\n\n请检查 JSON 格式是否正确。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Revert toggle state
                EditorModeToggle.IsChecked = false;
                _isInPropertyEditorMode = false;
            }
        }

        private void SwitchToTextMode()
        {
            try
            {
                // Get JSON from property editor
                var json = propertyEditor.GetJson();
                if (!string.IsNullOrEmpty(json))
                {
                    // Update text editor
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    textEditor.Text = json;
                    textEditor.TextChanged += TextEditor_TextChanged;
                }

                // Show text editor, hide property editor
                textEditor.Visibility = Visibility.Visible;
                propertyEditor.Visibility = Visibility.Collapsed;

                // Update toggle button text
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.PropertyEdit;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换到文本编辑器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateEditorMode()
        {
            if (_isInPropertyEditorMode)
            {
                textEditor.Visibility = Visibility.Collapsed;
                propertyEditor.Visibility = Visibility.Visible;
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.TextEdit;
            }
            else
            {
                textEditor.Visibility = Visibility.Visible;
                propertyEditor.Visibility = Visibility.Collapsed;
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.PropertyEdit;
            }
        }

        private void PropertyEditor_JsonValueChanged(object? sender, string json)
        {
            if (_isSyncingFromPropertyEditor)
                return;

            _isSyncingFromPropertyEditor = true;
            try
            {
                // Update the IEditTemplateJson value when property editor changes
                if (IEditTemplateJson != null)
                {
                    IEditTemplateJson.JsonValue = json;
                }
            }
            finally
            {
                _isSyncingFromPropertyEditor = false;
            }
        }
    }
}
