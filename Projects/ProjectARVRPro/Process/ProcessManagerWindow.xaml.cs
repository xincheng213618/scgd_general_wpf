using ColorVision.UI;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProjectARVRPro.Process
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// ProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessManagerWindow : Window
    {
        private ProcessMeta _currentSelectedMeta;
        private INotifyPropertyChanged _currentRecipeConfig;
        private INotifyPropertyChanged _currentFixConfig;
        private INotifyPropertyChanged _currentProcessConfig;
        private PropertyChangedEventHandler _recipeConfigPropertyChangedHandler;
        private PropertyChangedEventHandler _fixConfigPropertyChangedHandler;
        private PropertyChangedEventHandler _processConfigPropertyChangedHandler;

        public ProcessManagerWindow()
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

            CleanupConfigHandler(ref _currentRecipeConfig, ref _recipeConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentFixConfig, ref _fixConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentProcessConfig, ref _processConfigPropertyChangedHandler);
        }

        private void CleanupConfigHandler(ref INotifyPropertyChanged config, ref PropertyChangedEventHandler handler)
        {
            if (config != null && handler != null)
            {
                config.PropertyChanged -= handler;
                config = null;
                handler = null;
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

            // Cleanup previous config handlers
            CleanupConfigHandler(ref _currentRecipeConfig, ref _recipeConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentFixConfig, ref _fixConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentProcessConfig, ref _processConfigPropertyChangedHandler);

            var manager = DataContext as ProcessManager;
            var selectedMeta = manager?.SelectedProcessMeta;

            if (selectedMeta == null)
            {
                // Show placeholder text
                PropertyPanel.Children.Add(new TextBlock
                {
                    Text = "请选择一个处理项查看配置",
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

            // Add Recipe config if available
            var recipeConfig = selectedMeta.Process?.GetRecipeConfig();
            if (recipeConfig != null)
            {
                AddConfigSection(recipeConfig, "Recipe 配置", selectedMeta, ConfigType.Recipe);
            }

            // Add Fix config if available
            var fixConfig = selectedMeta.Process?.GetFixConfig();
            if (fixConfig != null)
            {
                AddConfigSection(fixConfig, "Fix 配置", selectedMeta, ConfigType.Fix);
            }

            // Add Process config if available
            var processConfig = selectedMeta.Process?.GetProcessConfig();
            if (processConfig != null)
            {
                AddConfigSection(processConfig, "Process 配置", selectedMeta, ConfigType.Process);
            }
        }

        private enum ConfigType
        {
            Recipe,
            Fix,
            Process
        }

        private void SelectedMeta_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Refresh when Process changes (which may change the configs)
            if (e.PropertyName == nameof(ProcessMeta.Process))
            {
                RefreshPropertyPanel();
            }
        }

        private void AddMetaInfoSection(ProcessMeta meta)
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
            AddLabeledText(stack, "流程模板:", meta.FlowTemplate);

            // Process Type (read-only)
            AddLabeledText(stack, "处理类:", meta.ProcessTypeName);

            // Enabled status
            AddLabeledCheckBox(stack, "是否启用:", meta.IsEnabled, isChecked => meta.IsEnabled = isChecked);

            PropertyPanel.Children.Add(border);
        }

        private void AddConfigSection(object config, string headerText, ProcessMeta meta, ConfigType configType)
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
                Text = headerText,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Generate property editor controls
            var configPanel = PropertyEditorHelper.GenPropertyEditorControl(config);

            // Subscribe to config changes to persist (with proper cleanup)
            if (config is INotifyPropertyChanged notifyConfig)
            {
                PropertyChangedEventHandler handler = (s, e) =>
                {
                    // Save config changes based on type
                    switch (configType)
                    {
                        case ConfigType.Recipe:
                            RecipeManager.GetInstance().Save();
                            break;
                        case ConfigType.Fix:
                            FixManager.GetInstance().Save();
                            break;
                        case ConfigType.Process:
                            meta.ConfigJson = JsonConvert.SerializeObject(config);
                            break;
                    }
                };

                notifyConfig.PropertyChanged += handler;

                // Store reference for cleanup
                switch (configType)
                {
                    case ConfigType.Recipe:
                        _currentRecipeConfig = notifyConfig;
                        _recipeConfigPropertyChangedHandler = handler;
                        break;
                    case ConfigType.Fix:
                        _currentFixConfig = notifyConfig;
                        _fixConfigPropertyChangedHandler = handler;
                        break;
                    case ConfigType.Process:
                        _currentProcessConfig = notifyConfig;
                        _processConfigPropertyChangedHandler = handler;
                        break;
                }
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

        private void AddLabeledCheckBox(StackPanel parent, string label, bool value, Action<bool> onChanged)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            dock.Children.Add(new TextBlock
            {
                Text = label,
                Width = 70,
                VerticalAlignment = VerticalAlignment.Center
            });

            var checkBox = new CheckBox
            {
                IsChecked = value,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += (s, e) => onChanged?.Invoke(true);
            checkBox.Unchecked += (s, e) => onChanged?.Invoke(false);

            dock.Children.Add(checkBox);
            parent.Children.Add(dock);
        }
    }
}
