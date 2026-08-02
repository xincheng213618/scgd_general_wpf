#pragma warning disable CA1822,CA1859,CS8622,CS8625
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// ProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessManagerWindow : Window
    {
        private ProcessMeta _currentSelectedMeta;
        private ProcessManager? _recipeImportManager;
        private readonly List<(INotifyPropertyChanged obj, PropertyChangedEventHandler handler)> _configSubscriptions = new();

        public ProcessManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Closing += Window_Closing;
        }

        private void PreProcessManager_Click(object sender, RoutedEventArgs e)
        {
            PreProcessManager.GetInstance().Edit();
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

            if (_recipeImportManager != null)
            {
                _recipeImportManager.RecipeConfigImported -= ProcessManager_RecipeConfigImported;
                _recipeImportManager = null;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProcessManager manager)
            {
                _recipeImportManager = manager;
                _recipeImportManager.RecipeConfigImported += ProcessManager_RecipeConfigImported;
            }
            RefreshConfigPanels();
        }

        private void ProcessManager_RecipeConfigImported(object? sender, EventArgs e)
        {
            RefreshConfigPanels();
        }

        private void MetaNameEditTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                MetaNameEditTextBox.Focus();
                MetaNameEditTextBox.SelectAll();
            }));
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProcessManager manager && lvMetas.SelectedItem is ProcessMeta selectedMeta)
            {
                manager.SelectedProcessMeta = selectedMeta;
                manager.SelectedResultParserMeta = null;
                lvResultParsers.SelectedItem = null;
            }
            RefreshConfigPanels();
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProcessManager manager)
            {
                manager.SelectedProcessMeta = null;
                lvMetas.SelectedItem = null;
            }
            RefreshConfigPanels();
        }

        private void ResultParserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProcessManager manager && lvResultParsers.SelectedItem is ProcessMeta selectedMeta)
            {
                manager.SelectedResultParserMeta = selectedMeta;
                manager.SelectedProcessMeta = null;
                lvMetas.SelectedItem = null;
            }
            RefreshConfigPanels();
        }

        private void ResultParserListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ProcessManager manager && manager.UpdateResultParserCommand.CanExecute(null))
            {
                manager.UpdateResultParserCommand.Execute(null);
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || !ReferenceEquals(e.OriginalSource, sender))
                return;

            if (DataContext is ProcessManager manager)
            {
                if (MainTabControl.SelectedIndex == 0)
                    manager.SelectedResultParserMeta = null;
                else
                    manager.SelectedProcessMeta = null;
            }

            RefreshConfigPanels();
        }

        private void RefreshConfigPanels()
        {
            if (!IsInitialized)
                return;

            RecipePanel.Children.Clear();
            ProcessPanel.Children.Clear();
            PictureSwitchPanel.Children.Clear();
            ResultRecipePanel.Children.Clear();
            ResultProcessPanel.Children.Clear();

            CleanupConfigSubscriptions();

            var manager = DataContext as ProcessManager;
            var selectedMeta = manager?.SelectedConfigurationMeta;

            if (selectedMeta == null)
            {
                if (_currentSelectedMeta != null)
                {
                    _currentSelectedMeta.PropertyChanged -= SelectedMeta_PropertyChanged;
                    _currentSelectedMeta = null;
                }

                AddPlaceholderText(RecipePanel);
                AddPlaceholderText(ProcessPanel);
                AddPlaceholderText(PictureSwitchPanel);
                AddPlaceholderText(ResultRecipePanel, "请选择一个解析映射");
                AddPlaceholderText(ResultProcessPanel, "请选择一个解析映射");
                return;
            }

            if (_currentSelectedMeta != null && _currentSelectedMeta != selectedMeta)
            {
                _currentSelectedMeta.PropertyChanged -= SelectedMeta_PropertyChanged;
            }

            if (_currentSelectedMeta != selectedMeta)
            {
                _currentSelectedMeta = selectedMeta;
                _currentSelectedMeta.PropertyChanged += SelectedMeta_PropertyChanged;
            }

            bool isResultParser = ReferenceEquals(selectedMeta, manager?.SelectedResultParserMeta);
            StackPanel processPanel = isResultParser ? ResultProcessPanel : ProcessPanel;
            StackPanel recipePanel = isResultParser ? ResultRecipePanel : RecipePanel;

            if (isResultParser)
            {
                AddPlaceholderText(ProcessPanel);
                AddPlaceholderText(RecipePanel);
                AddPlaceholderText(PictureSwitchPanel);
            }
            else
            {
                AddPlaceholderText(ResultProcessPanel, "请选择一个解析映射");
                AddPlaceholderText(ResultRecipePanel, "请选择一个解析映射");
            }

            var recipeConfig = selectedMeta.Process?.GetRecipeConfig();
            if (recipeConfig != null)
            {
                AddConfigToPanel(recipeConfig, recipePanel, selectedMeta, ConfigType.Recipe);
            }
            else
            {
                AddNoConfigText(recipePanel, "无Recipe配置");
            }

            var processConfig = selectedMeta.Process?.GetProcessConfig();
            if (processConfig != null)
            {
                AddConfigToPanel(processConfig, processPanel, selectedMeta, ConfigType.Process);
            }
            else
            {
                AddNoConfigText(processPanel, "无Process配置");
            }

            if (!isResultParser)
                AddPictureSwitchConfigToPanel(selectedMeta.PictureSwitchConfig, PictureSwitchPanel);
        }

        private void AddPlaceholderText(StackPanel panel, string message = "请选择一个处理项")
        {
            panel.Children.Add(new TextBlock
            {
                Text = message,
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
                ConfigType.Recipe => () => ProcessManager.GetInstance().SaveProcessGroups(),
                ConfigType.Process => () => { meta.ConfigJson = JsonConvert.SerializeObject(config); },
                _ => () => { }
            };

            SubscribeRecursively(config, saveAction);

            panel.Children.Add(configPanel);
        }

        private void AddPictureSwitchConfigToPanel(PictureSwitchConfig config, StackPanel panel)
        {
            panel.Children.Add(CreateCheckBox("启用切图", config, nameof(PictureSwitchConfig.IsEnabled)));

            var modeBox = new ComboBox
            {
                IsEnabled = false,
                Margin = new Thickness(0, 0, 0, 8)
            };
            modeBox.Items.Add("雷鸟");
            modeBox.SelectedIndex = 0;
            panel.Children.Add(CreateLabeledControl("模式", modeBox));

            var presetBox = new ComboBox
            {
                ItemsSource = PictureSwitchConfig.Presets,
                DisplayMemberPath = nameof(PictureSwitchPreset.DisplayText),
                Margin = new Thickness(0, 0, 0, 8)
            };
            presetBox.SelectedItem = PictureSwitchConfig.Presets.FirstOrDefault(p => string.Equals(p.Command, config.SendCommand, StringComparison.OrdinalIgnoreCase));
            presetBox.SelectionChanged += (s, e) =>
            {
                if (presetBox.SelectedItem is PictureSwitchPreset preset)
                    config.SendCommand = preset.Command;
            };
            panel.Children.Add(CreateLabeledControl("预设切图", presetBox));

            panel.Children.Add(CreateTextBoxRow("发送值", config, nameof(PictureSwitchConfig.SendCommand)));
            panel.Children.Add(CreateTextBoxRow("返回值", config, nameof(PictureSwitchConfig.ExpectedResponse)));
            panel.Children.Add(CreateTextBoxRow("超时(ms)", config, nameof(PictureSwitchConfig.TimeoutMs)));
            panel.Children.Add(CreateTextBoxRow("成功后延时(ms)", config, nameof(PictureSwitchConfig.SuccessDelayMs)));
        }

        private static CheckBox CreateCheckBox(string content, object source, string propertyName)
        {
            var checkBox = new CheckBox
            {
                Content = content,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            return checkBox;
        }

        private static FrameworkElement CreateTextBoxRow(string label, object source, string propertyName)
        {
            var textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            return CreateLabeledControl(label, textBox);
        }

        private static FrameworkElement CreateLabeledControl(string label, FrameworkElement control)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 2)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 8)
            };

            Grid.SetColumn(labelBlock, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(control);
            return grid;
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
