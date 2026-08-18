#pragma warning disable CA1822,CA1859,CS8622,CS8625
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// ProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessManagerWindow : Window
    {
        private const string ProcessMetaDragFormat = "ProjectARVRPro.Process.ProcessMeta";
        private ProcessMeta _currentSelectedMeta;
        private ProcessManager? _recipeImportManager;
        private readonly List<(INotifyPropertyChanged obj, PropertyChangedEventHandler handler)> _configSubscriptions = new();
        private Point _processDragStartPoint;
        private ProcessMeta? _draggedProcessMeta;
        private ScrollViewer? _processListScrollViewer;

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

        private void CopyableTextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            textBox.Focus();
            textBox.SelectAll();
            e.Handled = true;
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

        private void ProcessList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedProcessMeta = null;
            if (IsDragBlockedElement(e.OriginalSource as DependencyObject))
                return;

            _processDragStartPoint = e.GetPosition(lvMetas);
            _draggedProcessMeta = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content as ProcessMeta;
        }

        private void ProcessList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedProcessMeta == null)
                return;

            Point currentPosition = e.GetPosition(lvMetas);
            Vector difference = _processDragStartPoint - currentPosition;
            if (Math.Abs(difference.X) <= SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(difference.Y) <= SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            ProcessMeta draggedMeta = _draggedProcessMeta;
            _draggedProcessMeta = null;
            DragDrop.DoDragDrop(lvMetas, new DataObject(ProcessMetaDragFormat, draggedMeta), DragDropEffects.Move);
        }

        private void ProcessList_DragOver(object sender, DragEventArgs e)
        {
            ProcessMeta? draggedMeta = e.Data.GetData(ProcessMetaDragFormat) as ProcessMeta;
            ProcessMeta? targetMeta = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content as ProcessMeta;
            e.Effects = draggedMeta != null && targetMeta != null && !ReferenceEquals(draggedMeta, targetMeta)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            if (draggedMeta != null)
                AutoScrollProcessList(e.GetPosition(lvMetas));
            e.Handled = true;
        }

        private void ProcessList_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not ProcessManager manager)
                return;

            ProcessMeta? draggedMeta = e.Data.GetData(ProcessMetaDragFormat) as ProcessMeta;
            ListViewItem? targetItem = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (draggedMeta == null || targetItem?.Content is not ProcessMeta targetMeta || ReferenceEquals(draggedMeta, targetMeta))
                return;

            int sourceIndex = manager.ProcessMetas.IndexOf(draggedMeta);
            int targetIndex = manager.ProcessMetas.IndexOf(targetMeta);
            if (sourceIndex < 0 || targetIndex < 0)
                return;

            bool dropAfterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            int destinationIndex = targetIndex;
            if (dropAfterTarget && sourceIndex > targetIndex)
                destinationIndex++;
            else if (!dropAfterTarget && sourceIndex < targetIndex)
                destinationIndex--;

            if (manager.MoveMetaToIndex(draggedMeta, destinationIndex))
            {
                lvMetas.SelectedItem = draggedMeta;
                lvMetas.ScrollIntoView(draggedMeta);
            }

            e.Handled = true;
        }

        private void AutoScrollProcessList(Point position)
        {
            _processListScrollViewer ??= FindVisualChild<ScrollViewer>(lvMetas);
            if (_processListScrollViewer == null)
                return;

            const double scrollEdge = 24;
            if (position.Y < scrollEdge)
                _processListScrollViewer.LineUp();
            else if (position.Y > lvMetas.ActualHeight - scrollEdge)
                _processListScrollViewer.LineDown();
        }

        private static T? FindVisualParent<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T parent)
                    return parent;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject element) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                if (child is T result)
                    return result;

                T? descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        private static bool IsDragBlockedElement(DependencyObject? element)
        {
            while (element != null && element is not ListViewItem)
            {
                if (element is ButtonBase or TextBox)
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
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
                ConfigType.Recipe => () =>
                {
                    if (!ProcessManager.GetInstance().TrySaveProcessGroups())
                    {
                        MessageBox.Show(
                            this,
                            "Recipe 已修改，但保存 ProcessGroups.json 失败。请检查磁盘空间和文件权限后重试。",
                            "ColorVision",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                },
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
                if (_configSubscriptions.Any(subscription => ReferenceEquals(subscription.obj, notifyObj)))
                    return;

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
