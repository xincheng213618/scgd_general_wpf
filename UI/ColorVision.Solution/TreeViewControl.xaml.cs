#pragma warning disable CA1868
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
        private const string SolutionOrganizationDragFormat = "ColorVision.Solution.OrganizationNodes";

        public static SolutionManager SolutionManager => SolutionManager.GetInstance();

        private readonly SolutionContextMenuService _contextMenuService;
        private readonly SolutionSelectionService _selectionService;
        private readonly DispatcherTimer _workspaceStateSaveTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private SolutionExplorer? _observedExplorer;
        private readonly List<SolutionSearchResultNode> _searchResultNodes = new();
        private SolutionNode? _dropTargetNode;
        private CancellationTokenSource? _workspaceStateRestoreCancellation;
        private CancellationTokenSource? _searchCancellation;
        private CancellationTokenSource? _revealCancellation;
        private int _searchVersion;
        private bool _isDragging;
        private bool _isRestoringWorkspaceState;
        private bool _isClearingSearchForReveal;
        private bool _allowProgrammaticBringIntoView;
        private bool _suppressTreeSelectionSynchronization;

        public IReadOnlyList<SolutionNode> SelectedNodes => _selectionService.CommandNodes;

        public TreeViewControl()
        {
            _selectionService = new SolutionSelectionService();
            _contextMenuService = new SolutionContextMenuService();
            _workspaceStateSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350),
            };
            _workspaceStateSaveTimer.Tick += WorkspaceStateSaveTimer_Tick;
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(220),
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            _selectionService.SelectionChanged += SelectionService_SelectionChanged;
            InitializeComponent();
            _contextMenuService.GetSelectedNodes = () => _selectionService.SelectedNodes;
            _contextMenuService.CommandTarget = SolutionTreeView;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionManager;
            SolutionTreeView.ItemsSource = GetActiveWorkspaceItems();
            IniCommand();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllowDrop = true;
            DragOver -= TreeViewControl_DragOver;
            Drop -= TreeViewControl_Drop;
            DragLeave -= TreeViewControl_DragLeave;
            DragOver += TreeViewControl_DragOver;
            Drop += TreeViewControl_Drop;
            DragLeave += TreeViewControl_DragLeave;

            // Use the shared context menu service instead of per-node ContextMenu
            SolutionTreeView.ContextMenu = _contextMenuService.ContextMenu;
            SolutionManager.CurrentWorkspaceChanged -= SolutionManager_CurrentWorkspaceChanged;
            SolutionManager.CurrentWorkspaceChanged += SolutionManager_CurrentWorkspaceChanged;
            SynchronizeObservedExplorer();
            Dispatcher.BeginInvoke(
                () => RestoreWorkspaceState(SolutionManager.CurrentSolutionExplorer),
                DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveWorkspaceState(SolutionManager.CurrentSolutionExplorer);
            CancelWorkspaceStateRestore();
            _workspaceStateSaveTimer.Stop();
            _searchDebounceTimer.Stop();
            CancelPendingSearch();
            CancelPendingReveal();
            DisposeSearchResultNodes();
            SolutionManager.CurrentWorkspaceChanged -= SolutionManager_CurrentWorkspaceChanged;
            if (_observedExplorer != null)
                DetachExplorer(_observedExplorer);
            _observedExplorer = null;
            DragOver -= TreeViewControl_DragOver;
            Drop -= TreeViewControl_Drop;
            DragLeave -= TreeViewControl_DragLeave;
            ClearDropTargetVisual();
        }

        private void SolutionManager_CurrentWorkspaceChanged(object? sender, EventArgs e)
        {
            _workspaceStateSaveTimer.Stop();
            CancelWorkspaceStateRestore();
            CancelPendingReveal();
            _isRestoringWorkspaceState = true;
            try
            {
                _selectionService.Clear();
            }
            finally
            {
                _isRestoringWorkspaceState = false;
            }

            SynchronizeObservedExplorer();
            SolutionTreeView.ItemsSource = GetActiveWorkspaceItems();
            Dispatcher.BeginInvoke(
                () => RestoreWorkspaceState(SolutionManager.CurrentSolutionExplorer),
                DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void SynchronizeObservedExplorer()
        {
            SolutionExplorer? current = SolutionManager.CurrentSolutionExplorer;
            if (ReferenceEquals(_observedExplorer, current))
                return;
            if (_observedExplorer != null)
                DetachExplorer(_observedExplorer);
            _observedExplorer = current;
            if (current != null)
                AttachExplorer(current);
        }

        private static IReadOnlyList<SolutionExplorer> GetActiveWorkspaceItems()
        {
            return SolutionManager.CurrentSolutionExplorer is { } explorer
                ? [explorer]
                : [];
        }

        private void AttachExplorer(SolutionExplorer explorer)
        {
            explorer.VisualChildrenEventHandler += Explorer_VisualChildrenChanged;
            explorer.SolutionStateReloading += Explorer_SolutionStateReloading;
            explorer.SolutionStateReloaded += Explorer_SolutionStateReloaded;
            explorer.Disposing += Explorer_Disposing;
        }

        private void DetachExplorer(SolutionExplorer explorer)
        {
            explorer.VisualChildrenEventHandler -= Explorer_VisualChildrenChanged;
            explorer.SolutionStateReloading -= Explorer_SolutionStateReloading;
            explorer.SolutionStateReloaded -= Explorer_SolutionStateReloaded;
            explorer.Disposing -= Explorer_Disposing;
        }

        private void Explorer_VisualChildrenChanged(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void Explorer_SolutionStateReloading(object? sender, EventArgs e)
        {
            if (sender is SolutionExplorer explorer
                && ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                _workspaceStateSaveTimer.Stop();
                SaveWorkspaceState(explorer);
                CancelWorkspaceStateRestore();
                CancelPendingReveal();
            }
        }

        private void Explorer_SolutionStateReloaded(object? sender, EventArgs e)
        {
            if (sender is not SolutionExplorer explorer
                || !ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                return;
            }

            _workspaceStateSaveTimer.Stop();
            Dispatcher.BeginInvoke(() => RestoreWorkspaceState(explorer), DispatcherPriority.Loaded);
            if (!string.IsNullOrWhiteSpace(SearchBar1.Text))
                SearchBar1TextChanged();
        }

        private void Explorer_Disposing(object? sender, EventArgs e)
        {
            if (sender is SolutionExplorer explorer
                && ReferenceEquals(explorer, SolutionManager.CurrentSolutionExplorer))
            {
                _workspaceStateSaveTimer.Stop();
                SaveWorkspaceState(explorer);
                CancelWorkspaceStateRestore();
                CancelPendingReveal();
            }
        }

        private void SelectionService_SelectionChanged(object? sender, EventArgs e)
        {
            ScheduleWorkspaceStateSave();
            CommandManager.InvalidateRequerySuggested();
        }

        private void SolutionTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!_contextMenuService.PrepareMenu())
                e.Handled = true;
        }

        private void SolutionTreeView_SelectedItemChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressTreeSelectionSynchronization
                || e.NewValue is not SolutionNode node)
            {
                return;
            }
            if (_selectionService.SelectedNodes is [var selected]
                && ReferenceEquals(selected, node))
            {
                return;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                _selectionService.SelectRange(
                    GetVisibleNodes(),
                    node,
                    additive: modifiers.HasFlag(ModifierKeys.Control));
            }
            else
            {
                _selectionService.SelectSingle(node);
            }
        }

        private void SolutionTreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox
                || e.Key != Key.Space
                || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                return;
            }

            SolutionNode? currentNode = GetCurrentTreeNode();
            if (currentNode == null)
                return;

            _selectionService.Toggle(currentNode);
            e.Handled = true;
        }

        private void TreeViewItem_ExpansionChanged(object sender, RoutedEventArgs e)
        {
            ScheduleWorkspaceStateSave();
        }

        private void ClearSelection()
        {
            _selectionService.Clear();
            ClearTreeViewSelection();
        }

        private List<SolutionNode> GetVisibleNodes()
        {
            var nodes = new List<SolutionNode>();
            foreach (SolutionNode rootNode in SolutionTreeView.Items.OfType<SolutionNode>())
                AppendVisibleNodes(rootNode, nodes);
            return nodes;
        }

        private static void AppendVisibleNodes(SolutionNode node, List<SolutionNode> nodes)
        {
            nodes.Add(node);
            if (!node.IsExpanded)
                return;

            foreach (SolutionNode child in node.VisualChildren)
                AppendVisibleNodes(child, nodes);
        }

        /// <summary>
        /// Clear TreeView's internal IsSelected state to prevent visual conflicts.
        /// We use IsMultiSelected exclusively for selection visuals.
        /// </summary>
        private void ClearTreeViewSelection()
        {
            if (SolutionTreeView.SelectedItem is not SolutionNode selected)
                return;

            _suppressTreeSelectionSynchronization = true;
            try
            {
                selected.IsSelected = false;
            }
            finally
            {
                _suppressTreeSelectionSynchronization = false;
            }
        }

    }
}
