using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
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
        private readonly List<(INotifyPropertyChanged obj, PropertyChangedEventHandler handler)> _configSubscriptions = new();

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

            CleanupConfigSubscriptions();
        }

        private void CleanupConfigSubscriptions()
        {
            foreach (var (obj, handler) in _configSubscriptions)
            {
                obj.PropertyChanged -= handler;
            }
            _configSubscriptions.Clear();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshConfigPanels();
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Rebind the ListView when group changes
            RefreshConfigPanels();
        }

        private void EditInterStepAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProcessMeta meta)
            {
                if (meta.InterStepAction == null)
                {
                    meta.InterStepAction = new InterStepAction();
                }

                var editor = new PropertyEditorWindow(meta.InterStepAction)
                {
                    Title = $"步间通信指令 - {meta.Name}",
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                editor.ShowDialog();
            }
        }

        private void RefreshConfigPanels()
        {
            // Clear all panels
            RecipePanel.Children.Clear();
            FixPanel.Children.Clear();
            ProcessPanel.Children.Clear();

            // Cleanup previous config handlers
            CleanupConfigSubscriptions();

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

            // Subscribe to config changes to persist (recursively for nested objects)
            Action saveAction = configType switch
            {
                ConfigType.Recipe => () => RecipeManager.GetInstance().Save(),
                ConfigType.Fix => () => FixManager.GetInstance().Save(),
                ConfigType.Process => () => { meta.ConfigJson = JsonConvert.SerializeObject(config); },
                _ => () => { }
            };

            SubscribeRecursively(config, saveAction);

            panel.Children.Add(configPanel);
        }

        /// <summary>
        /// Recursively subscribes to PropertyChanged on the object and all its nested INotifyPropertyChanged properties.
        /// This ensures that changes to nested objects (e.g., RecipeBase.Min/Max) also trigger the save action.
        /// </summary>
        private void SubscribeRecursively(object config, Action onChanged)
        {
            if (config is INotifyPropertyChanged notifyObj)
            {
                PropertyChangedEventHandler handler = (s, e) => onChanged();
                notifyObj.PropertyChanged += handler;
                _configSubscriptions.Add((notifyObj, handler));

                foreach (var prop in config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.CanRead && typeof(INotifyPropertyChanged).IsAssignableFrom(prop.PropertyType))
                    {
                        var nestedObj = prop.GetValue(config);
                        if (nestedObj != null)
                        {
                            SubscribeRecursively(nestedObj, onChanged);
                        }
                    }
                }
            }
        }
    }
}
