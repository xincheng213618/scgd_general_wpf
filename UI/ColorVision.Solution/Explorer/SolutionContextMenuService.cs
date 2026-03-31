using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        public SolutionContextMenuService()
        {
            _contextMenu = new ContextMenu();
            _contextMenu.Opened += OnContextMenuOpened;
        }

        /// <summary>
        /// Gets the current selected nodes. Should be set by the TreeView selection logic.
        /// </summary>
        public Func<IReadOnlyList<SolutionNode>>? GetSelectedNodes { get; set; }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            _contextMenu.Items.Clear();

            var nodes = GetSelectedNodes?.Invoke();
            if (nodes == null || nodes.Count == 0)
            {
                e.Handled = true;
                return;
            }

            if (nodes.Count == 1)
            {
                BuildSingleNodeMenu(nodes[0]);
            }
            else
            {
                BuildMultiNodeMenu(nodes);
            }
        }

        private void BuildSingleNodeMenu(SolutionNode node)
        {
            // Collect metadata from the node
            var metadatas = new List<MenuItemMetadata>();
            node.CollectMenuItems(metadatas);

            BuildMenuFromMetadata(metadatas);
        }

        private void BuildMultiNodeMenu(IReadOnlyList<SolutionNode> nodes)
        {
            // For multi-select, only show operations that are valid for all selected nodes
            var metadatasByGuid = new Dictionary<string, MenuItemMetadata>();
            var firstNodeMetadatas = new List<MenuItemMetadata>();
            nodes[0].CollectMenuItems(firstNodeMetadatas);

            foreach (var meta in firstNodeMetadatas)
            {
                metadatasByGuid[meta.GuidId] = meta;
            }

            // Keep only items that appear in ALL selected nodes
            for (int i = 1; i < nodes.Count; i++)
            {
                var nodeMetadatas = new List<MenuItemMetadata>();
                nodes[i].CollectMenuItems(nodeMetadatas);
                var nodeGuids = new HashSet<string>(nodeMetadatas.Select(m => m.GuidId));
                var toRemove = metadatasByGuid.Keys.Where(k => !nodeGuids.Contains(k)).ToList();
                foreach (var key in toRemove)
                    metadatasByGuid.Remove(key);
            }

            // Filter to common operations (Cut/Copy/Paste/Delete work on multi-select)
            var commonMetadatas = metadatasByGuid.Values.ToList();
            BuildMenuFromMetadata(commonMetadatas);
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
                        Visibility = meta.Visibility,
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
                    Icon = meta.Icon,
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
