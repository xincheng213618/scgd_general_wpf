#pragma warning disable CS8604,CS8620
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Shared ContextMenu builder for the Solution TreeView.
    /// Instead of each SolutionNode owning a ContextMenu instance,
    /// a single ContextMenu is shared and rebuilt when opened based on the selected node(s).
    /// 
    /// Benefits:
    /// - Multi-select: menu reflects intersection of available operations
    /// - Performance: no per-node ContextMenu allocation
    /// - Consistency: centralized menu building logic
    /// </summary>
    public class SolutionContextMenuService
    {
        private readonly ContextMenu _contextMenu;

        public ContextMenu ContextMenu => _contextMenu;

        /// <summary>
        /// Routed commands from a ContextMenu are outside the TreeView visual
        /// tree, so they need an explicit target to reach the command bindings.
        /// </summary>
        public IInputElement? CommandTarget { get; set; }

        public SolutionContextMenuService()
        {
            _contextMenu = new ContextMenu();
        }

        /// <summary>
        /// Gets the current selected nodes. Should be set by the TreeView selection logic.
        /// </summary>
        public Func<IReadOnlyList<SolutionNode>>? GetSelectedNodes { get; set; }

        public bool PrepareMenu()
        {
            _contextMenu.Items.Clear();

            var nodes = GetSelectedNodes?.Invoke();
            if (nodes == null || nodes.Count == 0)
                return false;

            BuildMenuFromMetadata(CreateMenuMetadata(nodes));
            return _contextMenu.Items.Count > 0;
        }

        internal static List<MenuItemMetadata> CreateMenuMetadata(IReadOnlyList<SolutionNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            if (nodes.Count == 0)
                return [];

            List<MenuItemMetadata> metadatas = nodes.Count == 1
                ? BuildSingleNodeMenu(nodes[0])
                : BuildMultiNodeMenu(nodes);
            metadatas.AddRange(SolutionMenuContributionRegistry.GetMenuItems(
                new SolutionMenuContext(nodes)));
            return metadatas;
        }

        private static List<MenuItemMetadata> BuildSingleNodeMenu(SolutionNode node)
        {
            var metadatas = new List<MenuItemMetadata>();
            node.CollectMenuItems(metadatas);
            return metadatas;
        }

        private static List<MenuItemMetadata> BuildMultiNodeMenu(IReadOnlyList<SolutionNode> nodes)
        {
            // For multi-select, only show operations that are valid for all selected nodes
            // Collect all metadata per node, keyed by GuidId
            var allNodeMetadatas = new List<List<MenuItemMetadata>>();
            foreach (var node in nodes)
            {
                var metas = new List<MenuItemMetadata>();
                node.CollectMenuItems(metas);
                allNodeMetadatas.Add(metas);
            }

            // Intersect GuidIds: keep only items present in ALL nodes
            var commonGuids = new HashSet<string>(allNodeMetadatas[0].Select(m => m.GuidId));
            for (int i = 1; i < allNodeMetadatas.Count; i++)
            {
                var nodeGuids = new HashSet<string>(allNodeMetadatas[i].Select(m => m.GuidId));
                commonGuids.IntersectWith(nodeGuids);
            }

            // Multi-selection exposes only commands with explicit batch semantics.
            // Matching GuidIds alone is not enough: Open/Properties/Add may share
            // ids while still being single-target commands.
            var commonMetadatas = new List<MenuItemMetadata>();
            foreach (var meta in allNodeMetadatas[0].Where(m => commonGuids.Contains(m.GuidId)
                && SolutionCommandIds.SupportsMultipleSelection(m.GuidId)
                && m.Command is RoutedCommand))
            {
                commonMetadatas.Add(meta);
            }

            return commonMetadatas;
        }

        private void BuildMenuFromMetadata(List<MenuItemMetadata> metadatas)
        {
            var sorted = metadatas.OrderBy(m => m.Order).ToList();

            void CreateChildren(ItemsControl parent, string ownerGuid)
            {
                var children = sorted.Where(a => a.OwnerGuid == ownerGuid).OrderBy(a => a.Order).ToList();
                for (int i = 0; i < children.Count; i++)
                {
                    var meta = children[i];
                    var menuItem = new MenuItem
                    {
                        Header = meta.Header,
                        Icon = meta.Icon,
                        InputGestureText = meta.InputGestureText,
                        Command = meta.Command,
                        CommandTarget = meta.Command is RoutedCommand ? CommandTarget : null,
                        Visibility = meta.Visibility,
                        IsCheckable = meta.IsChecked.HasValue,
                        IsChecked = meta.IsChecked ?? false,
                    };

                    if (meta.GuidId != null)
                        CreateChildren(menuItem, meta.GuidId);

                    if (i > 0 && meta.Order - children[i - 1].Order > 4 && meta.Visibility == Visibility.Visible)
                        parent.Items.Add(new Separator());

                    parent.Items.Add(menuItem);
                }
            }

            var roots = metadatas
                .Where(m => m.OwnerGuid == MenuItemConstants.Menu && m.Visibility == Visibility.Visible)
                .OrderBy(m => m.Order)
                .ToList();

            for (int i = 0; i < roots.Count; i++)
            {
                var meta = roots[i];
                var menuItem = new MenuItem
                {
                    Header = meta.Header,
                    Command = meta.Command,
                    CommandTarget = meta.Command is RoutedCommand ? CommandTarget : null,
                    Icon = meta.Icon,
                    IsCheckable = meta.IsChecked.HasValue,
                    IsChecked = meta.IsChecked ?? false,
                };

                if (meta.GuidId != null)
                    CreateChildren(menuItem, meta.GuidId);

                if (i > 0 && meta.Order - roots[i - 1].Order > 4)
                    _contextMenu.Items.Add(new Separator());

                _contextMenu.Items.Add(menuItem);
            }
        }
    }
}
