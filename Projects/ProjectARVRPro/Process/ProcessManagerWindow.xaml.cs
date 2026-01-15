using ColorVision.UI;
using Newtonsoft.Json;
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
            RefreshConfigPanels();
        }

        private void RefreshConfigPanels()
        {
            // Clear all panels
            RecipePanel.Children.Clear();
            FixPanel.Children.Clear();
            ProcessPanel.Children.Clear();

            // Cleanup previous config handlers
            CleanupConfigHandler(ref _currentRecipeConfig, ref _recipeConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentFixConfig, ref _fixConfigPropertyChangedHandler);
            CleanupConfigHandler(ref _currentProcessConfig, ref _processConfigPropertyChangedHandler);

            var manager = DataContext as ProcessManager;
            var selectedMeta = manager?.SelectedProcessMeta;

            if (selectedMeta == null)
            {
                // Show placeholder text in all panels
                AddPlaceholderText(RecipePanel);
                AddPlaceholderText(FixPanel);
                AddPlaceholderText(ProcessPanel);
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

            // Add Recipe config if available
            var recipeConfig = selectedMeta.Process?.GetRecipeConfig();
            if (recipeConfig != null)
            {
                AddConfigToPanel(recipeConfig, RecipePanel, selectedMeta, ConfigType.Recipe);
            }
            else
            {
                AddNoConfigText(RecipePanel, "无Recipe配置");
            }

            // Add Fix config if available
            var fixConfig = selectedMeta.Process?.GetFixConfig();
            if (fixConfig != null)
            {
                AddConfigToPanel(fixConfig, FixPanel, selectedMeta, ConfigType.Fix);
            }
            else
            {
                AddNoConfigText(FixPanel, "无Fix配置");
            }

            // Add Process config if available
            var processConfig = selectedMeta.Process?.GetProcessConfig();
            if (processConfig != null)
            {
                AddConfigToPanel(processConfig, ProcessPanel, selectedMeta, ConfigType.Process);
            }
            else
            {
                AddNoConfigText(ProcessPanel, "无Process配置");
            }
        }

        private void AddPlaceholderText(StackPanel panel)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "请选择一个处理项",
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });
        }

        private void AddNoConfigText(StackPanel panel, string message)
        {
            panel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });
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
                RefreshConfigPanels();
            }
        }

        private void AddConfigToPanel(object config, StackPanel panel, ProcessMeta meta, ConfigType configType)
        {
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

            panel.Children.Add(configPanel);
        }
    }
}
