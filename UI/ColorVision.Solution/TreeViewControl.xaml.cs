#pragma warning disable CA1868
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
            if (TryGetDropPaths(e.Data, out var paths, out bool isInternalDrag))
            {
                if (!isInternalDrag && TryOpenDroppedSolution(paths))
                {
                    e.Handled = true;
                    return;
                }

                string? targetDir = GetDropTargetDirectory(e);
                if (targetDir == null)
                    return;

                bool isMove = isInternalDrag
                    ? !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                CopyOrMovePaths(paths, targetDir, isMove);
                e.Handled = true;
            }
        }

        private void TreeViewControl_DragOver(object sender, DragEventArgs e)
        {
            if (TryGetDropPaths(e.Data, out _, out bool isInternalDrag))
            {
                bool isMove = isInternalDrag
                    ? !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                e.Effects = isMove ? DragDropEffects.Move : DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllowDrop = true;
            DragOver += TreeViewControl_DragOver;
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

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (e.LeftButton != MouseButtonState.Pressed || _selectedNodes.Count == 0)
                return;

            Point currentPoint = e.GetPosition(SolutionTreeView);
            if (Math.Abs(currentPoint.X - SelectPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - SelectPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var paths = _selectedNodes
                .Where(node => !string.IsNullOrWhiteSpace(node.FullPath) && (File.Exists(node.FullPath) || Directory.Exists(node.FullPath)))
                .Select(node => node.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
                return;

            DragDrop.DoDragDrop(SolutionTreeView, CreatePathDataObject(paths, isCut: false), DragDropEffects.Copy | DragDropEffects.Move);
        }

        private static bool TryGetDropPaths(IDataObject dataObject, out string[] paths, out bool isInternalDrag)
        {
            paths = Array.Empty<string>();
            isInternalDrag = dataObject.GetDataPresent(ClipboardFormat);

            if (dataObject.GetDataPresent(DataFormats.FileDrop) &&
                dataObject.GetData(DataFormats.FileDrop) is string[] fileDropPaths)
            {
                paths = fileDropPaths
                    .Where(path => File.Exists(path) || Directory.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return paths.Length > 0;
        }

        private static bool TryOpenDroppedSolution(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".cvproj", StringComparison.OrdinalIgnoreCase))
                {
                    SolutionManager.OpenSolution(path);
                    return true;
                }

                if (!Directory.Exists(path))
                    continue;

                foreach (string projectFile in Directory.EnumerateFiles(path, "*.cvproj", SearchOption.TopDirectoryOnly))
                {
                    SolutionManager.OpenSolution(projectFile);
                    return true;
                }
            }

            return false;
        }

        private string? GetDropTargetDirectory(DragEventArgs e)
        {
            var node = GetNodeAtPoint(e.GetPosition(SolutionTreeView));
            if (node is FileNode)
                return Path.GetDirectoryName(node.FullPath);

            if (node != null && Directory.Exists(node.FullPath))
                return node.FullPath;

            return SolutionManager.CurrentSolutionExplorer?.DirectoryInfo?.FullName;
        }

        private SolutionNode? GetNodeAtPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(SolutionTreeView, point);
            if (result == null)
                return null;

            TreeViewItem? item = ViewHelper.FindVisualParent<TreeViewItem>(result.VisualHit);
            return item?.DataContext as SolutionNode;
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
                var currentExplorer = SolutionManager.GetInstance().CurrentSolutionExplorer;
                List<SolutionNode> filteredResults;
                if (currentExplorer?.Cache?.HasCache() == true)
                {
                    filteredResults = currentExplorer.Cache.Search(keywords)
                        .Select(CreateSearchResultNode)
                        .Where(node => node != null)
                        .Cast<SolutionNode>()
                        .ToList();
                }
                else
                {
                    filteredResults = SolutionManager.GetInstance().SolutionExplorers.
                        SelectMany(explorer => explorer.VisualChildren.GetAllVisualChildren())
                        .OfType<SolutionNode>()
                        .Where(template => keywords.All(keyword =>
                            template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            ))
                        .ToList();
                }

                SolutionTreeView.ItemsSource = filteredResults;
            }
        }

        private static SolutionNode? CreateSearchResultNode(FileTreeCacheEntry entry)
        {
            try
            {
                if (entry.IsDirectory)
                {
                    if (!Directory.Exists(entry.FullPath))
                        return null;
                    return SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(entry.FullPath));
                }

                if (!File.Exists(entry.FullPath))
                    return null;
                return SolutionNodeFactory.CreateFileNode(new FileInfo(entry.FullPath));
            }
            catch
            {
                return null;
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
