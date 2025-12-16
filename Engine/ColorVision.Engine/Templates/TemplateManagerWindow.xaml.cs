using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.Engine.Templates
{

    public class MenuTemplateManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => ColorVision.Engine.Properties.Resources.Settings;

        public override int Order => 999999;
        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE713", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }

        public override void Execute()
        {
            new TemplateManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// Template group by namespace
    /// </summary>
    public class TemplateGroup : ViewModelBase
    {
        public string Namespace
        {
            get => _namespace;
            set { _namespace = value; OnPropertyChanged(); }
        }
        private string _namespace;

        public List<TemplateItem> Templates { get; set; } = new List<TemplateItem>();
    }

    /// <summary>
    /// Individual template item
    /// </summary>
    public class TemplateItem : ViewModelBase
    {
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        private string _title;

        public string Code { get; set; }
        public int TemplateDicId { get; set; }
        public ITemplate Template { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        private bool _isSelected;
    }

    /// <summary>
    /// TemplateManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateManagerWindow : Window
    {
        public TemplateManagerWindow()
        {
            InitializeComponent();
        }

        private List<TemplateGroup> _templateGroups = new List<TemplateGroup>();
        private List<TemplateItem> _allTemplates = new List<TemplateItem>();
        private Dictionary<string, FrameworkElement> _groupSectionElements = new Dictionary<string, FrameworkElement>();
        private TemplateItem _selectedTemplate = null;
        private int _templateColumns = 3;
        private FrameworkElement _currentEditorContent = null;

        public int TemplateColumns
        {
            get => _templateColumns;
            set
            {
                if (_templateColumns != value && value > 0 && value <= 6)
                {
                    _templateColumns = value;
                    RebuildTemplateGrid();
                }
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildTemplateData();
            RebuildTemplateGrid();

            // 显示汇总信息
            int totalTemplates = _allTemplates.Count;
            int totalGroups = _templateGroups.Count;
            SummaryText.Text = $"共计 {totalGroups} 个命名空间分组，{totalTemplates} 个模板类型";
        }

        /// <summary>
        /// Build template data and group by parent namespace
        /// </summary>
        private void BuildTemplateData()
        {
            var templates = TemplateControl.ITemplateNames.ToList();
            
            // Group by parent namespace
            var namespaceGroups = templates
                .GroupBy(kvp => GetParentNamespace(kvp.Value.GetType()))
                .OrderBy(g => g.Key);

            foreach (var namespaceGroup in namespaceGroups)
            {
                var group = new TemplateGroup
                {
                    Namespace = namespaceGroup.Key
                };

                foreach (var template in namespaceGroup.OrderBy(t => t.Value.Title ?? t.Key))
                {
                    var templateItem = new TemplateItem
                    {
                        Title = template.Value.Title ?? template.Key,
                        Code = template.Value.Code ?? template.Key,
                        TemplateDicId = template.Value.TemplateDicId,
                        Template = template.Value
                    };
                    group.Templates.Add(templateItem);
                    _allTemplates.Add(templateItem);
                }

                _templateGroups.Add(group);
            }
        }

        /// <summary>
        /// Get parent namespace for grouping
        /// Example: ColorVision.Engine.Templates.Jsons.KB -> ColorVision.Engine.Templates.Jsons
        /// Example: ColorVision.Engine.Templates.SFR -> ColorVision.Engine.Templates
        /// </summary>
        private string GetParentNamespace(Type type)
        {
            string fullNamespace = type.Namespace ?? "Other";
            
            // Split by dots
            var parts = fullNamespace.Split('.');
            
            // If it's a deep namespace (more than base), take parent
            // Base is ColorVision.Engine.Templates
            const string baseNamespace = "ColorVision.Engine.Templates";
            
            if (fullNamespace.StartsWith(baseNamespace))
            {
                // Remove base namespace
                string relative = fullNamespace.Substring(baseNamespace.Length).TrimStart('.');
                
                if (string.IsNullOrEmpty(relative))
                {
                    // Already at base level
                    return baseNamespace;
                }
                
                // Get first segment after base
                var segments = relative.Split('.');
                if (segments.Length > 0)
                {
                    return $"{baseNamespace}.{segments[0]}";
                }
            }
            
            // Fallback to full namespace
            return fullNamespace;
        }

        /// <summary>
        /// Rebuild the template grid in the middle panel
        /// </summary>
        private void RebuildTemplateGrid(string searchText = null)
        {
            TemplateContainer.Children.Clear();
            _groupSectionElements.Clear();

            var groupsToShow = _templateGroups.AsEnumerable();
            
            // Filter by search if provided
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                groupsToShow = _templateGroups
                    .Select(g => new TemplateGroup
                    {
                        Namespace = g.Namespace,
                        Templates = g.Templates.Where(t => 
                            t.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) || 
                            t.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                            t.TemplateDicId.ToString().Contains(searchText, StringComparison.Ordinal))
                            .ToList()
                    })
                    .Where(g => g.Templates.Any());
            }

            foreach (var group in groupsToShow)
            {
                if (group.Templates.Count == 0) continue;

                // Namespace header
                var headerBorder = new Border
                {
                    Background = (Brush)Application.Current.FindResource("SecondaryRegionBrush"),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, TemplateContainer.Children.Count > 0 ? 20 : 0, 0, 10)
                };

                // Display only the last segment of the namespace for cleaner look
                var displayName = group.Namespace;
                if (displayName.Contains('.'))
                {
                    var parts = displayName.Split('.');
                    displayName = parts[parts.Length - 1]; // Get last segment
                }

                var headerText = new TextBlock
                {
                    Text = displayName,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush")
                };
                headerBorder.Child = headerText;
                TemplateContainer.Children.Add(headerBorder);

                // Store reference for scrolling
                _groupSectionElements[group.Namespace] = headerBorder;

                // Template grid (dynamic columns)
                var uniformGrid = new UniformGrid
                {
                    Columns = _templateColumns,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                foreach (var template in group.Templates)
                {
                    var templateButton = CreateTemplateButton(template);
                    uniformGrid.Children.Add(templateButton);
                }

                TemplateContainer.Children.Add(uniformGrid);
            }
        }

        /// <summary>
        /// Create a button for a template item
        /// </summary>
        private Button CreateTemplateButton(TemplateItem template)
        {
            var button = new Button
            {
                Margin = new Thickness(5),
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.FindResource("GlobalBackground"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Tag = template
            };

            var stackPanel = new StackPanel();
            
            // Primary: Title
            var titleText = new TextBlock
            {
                Text = template.Title,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(titleText);

            // Secondary: Code and TemplateDicId
            var detailsText = new TextBlock
            {
                Text = $"Code: {template.Code} | ID: {template.TemplateDicId}",
                FontSize = 10,
                Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 3, 0, 0),
                Opacity = 0.7
            };
            stackPanel.Children.Add(detailsText);

            button.Content = stackPanel;
            button.Click += TemplateButton_Click;

            // Update selection visual state
            UpdateButtonSelectionState(button, template.IsSelected);

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
        /// Handle template button click
        /// </summary>
        private void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TemplateItem template)
            {
                // Deselect previous template
                if (_selectedTemplate != null && _selectedTemplate != template)
                {
                    _selectedTemplate.IsSelected = false;
                }

                // Select current template
                template.IsSelected = true;
                _selectedTemplate = template;

                // Update all button visual states
                UpdateAllButtonStates();

                DisplayTemplateEditor(template);
            }
        }

        /// <summary>
        /// Update all button selection states
        /// </summary>
        private void UpdateAllButtonStates()
        {
            foreach (var child in TemplateContainer.Children)
            {
                if (child is UniformGrid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Button btn && btn.Tag is TemplateItem tmpl)
                        {
                            UpdateButtonSelectionState(btn, tmpl.IsSelected);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Display embedded template editor for selected template
        /// </summary>
        private void DisplayTemplateEditor(TemplateItem template)
        {
            if (template.Template == null) return;

            EditorTitle.Text = $"模板编辑: {template.Title}";
            SummaryText1.Text = $"当前选择: {template.Title} (共 {template.Template.Count} 个模板)";

            // Remove old editor if exists
            if (_currentEditorContent != null)
            {
                EditorContainer.Child = null;
                _currentEditorContent = null;
            }

            // Hide placeholder
            PlaceholderText.Visibility = Visibility.Collapsed;

            // Load the template to ensure data is fresh
            template.Template.Load();

            // Create embedded editor content (simplified version of TemplateEditorWindow)
            var editorControl = CreateEmbeddedEditor(template.Template);
            
            if (editorControl == null)
            {
                EditorContainer.Child = new TextBlock
                {
                    Text = "无法加载模板编辑器",
                    Margin = new Thickness(10),
                    Foreground = (Brush)Application.Current.FindResource("GlobalTextBrush")
                };
                return;
            }

            _currentEditorContent = editorControl;
            EditorContainer.Child = editorControl;
        }

        /// <summary>
        /// Create embedded editor content using EmbeddedTemplateEditor UserControl
        /// </summary>
        private FrameworkElement CreateEmbeddedEditor(ITemplate template)
        {
            var embeddedEditor = new EmbeddedTemplateEditor
            {
                OwnerWindow = this
            };
            
            embeddedEditor.SetTemplate(template);
            
            return embeddedEditor;
        }

        /// <summary>
        /// Clear editor display
        /// </summary>
        private void ClearEditorDisplay()
        {
            EditorTitle.Text = "选择一个模板查看详情";
            
            if (_currentEditorContent != null)
            {
                EditorContainer.Child = null;
                _currentEditorContent = null;
            }
            
            PlaceholderText.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handle search text change
        /// </summary>
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchText = textBox.Text?.Trim() ?? "";
                
                // Check if selected template will be filtered out
                if (!string.IsNullOrWhiteSpace(searchText) && _selectedTemplate != null)
                {
                    bool selectedTemplateMatches = 
                        _selectedTemplate.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) || 
                        _selectedTemplate.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        _selectedTemplate.TemplateDicId.ToString().Contains(searchText, StringComparison.Ordinal);
                    
                    if (!selectedTemplateMatches)
                    {
                        // Clear selection and right panel
                        _selectedTemplate.IsSelected = false;
                        _selectedTemplate = null;
                        ClearEditorDisplay();
                    }
                }
                
                RebuildTemplateGrid(string.IsNullOrWhiteSpace(searchText) ? null : searchText);
            }
        }

        private void ColumnCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                TemplateColumns = comboBox.SelectedIndex + 1; // Index 0 = 1 column, etc.
            }
        }
    }
}
