using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Batch
{
    public class PreTypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IPreProcess process)
            {
                var metadata = PreProcessMetadata.FromProcess(process);
                return metadata.DisplayName;
            }
            return value?.GetType().Name ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class PreProcessTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IPreProcess process)
            {
                var metadata = PreProcessMetadata.FromProcess(process);
                return metadata.GetTooltipText();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// PreProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PreProcessManagerWindow : Window
    {
        private PreProcessMeta _currentSelectedMeta;
        private INotifyPropertyChanged _currentConfig;
        private PropertyChangedEventHandler _configPropertyChangedHandler;

        public PreProcessManagerWindow()
        {
            InitializeComponent();
            Closing += Window_Closing;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Cleanup event handlers on window close
            CleanupEventHandlers();
        }

        private void CleanupEventHandlers()
        {
            if (_currentSelectedMeta != null)
            {
                _currentSelectedMeta.PropertyChanged -= SelectedMeta_PropertyChanged;
                _currentSelectedMeta = null;
            }

            if (_currentConfig != null && _configPropertyChangedHandler != null)
            {
                _currentConfig.PropertyChanged -= _configPropertyChangedHandler;
                _currentConfig = null;
                _configPropertyChangedHandler = null;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPropertyPanel();
        }

        private void RefreshPropertyPanel()
        {
            PropertyPanel.Children.Clear();

            // Cleanup previous config handler
            if (_currentConfig != null && _configPropertyChangedHandler != null)
            {
                _currentConfig.PropertyChanged -= _configPropertyChangedHandler;
                _currentConfig = null;
                _configPropertyChangedHandler = null;
            }

            var manager = DataContext as PreProcessManager;
            var selectedMeta = manager?.SelectedProcessMeta;

            if (selectedMeta == null)
            {
                // Show placeholder text
                PropertyPanel.Children.Add(new TextBlock 
                { 
                    Text = "请选择一个预处理项查看配置", 
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return;
            }

            // Unsubscribe from previous meta if any
            if (_currentSelectedMeta != null && _currentSelectedMeta != selectedMeta)
            {
                _currentSelectedMeta.PropertyChanged -= SelectedMeta_PropertyChanged;
            }

            if (_currentSelectedMeta != selectedMeta)
            {
                _currentSelectedMeta = selectedMeta;
                _currentSelectedMeta.PropertyChanged += SelectedMeta_PropertyChanged;
            }

            // Add meta info section
            AddMetaInfoSection(selectedMeta);

            // Add pre-processor config if available
            var config = selectedMeta.PreProcess?.GetConfig();
            if (config != null)
            {
                AddConfigSection(config, selectedMeta);
            }
        }

        private void SelectedMeta_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Refresh when PreProcess changes (which may change the config)
            if (e.PropertyName == nameof(PreProcessMeta.PreProcess))
            {
                RefreshPropertyPanel();
            }
        }

        private void AddMetaInfoSection(PreProcessMeta meta)
        {
            var border = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            border.Child = stack;

            // Header
            stack.Children.Add(new TextBlock 
            { 
                Text = "基本信息", 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 8) 
            });

            // Name
            AddLabeledTextBox(stack, "名称:", meta.Name, text => meta.Name = text);

            // Template Name (read-only)
            AddLabeledText(stack, "流程模板:", meta.TemplateName);

            // Process Type (read-only)
            AddLabeledText(stack, "处理类:", meta.ProcessDisplayName);

            // Tag (editable)
            AddLabeledTextBox(stack, "标签:", meta.Tag ?? "", text => meta.Tag = text);

            // Description (read-only)
            if (!string.IsNullOrEmpty(meta.ProcessDescription))
            {
                AddLabeledText(stack, "描述:", meta.ProcessDescription);
            }

            PropertyPanel.Children.Add(border);
        }

        private void AddConfigSection(object config, PreProcessMeta meta)
        {
            var border = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            border.Child = stack;

            // Header
            stack.Children.Add(new TextBlock 
            { 
                Text = "预处理配置", 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 8) 
            });

            // Generate property editor controls
            var configPanel = PropertyEditorHelper.GenPropertyEditorControl(config);
            
            // Subscribe to config changes to persist (with proper cleanup)
            if (config is INotifyPropertyChanged notifyConfig)
            {
                _currentConfig = notifyConfig;
                _configPropertyChangedHandler = (s, e) =>
                {
                    // Save config changes
                    meta.ConfigJson = JsonConvert.SerializeObject(config);
                };
                _currentConfig.PropertyChanged += _configPropertyChangedHandler;
            }

            stack.Children.Add(configPanel);

            PropertyPanel.Children.Add(border);
        }

        private void AddLabeledText(StackPanel parent, string label, string value)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            dock.Children.Add(new TextBlock 
            { 
                Text = label, 
                Width = 70, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            dock.Children.Add(new TextBlock 
            { 
                Text = value ?? "", 
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            parent.Children.Add(dock);
        }

        private void AddLabeledTextBox(StackPanel parent, string label, string value, Action<string> onChanged)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            dock.Children.Add(new TextBlock 
            { 
                Text = label, 
                Width = 70, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            
            var textBox = new TextBox 
            { 
                Text = value ?? "", 
                VerticalAlignment = VerticalAlignment.Center
            };
            // Use LostFocus instead of TextChanged to reduce update frequency
            textBox.LostFocus += (s, e) => onChanged?.Invoke(textBox.Text);
            
            dock.Children.Add(textBox);
            parent.Children.Add(dock);
        }
    }
}
