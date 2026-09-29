using ColorVision.Engine.MQTT;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.RC;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal sealed class FlowNodeContextMenuService : IDisposable
    {
        private sealed class NodeMenuCatalogEntry
        {
            public Type NodeType { get; }
            public string[] PathSegments { get; }
            public int OrderedSegmentIndex { get; }
            public int CategoryOrder { get; }

            public NodeMenuCatalogEntry(Type nodeType, string[] pathSegments, int orderedSegmentIndex, int categoryOrder)
            {
                NodeType = nodeType;
                PathSegments = pathSegments;
                OrderedSegmentIndex = orderedSegmentIndex;
                CategoryOrder = categoryOrder;
            }
        }

        private sealed class NodeMenuItemEntry
        {
            public Type NodeType { get; }
            public string Header { get; }

            public NodeMenuItemEntry(Type nodeType, string header)
            {
                NodeType = nodeType;
                Header = header;
            }
        }

        private sealed class NodeMenuCategory
        {
            private readonly Dictionary<string, NodeMenuCategory> categories = new(StringComparer.Ordinal);

            public int Order { get; private set; }
            public string SortText { get; }
            public string Header { get; }
            public IEnumerable<NodeMenuCategory> Categories => categories.Values;
            public List<NodeMenuItemEntry> Nodes { get; } = new();

            public NodeMenuCategory(int order = int.MaxValue, string sortText = "", string header = "")
            {
                Order = order;
                SortText = sortText;
                Header = header;
            }

            public NodeMenuCategory GetOrAddCategory(string rawSegment, int order)
            {
                if (categories.TryGetValue(rawSegment, out NodeMenuCategory? category))
                {
                    category.Order = Math.Min(category.Order, order);
                    return category;
                }

                string localized = LocalizeNodeMenuText(rawSegment);
                category = new NodeMenuCategory(order, localized, localized);
                categories.Add(rawSegment, category);
                return category;
            }
        }

        private static readonly HashSet<string> CoreNodeMenuAssemblies = new(StringComparer.Ordinal)
        {
            "FlowEngineLib",
            "ColorVision.Engine",
        };

        private static readonly object NodeMenuCacheLock = new();
        private static readonly Dictionary<Type, NodeMenuCatalogEntry?> NodeMenuCatalogCache = new();
        private static readonly Dictionary<(Type Type, string Culture), string?> NodeTitleCache = new();

        private readonly STNodeEditor _nodeEditor;
        private readonly FlowExecutionNavigator _executionNavigator;
        private readonly Action _importModule;
        private readonly ContextMenu _contextMenu;
        private System.Drawing.Point _contextCanvasPoint;

        public FlowNodeContextMenuService(
            STNodeEditor nodeEditor,
            FlowExecutionNavigator executionNavigator,
            Action importModule)
        {
            _nodeEditor = nodeEditor;
            _executionNavigator = executionNavigator;
            _importModule = importModule;
            _contextMenu = new ContextMenu();
            _nodeEditor.ContextMenu = _contextMenu;
            _nodeEditor.ContextMenuOpening += NodeEditor_ContextMenuOpening;
        }

        internal static string LocalizeNodeMenuPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string[] segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int startIndex = segments.Length > 0 && CoreNodeMenuAssemblies.Contains(segments[0]) ? 1 : 0;
            int orderedSegmentIndex = startIndex == 1 || segments.Length == 1 ? startIndex : 1;
            var displaySegments = new List<string>(Math.Max(segments.Length - startIndex, 0));
            for (int index = startIndex; index < segments.Length; index++)
            {
                string semanticSegment = index == orderedSegmentIndex ? RemoveSortPrefix(segments[index]) : segments[index];
                displaySegments.Add(LocalizeNodeMenuText(semanticSegment));
            }
            return string.Join("/", displaySegments);
        }

        internal static IReadOnlyDictionary<Type, string> GetNodeCreationMenuPaths()
        {
            return GetNodeMenuCatalogEntries().ToDictionary(
                entry => entry.NodeType,
                entry => LocalizeNodeMenuPath(string.Join("/", entry.PathSegments)));
        }

        internal static IReadOnlyList<string> GetNodeCreationMenuRootHeaders()
        {
            NodeMenuCategory root = BuildNodeMenuCatalog();
            return root.Categories
                .OrderBy(item => item.Order)
                .ThenBy(item => item.SortText, LogicalStringComparer.Instance)
                .Select(item => item.Header)
                .ToArray();
        }

        internal static MenuItem CreateImportModuleMenuItem(Action importModule)
        {
            ArgumentNullException.ThrowIfNull(importModule);

            var item = new MenuItem { Header = Properties.Resources.Flow_ImportTemplateAsModule };
            item.Click += (_, _) => importModule();
            return item;
        }

        private static string LocalizeNodeMenuText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string? localized = ST.Library.UI.Lang.Get(text);
            if (IsValidLocalizedMenuText(text, localized))
                return localized!;

            localized = ST.Library.UI.Properties.Resources.ResourceManager.GetString(text, CultureInfo.CurrentUICulture);
            return IsValidLocalizedMenuText(text, localized) ? localized! : text;
        }

        private static bool IsValidLocalizedMenuText(string key, string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, $"[{key}]", StringComparison.Ordinal);
        }

        private static string RemoveSortPrefix(string text)
        {
            int index = 0;
            while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '_'))
                index++;
            if (index == 0 || index >= text.Length || !char.IsWhiteSpace(text[index]))
                return text;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return text[index..];
        }

        private static int ResolveCategoryOrder(string segment, int configuredOrder)
        {
            if (configuredOrder != int.MaxValue)
                return configuredOrder;

            string text = segment.Trim();
            int index = 0;
            while (index < text.Length && char.IsDigit(text[index]))
                index++;
            if (index == 0 || index >= text.Length)
                return int.MaxValue;

            if (!int.TryParse(text[..index], out int major))
                return int.MaxValue;

            int minor = 0;
            if (text[index] == '_')
            {
                int minorStart = ++index;
                while (index < text.Length && char.IsDigit(text[index]))
                    index++;
                if (minorStart == index || !int.TryParse(text[minorStart..index], out minor))
                    return int.MaxValue;
            }

            if (index >= text.Length || !char.IsWhiteSpace(text[index]))
                return int.MaxValue;

            long order = (long)major * 100 + (long)minor * 10;
            return order <= int.MaxValue ? (int)order : int.MaxValue;
        }

        private void NodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var mousePosition = Mouse.GetPosition(_nodeEditor);
            var clientPoint = new System.Drawing.Point(
                (int)Math.Round(mousePosition.X),
                (int)Math.Round(mousePosition.Y));
            _contextCanvasPoint = _nodeEditor.ControlToCanvas(clientPoint);
            NodeFindInfo findInfo = _nodeEditor.FindNodeFromPoint(_contextCanvasPoint);
            _contextMenu.Items.Clear();

            if (findInfo.NodeOption != null)
            {
                e.Handled = true;
                return;
            }

            if (findInfo.Node != null)
            {
                AddNodeMenuItems(_contextMenu.Items, findInfo.Node);
            }
            else
            {
                AddNodeCreationMenuItems(_contextMenu.Items);
                AddImportModuleContextMenu(_contextMenu.Items);
            }

            if (_contextMenu.Items.Count == 0)
                e.Handled = true;
        }

        private void AddNodeMenuItems(ItemCollection items, STNode node)
        {
            var copyItem = new MenuItem { Header = Properties.Resources.Copy };
            copyItem.Click += (_, _) => CopyNode(node);
            items.Add(copyItem);

            var deleteItem = new MenuItem { Header = Properties.Resources.Delete };
            deleteItem.Click += (_, _) => _nodeEditor.Nodes.Remove(node);
            items.Add(deleteItem);

            if (node is CVCommonNode commonNode)
            {
                var historyItem = new MenuItem { Header = Properties.Resources.Flow_NodeExecutionDetails };
                historyItem.Click += (_, _) => _executionNavigator.OpenNodeExecutionDetails(commonNode);
                items.Add(historyItem);
            }

            if (node is LocalCalibrationNodeBase)
            {
                var cacheManagerItem = new MenuItem { Header = LocalizeNodeMenuText("本地校正缓存管理") };
                cacheManagerItem.Click += (_, _) => LocalCalibrationCacheManagerWindow.OpenWindow();
                items.Add(cacheManagerItem);
            }

            items.Add(new Separator());

            var lockOptionItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockOption)),
                IsCheckable = true,
                IsChecked = node.LockOption
            };
            lockOptionItem.Click += (_, _) => _nodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockOption)),
                () => node.LockOption = !node.LockOption);
            items.Add(lockOptionItem);

            var lockLocationItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                IsCheckable = true,
                IsChecked = node.LockLocation
            };
            lockLocationItem.Click += (_, _) => _nodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                () => node.LockLocation = !node.LockLocation);
            items.Add(lockLocationItem);
        }

        private void CopyNode(STNode node)
        {
            byte[] data = _nodeEditor.GetNodesData(new[] { node });
            _nodeEditor.ImportSelectionData(data, new System.Drawing.Point(node.Left + 30, node.Top + 30));
        }

        private void AddNodeCreationMenuItems(ItemCollection items)
        {
            NodeMenuCategory root = BuildNodeMenuCatalog();
            AddNodeMenuCategories(items, root);
        }

        private static IReadOnlyList<NodeMenuCatalogEntry> GetNodeMenuCatalogEntries()
        {
            Type[] registeredTypes = STNodeTypeRegistry.GetTypes();
            lock (NodeMenuCacheLock)
            {
                foreach (Type type in registeredTypes)
                {
                    if (NodeMenuCatalogCache.ContainsKey(type))
                        continue;

                    NodeMenuCatalogCache[type] = CreateNodeMenuCatalogEntry(type);
                }

                return registeredTypes
                    .Select(type => NodeMenuCatalogCache[type])
                    .Where(entry => entry != null)
                    .Cast<NodeMenuCatalogEntry>()
                    .ToArray();
            }
        }

        private static NodeMenuCatalogEntry? CreateNodeMenuCatalogEntry(Type type)
        {
            if (!type.IsSubclassOf(typeof(STNode))
                || type.IsAbstract
                || type.IsDefined(typeof(ObsoleteAttribute), inherit: false))
                return null;

            STNodeAttribute? attribute = type.GetCustomAttribute<STNodeAttribute>(inherit: true);
            if (attribute == null)
                return null;

            string assemblyName = type.Assembly.GetName().Name ?? "Unknown";
            string[] declaredSegments = attribute.Path?
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            int categoryOrder = attribute.CategoryOrder;
            if (declaredSegments.Length > 0)
            {
                categoryOrder = ResolveCategoryOrder(declaredSegments[0], categoryOrder);
                declaredSegments[0] = RemoveSortPrefix(declaredSegments[0]);
            }
            bool isCoreAssembly = CoreNodeMenuAssemblies.Contains(assemblyName);
            string[] pathSegments = isCoreAssembly
                ? declaredSegments.Length > 0 ? declaredSegments : new[] { assemblyName }
                : new[] { assemblyName }.Concat(declaredSegments).ToArray();
            int orderedSegmentIndex = declaredSegments.Length == 0 ? -1 : isCoreAssembly ? 0 : 1;
            return new NodeMenuCatalogEntry(type, pathSegments, orderedSegmentIndex, categoryOrder);
        }

        private static NodeMenuCategory BuildNodeMenuCatalog()
        {
            var root = new NodeMenuCategory();
            foreach (NodeMenuCatalogEntry entry in GetNodeMenuCatalogEntries())
            {
                if (entry.PathSegments.Length == 0 || !TryGetNodeTitle(entry.NodeType, out string title))
                    continue;

                NodeMenuCategory category = root;
                for (int index = 0; index < entry.PathSegments.Length; index++)
                {
                    int categoryOrder = index == entry.OrderedSegmentIndex ? entry.CategoryOrder : int.MaxValue;
                    category = category.GetOrAddCategory(entry.PathSegments[index], categoryOrder);
                }
                category.Nodes.Add(new NodeMenuItemEntry(entry.NodeType, title));
            }
            return root;
        }

        private static bool TryGetNodeTitle(Type type, out string title)
        {
            var cacheKey = (type, CultureInfo.CurrentUICulture.Name);
            lock (NodeMenuCacheLock)
            {
                if (NodeTitleCache.TryGetValue(cacheKey, out string? cachedTitle))
                {
                    title = cachedTitle ?? string.Empty;
                    return cachedTitle != null;
                }
            }

            string? resolvedTitle = null;
            try
            {
                if (Activator.CreateInstance(type) is STNode previewNode)
                    resolvedTitle = LocalizeNodeMenuText(previewNode.Title);
            }
            catch
            {
            }

            lock (NodeMenuCacheLock)
                NodeTitleCache[cacheKey] = resolvedTitle;
            title = resolvedTitle ?? string.Empty;
            return resolvedTitle != null;
        }

        private void AddNodeMenuCategories(ItemCollection items, NodeMenuCategory parent)
        {
            foreach (NodeMenuCategory category in parent.Categories
                .OrderBy(item => item.Order)
                .ThenBy(item => item.SortText, LogicalStringComparer.Instance))
            {
                var categoryItem = new MenuItem { Header = category.Header };
                AddNodeMenuCategories(categoryItem.Items, category);
                if (categoryItem.Items.Count > 0)
                    items.Add(categoryItem);
            }

            foreach (NodeMenuItemEntry node in parent.Nodes
                .OrderBy(item => item.Header, LogicalStringComparer.Instance)
                .ThenBy(item => item.NodeType.FullName, StringComparer.Ordinal))
            {
                Type nodeType = node.NodeType;
                var nodeItem = new MenuItem { Header = node.Header };
                nodeItem.Click += (_, _) => CreateNode(nodeType);
                items.Add(nodeItem);
            }
        }

        private sealed class LogicalStringComparer : IComparer<string>
        {
            public static LogicalStringComparer Instance { get; } = new();

            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                int logicalResult = Common.NativeMethods.Shlwapi.CompareLogical(x, y);
                return logicalResult != 0 ? logicalResult : StringComparer.CurrentCultureIgnoreCase.Compare(x, y);
            }
        }

        private void CreateNode(Type type)
        {
            if (Activator.CreateInstance(type) is not STNode node)
                return;

            node.Create();
            node.Left = _contextCanvasPoint.X;
            node.Top = _contextCanvasPoint.Y;
            CameraNodeCreationDefaults.InitializeFromCurrentCamera(node);

            if (node is CVBaseServerNode serverNode)
            {
                var matchedService = MqttRCService.GetInstance().ServiceTokens
                    .FirstOrDefault(service => service.Devices.Any(device => device.Key == serverNode.DeviceCode));
                if (matchedService != null)
                    serverNode.Token = matchedService.Token;
            }
            else if (node is MQTTStartNode startNode)
            {
                startNode.Server = MQTTControl.Config.Host;
                startNode.Port = MQTTControl.Config.Port;
            }

            _nodeEditor.Nodes.Add(node);
            _nodeEditor.SetActiveNode(node);
        }

        private void AddImportModuleContextMenu(ItemCollection items)
        {
            items.Add(new Separator());
            items.Add(CreateImportModuleMenuItem(_importModule));
        }

        public void Dispose()
        {
            _nodeEditor.ContextMenuOpening -= NodeEditor_ContextMenuOpening;
            if (ReferenceEquals(_nodeEditor.ContextMenu, _contextMenu))
                _nodeEditor.ContextMenu = null;
        }
    }
}
