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
    /// Tree node representing assembly, namespace, or config item
    /// </summary>
    public class ConfigTreeNode : ViewModelBase
    {
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        private string _displayName;

        public ConfigNodeType NodeType
        {
            get => _nodeType;
            set { _nodeType = value; OnPropertyChanged(); }
        }
        private ConfigNodeType _nodeType;

        public Type ConfigType
        {
            get => _configType;
            set { _configType = value; OnPropertyChanged(); }
        }
        private Type _configType;

        public IConfig ConfigInstance
        {
            get => _configInstance;
            set { _configInstance = value; OnPropertyChanged(); }
        }
        private IConfig _configInstance;

        public ObservableCollection<ConfigTreeNode> Children { get; set; } = new ObservableCollection<ConfigTreeNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
        private bool _isExpanded = true;
    }

    public enum ConfigNodeType
    {
        Assembly,
        Namespace,
        Config
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

        private ObservableCollection<ConfigTreeNode> _rootNodes = new ObservableCollection<ConfigTreeNode>();
        private List<ConfigTreeNode> _allConfigNodes = new List<ConfigTreeNode>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildConfigTree();
            ConfigTreeView.ItemsSource = _rootNodes;

            // 显示汇总信息
            int totalConfigs = _allConfigNodes.Count;
            int totalAssemblies = _rootNodes.Count;
            SummaryText.Text = $"共计 {totalAssemblies} 个程序集，{totalConfigs} 个配置类型";
        }

        /// <summary>
        /// Build hierarchical tree structure from configs
        /// </summary>
        private void BuildConfigTree()
        {
            var configs = ConfigHandler.GetInstance().Configs.ToList();
            var assemblyGroups = configs.GroupBy(kvp => kvp.Key.Assembly);

            foreach (var assemblyGroup in assemblyGroups.OrderBy(g => g.Key.GetName().Name))
            {
                var assemblyNode = new ConfigTreeNode
                {
                    DisplayName = assemblyGroup.Key.GetName().Name,
                    NodeType = ConfigNodeType.Assembly,
                    IsExpanded = true
                };

                // Group by namespace within assembly
                var namespaceGroups = assemblyGroup.GroupBy(kvp => kvp.Key.Namespace ?? "Global");

                foreach (var nsGroup in namespaceGroups.OrderBy(g => g.Key))
                {
                    // Build hierarchical namespace structure
                    var namespaceParts = nsGroup.Key.Split('.');
                    ConfigTreeNode currentParent = assemblyNode;

                    // Create intermediate namespace nodes
                    string currentNs = "";
                    foreach (var part in namespaceParts)
                    {
                        currentNs = string.IsNullOrEmpty(currentNs) ? part : $"{currentNs}.{part}";
                        
                        // Check if namespace node already exists
                        var existingNsNode = currentParent.Children.FirstOrDefault(n => 
                            n.NodeType == ConfigNodeType.Namespace && n.DisplayName == part);

                        if (existingNsNode == null)
                        {
                            var nsNode = new ConfigTreeNode
                            {
                                DisplayName = part,
                                NodeType = ConfigNodeType.Namespace,
                                IsExpanded = false
                            };
                            currentParent.Children.Add(nsNode);
                            currentParent = nsNode;
                        }
                        else
                        {
                            currentParent = existingNsNode;
                        }
                    }

                    // Add config items to the deepest namespace level
                    foreach (var config in nsGroup.OrderBy(c => GetDisplayName(c.Key)))
                    {
                        var configNode = new ConfigTreeNode
                        {
                            DisplayName = GetDisplayName(config.Key),
                            NodeType = ConfigNodeType.Config,
                            ConfigType = config.Key,
                            ConfigInstance = config.Value
                        };
                        currentParent.Children.Add(configNode);
                        _allConfigNodes.Add(configNode);
                    }
                }

                _rootNodes.Add(assemblyNode);
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
        /// Handle tree view selection change
        /// </summary>
        private void ConfigTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ConfigTreeNode node && node.NodeType == ConfigNodeType.Config)
            {
                DisplayConfigProperty(node);
            }
            else
            {
                ClearPropertyDisplay();
            }
        }

        /// <summary>
        /// Display property editor for selected config
        /// </summary>
        private void DisplayConfigProperty(ConfigTreeNode node)
        {
            if (node.ConfigInstance == null) return;

            PropertyTitle.Text = $"配置: {node.DisplayName}";
            SummaryText1.Text = $"当前选择: {node.DisplayName}";

            // Remove old property editor if exists
            if (PropertyContainer.Child is IDisposable disposable)
            {
                disposable.Dispose();
            }
            PropertyContainer.Child = null;

            // Create property editor using PropertyEditorHelper
            var propertyPanel = PropertyEditorHelper.GenPropertyEditorControl(node.ConfigInstance);
            
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
        /// Clear property display
        /// </summary>
        private void ClearPropertyDisplay()
        {
            PropertyTitle.Text = "选择一个配置查看详情";
            SummaryText1.Text = "";
            
            if (PropertyContainer.Child is IDisposable disposable)
            {
                disposable.Dispose();
            }
            PropertyContainer.Child = null;
        }

        /// <summary>
        /// Filter tree based on search text
        /// </summary>
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchText = textBox.Text?.Trim() ?? "";
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Show all nodes
                    RestoreTreeVisibility();
                }
                else
                {
                    // Filter nodes
                    FilterTree(searchText.ToLower());
                }
            }
        }

        /// <summary>
        /// Filter tree nodes based on search text
        /// </summary>
        private void FilterTree(string searchText)
        {
            foreach (var assemblyNode in _rootNodes)
            {
                bool assemblyMatches = FilterNodeRecursive(assemblyNode, searchText);
                assemblyNode.IsExpanded = assemblyMatches;
            }
        }

        /// <summary>
        /// Recursively filter nodes and return true if any child matches
        /// </summary>
        private bool FilterNodeRecursive(ConfigTreeNode node, string searchText)
        {
            bool matches = node.DisplayName.ToLower().Contains(searchText);

            if (node.Children.Count > 0)
            {
                bool anyChildMatches = false;
                foreach (var child in node.Children)
                {
                    bool childMatches = FilterNodeRecursive(child, searchText);
                    anyChildMatches = anyChildMatches || childMatches;
                }

                if (anyChildMatches)
                {
                    node.IsExpanded = true;
                    return true;
                }
            }

            return matches;
        }

        /// <summary>
        /// Restore all nodes visibility
        /// </summary>
        private void RestoreTreeVisibility()
        {
            foreach (var assemblyNode in _rootNodes)
            {
                RestoreNodeVisibility(assemblyNode);
            }
        }

        /// <summary>
        /// Recursively restore node visibility
        /// </summary>
        private void RestoreNodeVisibility(ConfigTreeNode node)
        {
            // Keep assembly nodes expanded, collapse namespace nodes by default
            if (node.NodeType == ConfigNodeType.Assembly)
                node.IsExpanded = true;
            else if (node.NodeType == ConfigNodeType.Namespace)
                node.IsExpanded = false;

            foreach (var child in node.Children)
            {
                RestoreNodeVisibility(child);
            }
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
    }
}
