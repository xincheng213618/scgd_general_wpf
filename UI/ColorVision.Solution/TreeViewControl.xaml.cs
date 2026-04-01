using ColorVision.Solution.Explorer;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution
{
    public class TreeViewItemMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double num = 0.0;
            UIElement uIElement = value as TreeViewItem;
            while (uIElement != null && uIElement.GetType() != typeof(TreeView))
            {
                uIElement = (UIElement)VisualTreeHelper.GetParent(uIElement);
                if (uIElement is TreeViewItem)
                {
                    num += 12.0;
                }
            }

            return new Thickness(num, 0.0, 0.0, 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// TreeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class TreeViewControl : UserControl
    {
        public static SolutionManager SolutionManager => SolutionManager.GetInstance();

        private readonly SolutionContextMenuService _contextMenuService;
        private readonly List<SolutionNode> _selectedNodes = new();

        public IReadOnlyList<SolutionNode> SelectedNodes => _selectedNodes;

        public TreeViewControl()
        {
            _contextMenuService = new SolutionContextMenuService();
            _contextMenuService.GetSelectedNodes = () => _selectedNodes;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionManager;
            SolutionTreeView.ItemsSource = SolutionManager.SolutionExplorers;
            if (SolutionManager.SolutionExplorers.Count > 0)
                SolutionManager.SolutionExplorers[0].VisualChildrenEventHandler += (s, e) => SearchBar1TextChanged();

            IniCommand();
        }

        private void TreeViewControl_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                {
                    var destDir = SolutionManager.SolutionExplorers[0].SolutionEnvironments.SolutionDir;
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    var destFile = Path.Combine(destDir, Path.GetFileName(fn));
                    File.Copy(fn, destFile, overwrite: true);
                    e.Handled = true;

                }
                else if (Directory.Exists(fn))
                {
                    DirectoryInfo directoryInfo = new(fn);
                    foreach (var item in directoryInfo.GetFiles())
                    {
                        if (item.Extension == ".cvproj")
                        {
                            SolutionManager.OpenSolution(item.FullName);
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllowDrop = true;
            Drop += TreeViewControl_Drop;

            // Use the shared context menu service instead of per-node ContextMenu
            SolutionTreeView.ContextMenu = _contextMenuService.ContextMenu;
        }

        private Point SelectPoint;

        private TreeViewItem? SelectedTreeViewItem;

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            SelectPoint = e.GetPosition(SolutionTreeView);
            HitTestResult result = VisualTreeHelper.HitTest(SolutionTreeView, SelectPoint);
            if (result != null)
            {
                TreeViewItem item = ViewHelper.FindVisualParent<TreeViewItem>(result.VisualHit);
                if (item == null)
                    return;

                if (item.DataContext is SolutionNode node)
                {
                    bool isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                    bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    bool isRightClick = e.RightButton == MouseButtonState.Pressed;

                    if (isCtrl)
                    {
                        // Ctrl+Click: toggle selection
                        if (_selectedNodes.Contains(node))
                        {
                            _selectedNodes.Remove(node);
                            node.IsMultiSelected = false;
                        }
                        else
                        {
                            _selectedNodes.Add(node);
                            node.IsMultiSelected = true;
                        }
                        ClearTreeViewSelection();
                        e.Handled = true;
                    }
                    else if (isShift && _selectedNodes.Count > 0)
                    {
                        // Shift+Click: add to selection
                        if (!_selectedNodes.Contains(node))
                            _selectedNodes.Add(node);
                        node.IsMultiSelected = true;
                        ClearTreeViewSelection();
                        e.Handled = true;
                    }
                    else if (isRightClick && _selectedNodes.Contains(node))
                    {
                        // Right-click on already selected node: keep multi-selection
                        ClearTreeViewSelection();
                        e.Handled = true;
                    }
                    else
                    {
                        // Normal click (or right-click on unselected node): single select
                        ClearSelection();
                        _selectedNodes.Add(node);
                        node.IsMultiSelected = true;
                    }

                    // Ensure TreeView has keyboard focus for Ctrl+C/V/X commands
                    if (!SolutionTreeView.IsKeyboardFocusWithin)
                        SolutionTreeView.Focus();
                }

                if (SelectedTreeViewItem != null && SelectedTreeViewItem != item && SelectedTreeViewItem.DataContext is SolutionNode vobj)
                {
                    vobj.IsEditMode = false;
                }
                SelectedTreeViewItem = item;
            }
            else
            {
                ClearSelection();
                SelectedTreeViewItem = null;
            }
        }

        private void ClearSelection()
        {
            foreach (var node in _selectedNodes)
                node.IsMultiSelected = false;
            _selectedNodes.Clear();
        }

        /// <summary>
        /// Clear TreeView's internal IsSelected state to prevent visual conflicts.
        /// We use IsMultiSelected exclusively for selection visuals.
        /// </summary>
        private void ClearTreeViewSelection()
        {
            if (SolutionTreeView.SelectedItem is SolutionNode selected)
                selected.IsSelected = false;
        }

        private readonly char[] Chars = new[] { ' ' };

        public void SearchBar1TextChanged()
        {
            string text = SearchBar1.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SolutionTreeView.ItemsSource = SolutionManager.GetInstance().SolutionExplorers;
            }
            else
            {
                var keywords = text.Split(Chars, StringSplitOptions.RemoveEmptyEntries);
                var filteredResults = SolutionManager.GetInstance().SolutionExplorers.
                    SelectMany(explorer => explorer.VisualChildren.GetAllVisualChildren())
                    .OfType<SolutionNode>()
                    .Where(template => keywords.All(keyword =>
                        template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        ))
                    .ToList();

                SolutionTreeView.ItemsSource = filteredResults;
            }
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchBar1TextChanged();
        }


        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
