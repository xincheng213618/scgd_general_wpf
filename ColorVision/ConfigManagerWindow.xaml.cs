using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision
{
    public class MenuConfigManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => ColorVision.Properties.Resources.ConfigurationManagement;

        public override int Order => 9009;

        public override void Execute()
        {
            new ConfigManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// Assembly group containing configs
    /// </summary>
    public class AssemblyGroup : ViewModelBase
    {
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        private string _displayName;

        public Assembly Assembly { get; set; }
        public List<ConfigItem> Configs { get; set; } = new List<ConfigItem>();
    }

    /// <summary>
    /// Individual config item
    /// </summary>
    public class ConfigItem : ViewModelBase
    {
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        private string _displayName;

        public Type ConfigType { get; set; }
        public IConfig ConfigInstance { get; set; }
        public string AssemblyName { get; set; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        private bool _isSelected;
    }

    /// <summary>
    /// ConfigManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigManagerWindow : Window
    {
        public ConfigManagerWindow()
        {
            InitializeComponent();
        }

        public ObservableCollection<AssemblyGroup> AssemblyGroups { get; set; } = new ObservableCollection<AssemblyGroup>();
        private List<ConfigItem> _allConfigs = new List<ConfigItem>();
        private Dictionary<string, FrameworkElement> _assemblySectionElements = new Dictionary<string, FrameworkElement>();
        private ConfigItem _selectedConfig = null;
        private int _configColumns = 3;

        public int ConfigColumns
        {
            get => _configColumns;
            set
            {
                if (_configColumns != value && value > 0 && value <= 6)
                {
                    _configColumns = value;
                    RebuildConfigGrid();
                }
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildConfigData();
            PopulateAssemblyList();
            RebuildConfigGrid();

            // 显示汇总信息
            int totalConfigs = _allConfigs.Count;
            int totalAssemblies = AssemblyGroups.Count;
            SummaryText.Text = $"共计 {totalAssemblies} 个程序集，{totalConfigs} 个配置类型";
        }

        /// <summary>
        /// Build config data from ConfigHandler
        /// </summary>
        private void BuildConfigData()
        {
            var configs = ConfigHandler.GetInstance().Configs.ToList();
            var assemblyGroups = configs.GroupBy(kvp => kvp.Key.Assembly);

            foreach (var assemblyGroup in assemblyGroups.OrderBy(g => g.Key.GetName().Name))
            {
                var group = new AssemblyGroup
                {
                    DisplayName = assemblyGroup.Key.GetName().Name,
                    Assembly = assemblyGroup.Key
                };

                foreach (var config in assemblyGroup.OrderBy(c => GetDisplayName(c.Key)))
                {
                    var configItem = new ConfigItem
                    {
                        DisplayName = GetDisplayName(config.Key),
                        ConfigType = config.Key,
                        ConfigInstance = config.Value,
                        AssemblyName = group.DisplayName
                    };
                    group.Configs.Add(configItem);
                    _allConfigs.Add(configItem);
                }

                AssemblyGroups.Add(group);
            }
        }

        /// <summary>
        /// Populate the assembly list on the left
        /// </summary>
        private void PopulateAssemblyList()
        {
            AssemblyListView.ItemsSource = AssemblyGroups;
        }

        /// <summary>
        /// Rebuild the config grid in the middle panel
        /// </summary>
        private void RebuildConfigGrid(string searchText = null)
        {
            ConfigContainer.Children.Clear();
            _assemblySectionElements.Clear();

            var groupsToShow = AssemblyGroups.AsEnumerable();
            
            // Filter by search if provided
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearch = searchText.ToLower();
                groupsToShow = AssemblyGroups
                    .Select(g => new AssemblyGroup
                    {
                        DisplayName = g.DisplayName,
                        Assembly = g.Assembly,
                        Configs = g.Configs.Where(c => c.DisplayName.ToLower().Contains(lowerSearch) || 
                                                       c.AssemblyName.ToLower().Contains(lowerSearch))
                                           .ToList()
                    })
                    .Where(g => g.Configs.Any());
            }

            foreach (var group in groupsToShow)
            {
                if (group.Configs.Count == 0) continue;

                // Assembly header
                var headerBorder = new Border
                {
                    Background = (Brush)Application.Current.FindResource("SecondaryRegionBrush"),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, ConfigContainer.Children.Count > 0 ? 20 : 0, 0, 10)
                };

                var headerText = new TextBlock
                {
                    Text = group.DisplayName,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
                };
                headerBorder.Child = headerText;
                ConfigContainer.Children.Add(headerBorder);

                // Store reference for scrolling
                _assemblySectionElements[group.DisplayName] = headerBorder;

                // Config grid (dynamic columns)
                var uniformGrid = new UniformGrid
                {
                    Columns = _configColumns,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                foreach (var config in group.Configs)
                {
                    var configButton = CreateConfigButton(config);
                    uniformGrid.Children.Add(configButton);
                }

                ConfigContainer.Children.Add(uniformGrid);
            }
        }

        /// <summary>
        /// Create a button for a config item
        /// </summary>
        private Button CreateConfigButton(ConfigItem config)
        {
            var button = new Button
            {
                Margin = new Thickness(5),
                Padding = new Thickness(10, 8,10,8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.FindResource("GlobalBackground"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Tag = config
            };

            var stackPanel = new StackPanel();
            
            var nameText = new TextBlock
            {
                Text = config.DisplayName,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(nameText);

            var assemblyText = new TextBlock
            {
                Text = config.AssemblyName,
                FontSize = 10,
                Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 3, 0, 0),
                Opacity = 0.7
            };
            stackPanel.Children.Add(assemblyText);

            button.Content = stackPanel;
            button.Click += ConfigButton_Click;

            // Update selection visual state
            UpdateButtonSelectionState(button, config.IsSelected);

            return button;
        }

        /// <summary>
        /// Update button visual state based on selection
        /// </summary>
        private void UpdateButtonSelectionState(Button button, bool isSelected)
        {
            if (isSelected)
            {
                button.BorderBrush = (Brush)Application.Current.FindResource("PrimaryBrush");
                button.BorderThickness = new Thickness(2);
            }
            else
            {
                button.BorderBrush = (Brush)Application.Current.FindResource("BorderBrush");
                button.BorderThickness = new Thickness(1);
            }
        }

        /// <summary>
        /// Handle config button click
        /// </summary>
        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ConfigItem config)
            {
                // Deselect previous config
                if (_selectedConfig != null && _selectedConfig != config)
                {
                    _selectedConfig.IsSelected = false;
                }

                // Select current config
                config.IsSelected = true;
                _selectedConfig = config;

                // Update all button visual states
                UpdateAllButtonStates();

                DisplayConfigProperty(config);
            }
        }

        /// <summary>
        /// Update all button selection states
        /// </summary>
        private void UpdateAllButtonStates()
        {
            foreach (var child in ConfigContainer.Children)
            {
                if (child is UniformGrid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Button btn && btn.Tag is ConfigItem cfg)
                        {
                            UpdateButtonSelectionState(btn, cfg.IsSelected);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle assembly list selection change
        /// </summary>
        private void AssemblyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AssemblyListView.SelectedItem is AssemblyGroup group)
            {
                // Scroll to the assembly section in the middle panel
                if (_assemblySectionElements.TryGetValue(group.DisplayName, out var element))
                {
                    element.BringIntoView();
                }
            }
        }

        /// <summary>
        /// Get display name for a type, preferring DisplayNameAttribute
        /// </summary>
        private string GetDisplayName(Type type)
        {
            // Try to get DisplayName attribute
            var displayNameAttr = type.GetCustomAttribute<DisplayNameAttribute>();
            if (displayNameAttr != null && !string.IsNullOrWhiteSpace(displayNameAttr.DisplayName))
            {
                return displayNameAttr.DisplayName;
            }

            // Fallback to type name
            return type.Name;
        }

        /// <summary>
        /// Display property editor for selected config
        /// </summary>
        private void DisplayConfigProperty(ConfigItem config)
        {
            if (config.ConfigInstance == null) return;

            PropertyTitle.Text = $"配置: {config.DisplayName}";
            SummaryText1.Text = $"当前选择: {config.DisplayName}";

            // Remove old property editor if exists
            if (PropertyContainer.Child is IDisposable disposable)
            {
                disposable.Dispose();
            }
            PropertyContainer.Child = null;

            // Create property editor using PropertyEditorHelper
            var propertyPanel = PropertyEditorHelper.GenPropertyEditorControl(config.ConfigInstance);
            
            if (propertyPanel == null)
            {
                PropertyContainer.Child = new TextBlock
                {
                    Text = "无法加载配置属性",
                    Margin = new Thickness(10),
                    Foreground = (Brush)Application.Current.FindResource("GlobalTextBrush")
                };
                return;
            }

            // Wrap in ScrollViewer for better UX
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(5)
            };
            scrollViewer.Content = propertyPanel;

            PropertyContainer.Child = scrollViewer;
        }

        /// <summary>
        /// Handle search text change
        /// </summary>
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchText = textBox.Text?.Trim() ?? "";
                
                // Check if selected config will be filtered out
                if (!string.IsNullOrWhiteSpace(searchText) && _selectedConfig != null)
                {
                    var lowerSearch = searchText.ToLower();
                    bool selectedConfigMatches = _selectedConfig.DisplayName.ToLower().Contains(lowerSearch) || 
                                                 _selectedConfig.AssemblyName.ToLower().Contains(lowerSearch);
                    
                    if (!selectedConfigMatches)
                    {
                        // Clear selection and right panel
                        _selectedConfig.IsSelected = false;
                        _selectedConfig = null;
                        ClearPropertyDisplay();
                    }
                }
                
                RebuildConfigGrid(string.IsNullOrWhiteSpace(searchText) ? null : searchText);
            }
        }

        /// <summary>
        /// Clear property display
        /// </summary>
        private void ClearPropertyDisplay()
        {
            PropertyTitle.Text = "选择一个配置查看详情";
            
            if (PropertyContainer.Child is IDisposable disposable)
            {
                disposable.Dispose();
            }
            PropertyContainer.Child = null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolderAndSelectFile(ConfigHandler.GetInstance().ConfigFilePath);
        }

        private void ColumnCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                ConfigColumns = comboBox.SelectedIndex + 1; // Index 0 = 1 column, etc.
            }
        }
    }
}
