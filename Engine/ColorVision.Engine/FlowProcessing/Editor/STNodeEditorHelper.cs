#pragma warning disable CA1304,CA1822,CA1854,CS8602,CS8603,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Editor
{

    public class STNodeEditorHelper:ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(STNodeEditorHelper));

        public STNodeEditor STNodeEditor { get; set; }

        public StackPanel SignStackPanel { get; set; }
        public System.Windows.Controls.Grid PropertyEditorPanel { get; set; }

        /// <summary>
        /// Whether to use the AvalonDock panel (true) or embedded panel references (false).
        /// </summary>
        public bool UseDockPanel { get; set; }


        public static STNodeTreeView STNodeTreeView { get 
            {
                if (_STNodeTreeView == null)
                {
                    _STNodeTreeView = new STNodeTreeView();
                }
                return _STNodeTreeView;
            }
        }
        private static STNodeTreeView _STNodeTreeView;

        public STNodeEditorHelper(STNodeEditor sTNodeEditor)
        {
            STNodeEditor = sTNodeEditor;
            STNodeEditor.ActiveChanged += STNodeEditorMain_ActiveChanged;
            STNodeEditor.SelectedChanged += STNodeEditorMain_SelectedChanged;
            AddContentMenu();
        }

        public void SelectAll()
        {
            STNodeEditor.SelectAllNodes();
        }

        public void ClearSelection()
        {
            STNodeEditor.SetActiveNode(null);
            foreach (STNode node in STNodeEditor.GetSelectedNode())
            {
                node.SetSelected(bSelected: false, bRedraw: false);
            }
            STNodeEditor.Invalidate();
            HidePropertyEditor();
        }

        public void Copy()
        {
            STNodeEditor.CopySelectionToClipboard();
        }

        public void Paste()
        {
            try
            {
                STNodeEditor.PasteFromClipboard();
            }
            catch (Exception ex)
            {
                log.Error("Paste failed", ex);
            }
        }

        public void ImportCanvasAsModule(byte[] canvasData)
        {
            System.Drawing.Point target;
            if (STNodeEditor.IsMouseOver)
            {
                var mousePosition = Mouse.GetPosition(STNodeEditor);
                target = STNodeEditor.ControlToCanvas(new System.Drawing.Point(
                    (int)Math.Round(mousePosition.X),
                    (int)Math.Round(mousePosition.Y)));
            }
            else
            {
                target = STNodeEditor.ControlToCanvas(new System.Drawing.Point(
                    STNodeEditor.ClientSize.Width / 2,
                    STNodeEditor.ClientSize.Height / 2));
            }
            STNodeEditor.ImportCanvasAsModule(canvasData, target);
        }


        #region Activate
        private void STNodeEditorMain_ActiveChanged(object? sender, EventArgs e)
        {
            RefreshActiveNodePropertyPanel();
        }

        private void STNodeEditorMain_SelectedChanged(object? sender, EventArgs e)
        {
            RefreshActiveNodePropertyPanel();
        }

        internal static bool ShouldShowPropertyEditor(STNodeEditor nodeEditor)
        {
            STNode? activeNode = nodeEditor.ActiveNode;
            if (activeNode == null || !activeNode.IsSelected)
            {
                return false;
            }

            STNode[] selectedNodes = nodeEditor.GetSelectedNode();
            return selectedNodes.Length == 1 && ReferenceEquals(selectedNodes[0], activeNode);
        }

        public void RefreshActiveNodePropertyPanel()
        {
            if (!STNodeEditor.Dispatcher.CheckAccess())
            {
                _ = STNodeEditor.BeginInvoke(new Action(RefreshActiveNodePropertyPanel));
                return;
            }

            StackPanel signPanel;

            if (UseDockPanel)
            {
                var dockPanel = FlowNodePropertyPanel.Instance;
                if (dockPanel == null) return;
                signPanel = dockPanel.SignStackPanel;
            }
            else
            {
                if (SignStackPanel == null || PropertyEditorPanel == null)
                    return;
                signPanel = SignStackPanel;
            }

            signPanel.Children.Clear();

            STNode? activeNode = STNodeEditor.ActiveNode;
            if (!ShouldShowPropertyEditor(STNodeEditor))
            {
                signPanel.Visibility = Visibility.Collapsed;
                if (UseDockPanel)
                {
                    // Don't hide the dock panel — let the user manage its visibility
                }
                else
                {
                    PropertyEditorPanel.Visibility = Visibility.Collapsed;
                }
                return;
            }

            // Show the property editor
            if (UseDockPanel)
            {
                WorkspaceManager.LayoutManager?.ShowPanel(FlowNodePropertyPanel.PanelId);
            }
            else
            {
                PropertyEditorPanel.Visibility = Visibility.Visible;
            }
            var configurator = NodeConfiguratorRegistry.GetConfigurator(activeNode!.GetType());
            if (configurator != null)
            {
                var context = new NodeConfiguratorContext
                {
                    Node = activeNode,
                    SignStackPanel = signPanel,
                    STNodeEditor = STNodeEditor,
                    Refresh = RefreshActiveNodePropertyPanel
                };
                configurator.Configure(context);
            }

            signPanel.Children.Add(StackPanel);
            StackPanel.Children.Clear();

            var resourceManager = PropertyEditorHelper.GetResourceManager(activeNode);
            StackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                activeNode,
                resourceManager,
                metadataProvider: FlowNodePropertyMetadataProvider.Instance,
                advancedOptions: FlowNodePropertyMetadataProvider.AdvancedOptions));
            signPanel.Visibility = signPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        public StackPanel StackPanel { get; set; } = new StackPanel();

        public void HidePropertyEditor()
        {
            if (UseDockPanel)
            {
                // Don't hide the dock panel automatically
            }
            else
            {
                if (PropertyEditorPanel != null)
                    PropertyEditorPanel.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region ContextMenu

        private static readonly string[] CoreNodeMenuAssemblyPrefixes =
        {
            "FlowEngineLib/",
            "ColorVision.Engine/",
        };

        internal static string LocalizeNodeMenuPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string displayPath = path;
            foreach (string prefix in CoreNodeMenuAssemblyPrefixes)
            {
                if (!displayPath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                displayPath = displayPath.Substring(prefix.Length);
                break;
            }

            return string.Join("/", displayPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(LocalizeNodeMenuText));
        }

        private static string LocalizeNodeMenuText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string localized = ST.Library.UI.Lang.Get(text);
            if (IsValidLocalizedMenuText(text, localized))
                return localized;

            localized = ST.Library.UI.Properties.Resources.ResourceManager.GetString(text);
            return IsValidLocalizedMenuText(text, localized) ? localized : text;
        }

        private static bool IsValidLocalizedMenuText(string key, string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, $"[{key}]", StringComparison.Ordinal);
        }

        public void AddNodeContext()
        {
            STNodeEditor.Invalidate();
        }

        public void CopySTNode(STNode sTNode)
        {
            byte[] data = STNodeEditor.GetNodesData(new[] { sTNode });
            STNodeEditor.ImportSelectionData(data, new System.Drawing.Point(sTNode.Left + 30, sTNode.Top + 30));
        }

        public void AddContentMenu()
        {
            STNodeEditor.ContextMenu = new ContextMenu();
            STNodeEditor.ContextMenuOpening += STNodeEditor_ContextMenuOpening;
        }

        private System.Drawing.Point contextCanvasPoint;

        private void STNodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var mousePosition = Mouse.GetPosition(STNodeEditor);
            var clientPoint = new System.Drawing.Point((int)Math.Round(mousePosition.X), (int)Math.Round(mousePosition.Y));
            contextCanvasPoint = STNodeEditor.ControlToCanvas(clientPoint);
            NodeFindInfo findInfo = STNodeEditor.FindNodeFromPoint(contextCanvasPoint);
            ContextMenu menu = STNodeEditor.ContextMenu;
            menu.Items.Clear();

            if (findInfo.NodeOption != null)
            {
                e.Handled = true;
                return;
            }

            if (findInfo.Node != null)
            {
                AddNodeMenuItems(menu.Items, findInfo.Node);
            }
            else
            {
                AddNodeCreationMenuItems(menu.Items);
                AddImportModuleContextMenu(menu.Items);
            }

            if (menu.Items.Count == 0)
            {
                e.Handled = true;
            }
        }

        private void AddNodeMenuItems(ItemCollection items, STNode node)
        {
            var copyItem = new MenuItem { Header = Properties.Resources.Copy };
            copyItem.Click += (s, e) => CopySTNode(node);
            items.Add(copyItem);

            var deleteItem = new MenuItem { Header = Properties.Resources.Delete };
            deleteItem.Click += (s, e) => STNodeEditor.Nodes.Remove(node);
            items.Add(deleteItem);

            if (node is CVCommonNode commonNode)
            {
                var historyItem = new MenuItem { Header = Properties.Resources.Flow_NodeExecutionDetails };
                historyItem.Click += (s, e) => OpenNodeExecutionDetails(commonNode);
                items.Add(historyItem);
            }

            items.Add(new Separator());

            var lockOptionItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockOption)),
                IsCheckable = true,
                IsChecked = node.LockOption
            };
            lockOptionItem.Click += (s, e) => STNodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockOption)),
                () => node.LockOption = !node.LockOption);
            items.Add(lockOptionItem);

            var lockLocationItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                IsCheckable = true,
                IsChecked = node.LockLocation
            };
            lockLocationItem.Click += (s, e) => STNodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                () => node.LockLocation = !node.LockLocation);
            items.Add(lockLocationItem);
        }

        internal static CVCommonNode? ResolveExecutionNode(
            STNodeEditor nodeEditor,
            string? executionNodeName,
            CVCommonNode? preferredNode = null)
        {
            return ResolveExecutionNode(
                nodeEditor.Nodes.OfType<CVCommonNode>(),
                executionNodeName,
                preferredNode);
        }

        internal static CVCommonNode? ResolveExecutionNode(
            IEnumerable<CVCommonNode> nodes,
            string? executionNodeName,
            CVCommonNode? preferredNode = null)
        {
            if (string.IsNullOrWhiteSpace(executionNodeName))
                return null;

            List<CVCommonNode> candidates = nodes.ToList();
            if (preferredNode != null
                && candidates.Contains(preferredNode)
                && IsExecutionNodeNameMatch(preferredNode, executionNodeName))
            {
                return preferredNode;
            }

            var matches = candidates
                .Where(node => IsExecutionNodeNameMatch(node, executionNodeName))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        internal static bool IsExecutionNodeNameMatch(CVCommonNode node, string? executionNodeName)
        {
            if (string.IsNullOrWhiteSpace(executionNodeName))
                return false;

            string candidate = executionNodeName.Trim();
            string fullName = string.IsNullOrWhiteSpace(node.NodeName)
                ? node.Title
                : $"{node.Title}.{node.NodeName}";
            return string.Equals(node.NodeID, candidate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeName, candidate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullName, candidate, StringComparison.OrdinalIgnoreCase);
        }

        public void OpenNodeExecutionDetails(CVCommonNode node, bool focusNode = false)
        {
            if (focusNode)
                FocusNode(node);

            var window = new FlowMessageListWindow(node.NodeID, node.OnGetDrawTitle())
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }

        private void FocusNode(STNode node)
        {
            foreach (STNode selectedNode in STNodeEditor.GetSelectedNode())
            {
                if (!ReferenceEquals(selectedNode, node))
                    selectedNode.SetSelected(bSelected: false, bRedraw: false);
            }

            STNodeEditor.SetActiveNode(node);
            float scale = STNodeEditor.CanvasScale;
            float offsetX = STNodeEditor.ClientSize.Width / 2f - (node.Left + node.Width / 2f) * scale;
            float offsetY = STNodeEditor.ClientSize.Height / 2f - (node.Top + node.Height / 2f) * scale;
            STNodeEditor.MoveCanvas(offsetX, offsetY, bAnimation: false, CanvasMoveArgs.All);
        }

        private void AddNodeCreationMenuItems(ItemCollection items)
        {
            STNodeTreeView.LoadAssembly();
            var groups = STNodeTreeView.NodeTypes
                .Where(item => item.Key.IsSubclassOf(typeof(STNode)) && !item.Key.IsAbstract && !item.Key.IsDefined(typeof(ObsoleteAttribute), inherit: false))
                .GroupBy(item => item.Value)
                .OrderBy(group => group.Key, Comparer<string>.Create((x, y) => Common.NativeMethods.Shlwapi.CompareLogical(x, y)));

            foreach (var group in groups)
            {
                var categoryItem = new MenuItem { Header = LocalizeNodeMenuPath(group.Key) };
                foreach (var entry in group.OrderBy(item => item.Key.Name, StringComparer.CurrentCulture))
                {
                    STNode previewNode;
                    try
                    {
                        previewNode = Activator.CreateInstance(entry.Key) as STNode;
                    }
                    catch
                    {
                        continue;
                    }
                    if (previewNode == null)
                        continue;

                    Type nodeType = entry.Key;
                    var nodeItem = new MenuItem { Header = LocalizeNodeMenuText(previewNode.Title) };
                    nodeItem.Click += (s, e) => CreateNode(nodeType);
                    categoryItem.Items.Add(nodeItem);
                }
                if (categoryItem.Items.Count > 0)
                {
                    items.Add(categoryItem);
                }
            }
        }

        private void CreateNode(Type type)
        {
            if (Activator.CreateInstance(type) is not STNode node)
                return;

            node.Create();
            node.Left = contextCanvasPoint.X;
            node.Top = contextCanvasPoint.Y;

            if (node is CVBaseServerNode serverNode)
            {
                var matchedService = MqttRCService.GetInstance().ServiceTokens.FirstOrDefault(s => s.Devices.Any(d => d.Key == serverNode.DeviceCode));
                if (matchedService != null)
                {
                    serverNode.Token = matchedService.Token;
                }
            }
            else if (node is MQTTStartNode startNode)
            {
                startNode.Server = MQTTControl.Config.Host;
                startNode.Port = MQTTControl.Config.Port;
            }

            STNodeEditor.Nodes.Add(node);
            STNodeEditor.SetActiveNode(node);
        }

        public bool IsHover()
        {
            var mousePosition = Mouse.GetPosition(STNodeEditor);
            var p = STNodeEditor.ControlToCanvas(new System.Drawing.Point((int)Math.Round(mousePosition.X), (int)Math.Round(mousePosition.Y)));

            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode sTNode)
                {
                    bool result = sTNode.Rectangle.Contains(p);
                    if (result)
                        return true;

                    if (sTNode.GetInputOptions() is STNodeOption[] inputOptions)
                    {
                        foreach (STNodeOption inputOption in inputOptions)
                        {
                            if (inputOption != STNodeOption.Empty && inputOption.DotRectangle.Contains(p))
                            {
                                return true;
                            }
                        }
                    }

                    if (sTNode.GetOutputOptions() is STNodeOption[] outputOptions)
                    {
                        foreach (STNodeOption outputOption in outputOptions)
                        {
                            if (outputOption != STNodeOption.Empty && outputOption.DotRectangle.Contains(p))
                            {
                                return true;
                            }
                        }

                    }
                }
            }
            return false;
        }

        #endregion

        private void AddImportModuleContextMenu(ItemCollection items)
        {
            items.Add(new Separator());
            var importModuleItem = new MenuItem { Header = Properties.Resources.Flow_ImportTemplateAsModule };
            importModuleItem.SubmenuOpened += (s, e) =>
            {
                importModuleItem.Items.Clear();
                foreach (var tp in TemplateFlow.Params)
                {
                    string name = tp.Key;
                    var param = tp.Value;
                    var templateItem = new MenuItem { Header = name };
                    templateItem.Click += (s2, e2) =>
                    {
                        if (string.IsNullOrEmpty(param.DataBase64)) return;
                        try
                        {
                            byte[] canvasData = Convert.FromBase64String(param.DataBase64);
                            ImportCanvasAsModule(canvasData);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Import module '{name}' failed", ex);
                        }
                    };
                    importModuleItem.Items.Add(templateItem);
                }
                if (importModuleItem.Items.Count == 0)
                {
                    importModuleItem.Items.Add(new MenuItem { Header = Properties.Resources.Flow_NoTemplateAvailable, IsEnabled = false });
                }
            };
            items.Add(importModuleItem);
        }

        #region AutoLayout
        public ConnectionInfo[] ConnectionInfo { get; set; }
        public float CanvasScale { get => STNodeEditor.CanvasScale; set { STNodeEditor.ScaleCanvas(value, STNodeEditor.ClientSize.Width / 2f, STNodeEditor.ClientSize.Height / 2f); OnPropertyChanged(); } }
        public void AutoSize()
        {
            STNodeEditor.FitCanvasToNodes(0.85f);
            OnPropertyChanged(nameof(CanvasScale));
        }

        private const int AutoLayoutHorizontalSpacing = 220;
        private const int AutoLayoutVerticalSpacing = 80;

        public void ApplyTreeLayout(int startX = 0, int startY = 0)
        {
            using var transaction = STNodeEditor.BeginEditTransaction("自动布局");
            ConnectionInfo = GetLiveConnectionInfo();
            STNode rootNode = GetRootNode();
            if (rootNode == null) return;

            var layout = new SugiyamaLayout(ConnectionInfo, startX, startY, AutoLayoutHorizontalSpacing, AutoLayoutVerticalSpacing,
                STNodeEditor.ClientSize.Width, STNodeEditor.ClientSize.Height);
            layout.Execute(rootNode);
        }

        private ConnectionInfo[] GetLiveConnectionInfo()
        {
            return STNodeEditor.GetConnections();
        }

        List<STNode> GetChildren(STNode node)
        {
            var list = ConnectionInfo.Where(c => c.Output.Owner == node);
            List<STNode> children = new();
            foreach (var item in list)
            {
                children.Add(item.Input.Owner);

            }
            return children;
        }

        public STNode GetRootNode()
        {
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode sTNode && sTNode is MQTTStartNode startNode)
                    return startNode;
            }
            return null;
        }

        public bool CheckFlow()
        {
            ConnectionInfo = STNodeEditor.GetConnectionInfo();
            log.Debug($"CheckFlow: 节点数={STNodeEditor.Nodes.Count}, 连接数={ConnectionInfo?.Length ?? 0}");

            bool isContainsMQTTStartNode = false;
            bool isContainsCVEndNode = false;
            STNode startNode = null;
            STNode endNode = null;

            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is MQTTStartNode mqttStartNode)
                {
                    isContainsMQTTStartNode = true;
                    startNode = mqttStartNode;
                }
                else if (item is CVEndNode cvEndNode)
                {
                    isContainsCVEndNode = true;
                    endNode = cvEndNode;
                }
            }

            if (!isContainsMQTTStartNode)
            {
                log.Warn("CheckFlow: 找不到流程起始结点 (MQTTStartNode)");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoStartNode);
                return false;
            }

            if (!isContainsCVEndNode)
            {
                log.Warn("CheckFlow: 找不到流程结束结点 (CVEndNode)");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoEndNode);
                return false;
            }

            // 检查从起点到终点的路径
            if (!IsPathExists(startNode, endNode))
            {
                log.Warn("CheckFlow: 无法找到从起始结点到结束结点的有效路径");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoPathFromStartToEnd);
                return false;
            }
            log.Debug("CheckFlow: 流程验证通过");
            return true;
        }

        public void Dispose()
        {
            STNodeEditor.ActiveChanged -= STNodeEditorMain_ActiveChanged;
            STNodeEditor.SelectedChanged -= STNodeEditorMain_SelectedChanged;
            STNodeEditor.ContextMenuOpening -= STNodeEditor_ContextMenuOpening;
            GC.SuppressFinalize(this);
        }

        private bool IsPathExists(STNode startNode, STNode endNode)
        {
            var visited = new HashSet<STNode>();
            var queue = new Queue<STNode>();
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                if (currentNode == endNode)
                {
                    return true;
                }

                visited.Add(currentNode);

                var children = GetChildren(currentNode);
                foreach (var child in children)
                {
                    if (!visited.Contains(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return false;
        }
        #endregion
    }
}
