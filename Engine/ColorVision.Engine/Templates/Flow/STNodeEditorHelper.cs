#pragma warning disable CA1304,CA1822,CA1854,CS8602,CS8603,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow.NodeConfigurator;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{

    public class STNodeEditorHelper:ViewModelBase
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

        public STNodeEditorHelper(Control Paraent,STNodeEditor sTNodeEditor)
        {


            STNodeEditor = sTNodeEditor;

            STNodeEditor.ActiveChanged += STNodeEditorMain_ActiveChanged;

            AddContentMenu();

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => 
            {
                foreach (var item in STNodeEditor.GetSelectedNode())
                    STNodeEditor.Nodes.Remove(item);
            } , (s, e) => { e.CanExecute = sTNodeEditor.GetSelectedNode().Length > 0; }));


            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => sTNodeEditor.Nodes.Clear(), (s, e) => { e.CanExecute = true; }));

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) => Copy(), (s, e) => { e.CanExecute = sTNodeEditor.GetSelectedNode().Length > 0; }));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (s, e) => Paste(), (s, e) => { e.CanExecute = Clipboard.ContainsData(ClipboardFormat); }));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => SelectAll(), (s, e) => { e.CanExecute = true; }));

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => sTNodeEditor.Nodes.Clear(), (s, e) => { e.CanExecute = true; }));
        }

        private List<STNode> CopyNodes = new List<STNode>();
        private const string ClipboardFormat = "STNodeEditor_Nodes_V1";

        public void SelectAll()
        {
            foreach (var item in STNodeEditor.Nodes.OfType<STNode>())
            {
                STNodeEditor.AddSelectedNode(item);
            }
        }

        public void Copy()
        {
            var selectedNodes = STNodeEditor.GetSelectedNode();
            if (selectedNodes.Length == 0) return;

            try
            {
                byte[] data = SerializeNodes(selectedNodes);
                string base64 = Convert.ToBase64String(data);
                Clipboard.SetData(ClipboardFormat, base64);
            }
            catch (Exception ex)
            {
                log.Error("Copy failed", ex);
            }
        }

        private byte[] SerializeNodes(STNode[] nodes)
        {
            var nodeSet = new HashSet<STNode>(nodes);
            var optionIndex = new Dictionary<STNodeOption, long>();

            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                // Write node count
                gz.Write(BitConverter.GetBytes(nodes.Length), 0, 4);

                // Compute bounding box for relative positioning
                int minLeft = nodes.Min(n => n.Left);
                int minTop = nodes.Min(n => n.Top);
                gz.Write(BitConverter.GetBytes(minLeft), 0, 4);
                gz.Write(BitConverter.GetBytes(minTop), 0, 4);

                foreach (var node in nodes)
                {
                    byte[] saveData = node.GetSaveData();
                    gz.Write(BitConverter.GetBytes(saveData.Length), 0, 4);
                    gz.Write(saveData, 0, saveData.Length);

                    var inputOpts = node.GetAllInputOptions();
                    if (inputOpts != null)
                    {
                        foreach (var opt in inputOpts)
                        {
                            if (opt != null && !optionIndex.ContainsKey(opt))
                                optionIndex.Add(opt, optionIndex.Count);
                        }
                    }
                    var outputOpts = node.GetAllOutputOptions();
                    if (outputOpts != null)
                    {
                        foreach (var opt in outputOpts)
                        {
                            if (opt != null && !optionIndex.ContainsKey(opt))
                                optionIndex.Add(opt, optionIndex.Count);
                        }
                    }
                }

                // Collect connections that are between selected nodes only
                // and where both options were successfully indexed
                var connections = STNodeEditor.GetConnectionInfo()
                    .Where(c => nodeSet.Contains(c.Output.Owner) && nodeSet.Contains(c.Input.Owner))
                    .Where(c => optionIndex.ContainsKey(c.Output) && optionIndex.ContainsKey(c.Input))
                    .ToList();

                gz.Write(BitConverter.GetBytes(connections.Count), 0, 4);
                foreach (var conn in connections)
                {
                    long packed = (optionIndex[conn.Output] << 32) | (optionIndex[conn.Input] & 0xFFFFFFFFL);
                    gz.Write(BitConverter.GetBytes(packed), 0, 8);
                }
            }
            return ms.ToArray();
        }

        public void Paste()
        {
            if (!Clipboard.ContainsData(ClipboardFormat)) return;

            try
            {
                string base64 = Clipboard.GetData(ClipboardFormat) as string;
                if (string.IsNullOrEmpty(base64)) return;

                byte[] data = Convert.FromBase64String(base64);
                DeserializeAndAddNodes(data);
            }
            catch (Exception ex)
            {
                log.Error("Paste failed", ex);
            }
        }

        private void DeserializeAndAddNodes(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            byte[] buf = new byte[32];

            gz.ReadExactly(buf, 0, 4);
            int nodeCount = BitConverter.ToInt32(buf, 0);

            gz.ReadExactly(buf, 0, 4);
            int origMinLeft = BitConverter.ToInt32(buf, 0);
            gz.ReadExactly(buf, 0, 4);
            int origMinTop = BitConverter.ToInt32(buf, 0);

            // Determine paste position: use mouse position in canvas if available, otherwise offset
            int offsetX, offsetY;
            if (STNodeEditor.IsMouseOver)
            {
                var mousePosition = Mouse.GetPosition(STNodeEditor);
                var clientPt = new System.Drawing.Point((int)Math.Round(mousePosition.X), (int)Math.Round(mousePosition.Y));
                var canvasPt = STNodeEditor.ControlToCanvas(clientPt);
                offsetX = canvasPt.X - origMinLeft;
                offsetY = canvasPt.Y - origMinTop;
            }
            else
            {
                offsetX = 30;
                offsetY = 30;
            }

            var optionMap = new Dictionary<long, STNodeOption>();
            var newNodes = new List<STNode>();

            // Deselect current selection
            foreach (var n in STNodeEditor.GetSelectedNode())
            {
                n.SetSelected(false, false);
                STNodeEditor.RemoveSelectedNode(n);
            }

            for (int i = 0; i < nodeCount; i++)
            {
                gz.ReadExactly(buf, 0, 4);
                int len = BitConverter.ToInt32(buf, 0);
                byte[] nodeData = new byte[len];
                gz.ReadExactly(nodeData, 0, len);

                STNode node = CreateNodeFromSaveData(nodeData);
                if (node == null) continue;

                node.Left += offsetX;
                node.Top += offsetY;

                STNodeEditor.Nodes.Add(node);
                newNodes.Add(node);

                var inputOpts = node.GetAllInputOptions();
                if (inputOpts != null)
                {
                    foreach (var opt in inputOpts)
                    {
                        if (opt != null)
                            optionMap[optionMap.Count] = opt;
                    }
                }
                var outputOpts = node.GetAllOutputOptions();
                if (outputOpts != null)
                {
                    foreach (var opt in outputOpts)
                    {
                        if (opt != null)
                            optionMap[optionMap.Count] = opt;
                    }
                }
            }

            // Restore connections
            gz.ReadExactly(buf, 0, 4);
            int connCount = BitConverter.ToInt32(buf, 0);
            byte[] connBuf = new byte[8];
            for (int i = 0; i < connCount; i++)
            {
                gz.ReadExactly(connBuf, 0, 8);
                long packed = BitConverter.ToInt64(connBuf, 0);
                long outIdx = packed >> 32;
                long inIdx = (int)packed;
                if (optionMap.ContainsKey(outIdx) && optionMap.ContainsKey(inIdx))
                {
                    optionMap[outIdx].ConnectOption(optionMap[inIdx]);
                }
            }

            // Select pasted nodes
            foreach (var node in newNodes)
            {
                node.SetSelected(true, false);
                STNodeEditor.AddSelectedNode(node);
            }
            if (newNodes.Count > 0)
            {
                STNodeEditor.SetActiveNode(newNodes[0]);
            }

            STNodeEditor.Invalidate();
        }

        private STNode CreateNodeFromSaveData(byte[] byData)
        {
            int pos = 0;
            string modelKey = Encoding.UTF8.GetString(byData, pos + 1, byData[pos]);
            pos += byData[pos] + 1;
            string guidKey = Encoding.UTF8.GetString(byData, pos + 1, byData[pos]);
            pos += byData[pos] + 1;

            var dic = new Dictionary<string, byte[]>();
            while (pos < byData.Length)
            {
                int keyLen = BitConverter.ToInt32(byData, pos); pos += 4;
                string key = Encoding.UTF8.GetString(byData, pos, keyLen); pos += keyLen;
                int valLen = BitConverter.ToInt32(byData, pos); pos += 4;
                byte[] val = new byte[valLen];
                Array.Copy(byData, pos, val, 0, valLen); pos += valLen;
                dic[key] = val;
            }

            // Find type from the tree view's loaded assemblies
            Type type = null;
            var treeView = STNodeTreeView;
            // Try to find from the editor's loaded types or use reflection
            string typeName = modelKey.Contains('|') ? modelKey.Split('|')[1] : modelKey;
            string assemblyName = modelKey.Contains('|') ? modelKey.Split('|')[0] : null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName != null && !asm.ManifestModule.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;
                type = asm.GetType(typeName);
                if (type != null) break;
            }

            if (type == null || !type.IsSubclassOf(typeof(STNode)))
            {
                log.Warn($"Cannot find node type: {modelKey}");
                return null;
            }

            var node = (STNode)Activator.CreateInstance(type);
            node.Create();
            node.OnLoadNode(dic);
            return node;
        }

        /// <summary>
        /// Import nodes from a canvas data byte array (STN format with header) 
        /// into the current editor without clearing existing nodes.
        /// This is used to import a saved template as a module/sub-block.
        /// </summary>
        public void ImportCanvasAsModule(byte[] canvasData)
        {
            if (canvasData == null || canvasData.Length < 5)
            {
                log.Warn("ImportCanvasAsModule: invalid canvas data");
                return;
            }

            using var ms = new MemoryStream(canvasData);
            byte[] header = new byte[5];
            ms.Read(header, 0, 5);

            // Validate STN header
            if (BitConverter.ToInt32(header, 0) != STNodeConstant.NodeFlagInt || header[4] != 1)
            {
                log.Warn("ImportCanvasAsModule: invalid STN header");
                return;
            }

            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            byte[] buf = new byte[32];

            // Skip canvas offset and scale (3 floats = 12 bytes)
            gz.ReadExactly(buf, 0, 12);

            // Read node count
            gz.ReadExactly(buf, 0, 4);
            int nodeCount = BitConverter.ToInt32(buf, 0);
            if (nodeCount == 0) return;

            // Determine paste position
            int offsetX, offsetY;
            if (STNodeEditor.IsMouseOver)
            {
                var mousePosition = Mouse.GetPosition(STNodeEditor);
                var clientPt = new System.Drawing.Point((int)Math.Round(mousePosition.X), (int)Math.Round(mousePosition.Y));
                var canvasPt = STNodeEditor.ControlToCanvas(clientPt);
                offsetX = canvasPt.X;
                offsetY = canvasPt.Y;
            }
            else
            {
                // Default: place near the center of the visible canvas area
                var center = STNodeEditor.ControlToCanvas(new System.Drawing.Point(
                    STNodeEditor.ClientSize.Width / 2, STNodeEditor.ClientSize.Height / 2));
                offsetX = center.X;
                offsetY = center.Y;
            }

            var optionMap = new Dictionary<long, STNodeOption>();
            var newNodes = new List<STNode>();
            int origMinLeft = int.MaxValue, origMinTop = int.MaxValue;

            // First pass: create all nodes to find bounding box
            var nodeDataList = new List<byte[]>();
            for (int i = 0; i < nodeCount; i++)
            {
                gz.ReadExactly(buf, 0, 4);
                int len = BitConverter.ToInt32(buf, 0);
                byte[] nodeData = new byte[len];
                gz.ReadExactly(nodeData, 0, len);
                nodeDataList.Add(nodeData);
            }

            // Create nodes and compute bounding box origin
            var createdNodes = new List<STNode>();
            foreach (var nodeData in nodeDataList)
            {
                STNode node = CreateNodeFromSaveData(nodeData);
                if (node == null) continue;
                createdNodes.Add(node);
                if (node.Left < origMinLeft) origMinLeft = node.Left;
                if (node.Top < origMinTop) origMinTop = node.Top;
            }

            if (createdNodes.Count == 0) return;

            // Deselect current selection
            foreach (var n in STNodeEditor.GetSelectedNode())
            {
                n.SetSelected(false, false);
                STNodeEditor.RemoveSelectedNode(n);
            }

            // Add nodes with offset so the module's top-left aligns with the target position
            foreach (var node in createdNodes)
            {
                node.Left = node.Left - origMinLeft + offsetX;
                node.Top = node.Top - origMinTop + offsetY;

                STNodeEditor.Nodes.Add(node);
                newNodes.Add(node);

                var inputOpts = node.GetAllInputOptions();
                if (inputOpts != null)
                {
                    foreach (var opt in inputOpts)
                    {
                        if (opt != null)
                            optionMap[optionMap.Count] = opt;
                    }
                }
                var outputOpts = node.GetAllOutputOptions();
                if (outputOpts != null)
                {
                    foreach (var opt in outputOpts)
                    {
                        if (opt != null)
                            optionMap[optionMap.Count] = opt;
                    }
                }
            }

            // Read and restore connections
            gz.ReadExactly(buf, 0, 4);
            int connCount = BitConverter.ToInt32(buf, 0);
            byte[] connBuf = new byte[8];
            for (int i = 0; i < connCount; i++)
            {
                gz.ReadExactly(connBuf, 0, 8);
                long packed = BitConverter.ToInt64(connBuf, 0);
                long outIdx = packed >> 32;
                long inIdx = (int)packed;
                if (optionMap.ContainsKey(outIdx) && optionMap.ContainsKey(inIdx))
                {
                    optionMap[outIdx].ConnectOption(optionMap[inIdx]);
                }
            }

            // Select imported nodes
            foreach (var node in newNodes)
            {
                node.SetSelected(true, false);
                STNodeEditor.AddSelectedNode(node);
            }
            if (newNodes.Count > 0)
            {
                STNodeEditor.SetActiveNode(newNodes[0]);
            }

            STNodeEditor.Invalidate();
        }



        #region Activate
        private void STNodeEditorMain_ActiveChanged(object? sender, EventArgs e)
        {
            RefreshActiveNodePropertyPanel();
        }

        public void RefreshActiveNodePropertyPanel()
        {
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

            if (STNodeEditor.ActiveNode == null)
            {
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
            var configurator = NodeConfiguratorRegistry.GetConfigurator(STNodeEditor.ActiveNode.GetType());
            if (configurator != null)
            {
                var context = new NodeConfiguratorContext
                {
                    Node = STNodeEditor.ActiveNode,
                    SignStackPanel = signPanel,
                    STNodeEditor = STNodeEditor,
                    Refresh = RefreshActiveNodePropertyPanel
                };
                configurator.Configure(context);
            }

            signPanel.Children.Add(StackPanel);
            StackPanel.Children.Clear();

            var resourceManager = PropertyEditorHelper.GetResourceManager(STNodeEditor.ActiveNode);
            StackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                STNodeEditor.ActiveNode,
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
            Type type = sTNode.GetType();

            STNode sTNode1 = (STNode)Activator.CreateInstance(type);
            if (sTNode1 != null)
            {
                sTNode1.Create();
                PropertyInfo[] properties = type.GetProperties();
                foreach (PropertyInfo property in properties)
                {
                    if (property.CanRead && property.CanWrite)
                    {
                        object value = property.GetValue(sTNode);
                        property.SetValue(sTNode1, value);
                    }
                }
                sTNode1.Left = sTNode.Left;
                sTNode1.Top = sTNode.Top;

                STNodeEditor.Nodes.Add(sTNode1);
            }
        }

        public void AddContentMenu()
        {
            STNodeEditor.ContextMenu = new ContextMenu();
            STNodeEditor.ContextMenuOpening += STNodeEditor_ContextMenuOpening;
            STNodeEditor.OptionDisConnected += (s, e) =>
            {
                IsOptionDisConnected = true;
            };
        }

        bool IsOptionDisConnected;
        private System.Drawing.Point contextCanvasPoint;

        private void STNodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (IsOptionDisConnected)
            {
                IsOptionDisConnected = false;
                e.Handled = true;
                return;
            }

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

            items.Add(new Separator());

            var lockOptionItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockOption)),
                IsCheckable = true,
                IsChecked = node.LockOption
            };
            lockOptionItem.Click += (s, e) => node.LockOption = !node.LockOption;
            items.Add(lockOptionItem);

            var lockLocationItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                IsCheckable = true,
                IsChecked = node.LockLocation
            };
            lockLocationItem.Click += (s, e) => node.LockLocation = !node.LockLocation;
            items.Add(lockLocationItem);
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
            STNodeEditor.FitCanvasToNodes();
            OnPropertyChanged(nameof(CanvasScale));
        }

        private const int AutoLayoutHorizontalSpacing = 220;
        private const int AutoLayoutVerticalSpacing = 80;

        public void ApplyTreeLayout(int startX = 0, int startY = 0)
        {
            ConnectionInfo = GetLiveConnectionInfo();
            STNode rootNode = GetRootNode();
            if (rootNode == null) return;

            var layout = new SugiyamaLayout(ConnectionInfo, startX, startY, AutoLayoutHorizontalSpacing, AutoLayoutVerticalSpacing,
                STNodeEditor.ClientSize.Width, STNodeEditor.ClientSize.Height);
            layout.Execute(rootNode);
        }

        private ConnectionInfo[] GetLiveConnectionInfo()
        {
            var connections = new List<ConnectionInfo>();
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is not STNode node)
                    continue;

                var outputOptions = node.GetAllOutputOptions();
                foreach (var output in outputOptions)
                {
                    if (output == null || output == STNodeOption.Empty || output.ConnectedOption == null)
                        continue;

                    foreach (var input in output.ConnectedOption)
                    {
                        if (input == null || input == STNodeOption.Empty)
                            continue;

                        connections.Add(new ConnectionInfo
                        {
                            Output = output,
                            Input = input
                        });
                    }
                }
            }

            return connections.ToArray();
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
