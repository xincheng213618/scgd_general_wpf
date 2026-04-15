#pragma warning disable CS8603,CS8604
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
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{

    public class STNodeEditorHelper:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(STNodeEditorHelper));

        public STNodeEditor STNodeEditor { get; set; }

        /// <summary>
        /// Direct panel references for standalone windows (FlowEngineToolWindow).
        /// When set, embedded mode is used. When null, dock panel mode is used.
        /// </summary>
        public STNodePropertyGrid STNodePropertyGrid1 { get; set; }
        public StackPanel SignStackPanel { get; set; }
        public System.Windows.Controls.Grid PropertyEditorPanel { get; set; }
        public WindowsFormsHost PropertyGridHost { get; set; }
        public ScrollViewer SignStackScrollViewer { get; set; }

        /// <summary>
        /// Whether to use the AvalonDock panel (true) or embedded panel references (false).
        /// </summary>
        public bool UseDockPanel { get; set; }

        private bool _isPropertyGridMode = true;


        public static STNodeTreeView STNodeTreeView { get 
            {
                if (_STNodeTreeView == null)
                {
                    _STNodeTreeView = new STNodeTreeView();
                    _STNodeTreeView.LoadAssembly("FlowEngineLib.dll");
                }
                return _STNodeTreeView;
            }
        }
        private static STNodeTreeView _STNodeTreeView;

        public STNodeEditorHelper(Control Paraent,STNodeEditor sTNodeEditor)
        {


            STNodeEditor = sTNodeEditor;

            STNodeEditor.NodeAdded += StNodeEditor1_NodeAdded;
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

            gz.Read(buf, 0, 4);
            int nodeCount = BitConverter.ToInt32(buf, 0);

            gz.Read(buf, 0, 4);
            int origMinLeft = BitConverter.ToInt32(buf, 0);
            gz.Read(buf, 0, 4);
            int origMinTop = BitConverter.ToInt32(buf, 0);

            // Determine paste position: use mouse position in canvas if available, otherwise offset
            int offsetX, offsetY;
            var cursorPos = System.Windows.Forms.Cursor.Position;
            var editorScreenRect = STNodeEditor.RectangleToScreen(STNodeEditor.ClientRectangle);
            if (editorScreenRect.Contains(cursorPos))
            {
                var clientPt = STNodeEditor.PointToClient(cursorPos);
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
                gz.Read(buf, 0, 4);
                int len = BitConverter.ToInt32(buf, 0);
                byte[] nodeData = new byte[len];
                gz.Read(nodeData, 0, len);

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
            gz.Read(buf, 0, 4);
            int connCount = BitConverter.ToInt32(buf, 0);
            byte[] connBuf = new byte[8];
            for (int i = 0; i < connCount; i++)
            {
                gz.Read(connBuf, 0, 8);
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
            gz.Read(buf, 0, 12);

            // Read node count
            gz.Read(buf, 0, 4);
            int nodeCount = BitConverter.ToInt32(buf, 0);
            if (nodeCount == 0) return;

            // Determine paste position
            int offsetX, offsetY;
            var cursorPos = System.Windows.Forms.Cursor.Position;
            var editorScreenRect = STNodeEditor.RectangleToScreen(STNodeEditor.ClientRectangle);
            if (editorScreenRect.Contains(cursorPos))
            {
                var clientPt = STNodeEditor.PointToClient(cursorPos);
                var canvasPt = STNodeEditor.ControlToCanvas(clientPt);
                offsetX = canvasPt.X;
                offsetY = canvasPt.Y;
            }
            else
            {
                // Default: place near the center of the visible canvas area
                var center = STNodeEditor.ControlToCanvas(new System.Drawing.Point(
                    STNodeEditor.Width / 2, STNodeEditor.Height / 2));
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
                gz.Read(buf, 0, 4);
                int len = BitConverter.ToInt32(buf, 0);
                byte[] nodeData = new byte[len];
                gz.Read(nodeData, 0, len);
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
            gz.Read(buf, 0, 4);
            int connCount = BitConverter.ToInt32(buf, 0);
            byte[] connBuf = new byte[8];
            for (int i = 0; i < connCount; i++)
            {
                gz.Read(connBuf, 0, 8);
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
            STNodePropertyGrid propertyGrid;
            StackPanel signPanel;

            if (UseDockPanel)
            {
                var dockPanel = FlowNodePropertyPanel.Instance;
                if (dockPanel == null) return;
                propertyGrid = dockPanel.NodePropertyGrid;
                signPanel = dockPanel.SignStackPanel;
            }
            else
            {
                if (STNodePropertyGrid1 == null || SignStackPanel == null || PropertyEditorPanel == null)
                    return;
                propertyGrid = STNodePropertyGrid1;
                signPanel = SignStackPanel;
            }

            propertyGrid.SetNode(STNodeEditor.ActiveNode);
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
                    STNodePropertyGrid = propertyGrid,
                    STNodeEditor = STNodeEditor,
                    PropertyStackPanel = StackPanel,
                    OnActiveChanged = () => STNodeEditorMain_ActiveChanged(this, new EventArgs())
                };
                configurator.Configure(context);
            }

            signPanel.Children.Add(StackPanel);
            StackPanel.Children.Clear();

            StackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(STNodeEditor.ActiveNode, ST.Library.UI.Properties.Resources.ResourceManager));
            signPanel.Visibility = signPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        public StackPanel StackPanel { get; set; } = new StackPanel();

        public void TogglePropertyEditorMode()
        {
            if (UseDockPanel) return; // Dock panel handles its own toggle
            _isPropertyGridMode = !_isPropertyGridMode;
            if (PropertyGridHost != null)
                PropertyGridHost.Visibility = _isPropertyGridMode ? Visibility.Visible : Visibility.Collapsed;
            if (SignStackScrollViewer != null)
                SignStackScrollViewer.Visibility = _isPropertyGridMode ? Visibility.Collapsed : Visibility.Visible;
        }

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

        public void AddNodeContext()
        {
            foreach (var item in STNodeEditor.Nodes)
            {
                if (item is STNode node)
                {
                    node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    node.ContextMenuStrip.Items.Add("复制", null, (s, e1) => CopySTNode(node));
                    node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor.Nodes.Remove(node));
                    node.ContextMenuStrip.Items.Add("LockOption", null, (s, e1) => STNodeEditor.ActiveNode.LockOption = !STNodeEditor.ActiveNode.LockOption);
                    node.ContextMenuStrip.Items.Add("LockLocation", null, (s, e1) => STNodeEditor.ActiveNode.LockLocation = !STNodeEditor.ActiveNode.LockLocation);
                }
            }
        }


        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = e.Node;
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor.Nodes.Remove(node));
            node.ContextMenuStrip.Items.Add("复制", null, (s, e1) => CopySTNode(node));
            node.ContextMenuStrip.Items.Add("LockOption", null, (s, e1) => STNodeEditor.ActiveNode.LockOption = !STNodeEditor.ActiveNode.LockOption);
            node.ContextMenuStrip.Items.Add("LockLocation", null, (s, e1) => STNodeEditor.ActiveNode.LockLocation = !STNodeEditor.ActiveNode.LockLocation);
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
            STNodeEditor.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            Type STNodeTreeViewtype = STNodeTreeView.GetType();

            // 获取私有字段信息
            FieldInfo fieldInfo = STNodeTreeViewtype.GetField("m_dic_all_type", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                // 获取字段的值
                var value = fieldInfo.GetValue(STNodeTreeView);
                Dictionary<string, List<Type>> values = new Dictionary<string, List<Type>>();
                if (value is Dictionary<Type, string> m_dic_all_type)
                {
                    foreach (var item in m_dic_all_type)
                    {
                        if (values.TryGetValue(item.Value, out List<Type>? value1))
                        {
                            value1.Add(item.Key);
                        }
                        else
                        {
                            values.Add(item.Value, new List<Type>() { item.Key });
                        }
                    }

                    foreach (var nodetype in values.OrderBy(x => x.Key, Comparer<string>.Create((x, y) => Common.NativeMethods.Shlwapi.CompareLogical(x, y))))
                    {
                        string header = nodetype.Key.Replace("FlowEngineLib/", "");
                        var toolStripItem = new System.Windows.Forms.ToolStripMenuItem(header);


                        foreach (var type in nodetype.Value)
                        {
                            if (type.IsSubclassOf(typeof(STNode)))
                            {
                                if (Activator.CreateInstance(type) is STNode sTNode)
                                {
                                    toolStripItem.DropDownItems.Add(sTNode.Title, null, (s, e) =>
                                    {
                                        STNode sTNode1 = (STNode)Activator.CreateInstance(type);
                                        if (sTNode1 != null)
                                        {
                                            sTNode1.Create();
                                            var p = STNodeEditor.PointToClient(lastMousePosition);
                                            p = STNodeEditor.ControlToCanvas(p);
                                            sTNode1.Left = p.X;
                                            sTNode1.Top = p.Y;

                                            if (sTNode1 is CVBaseServerNode vBaseServerNode)
                                            {
                                                var matchedService = MqttRCService.GetInstance().ServiceTokens.FirstOrDefault(s => s.Devices.Any(d => d.Key == vBaseServerNode.DeviceCode));

                                                if (matchedService != null)
                                                {
                                                    vBaseServerNode.Token = matchedService.Token;
                                                }
                                            }
                                            else if (sTNode1 is MQTTStartNode startNode)
                                            {
                                                startNode.Server = MQTTControl.Config.Host;
                                                startNode.Port = MQTTControl.Config.Port;
                                            }

                                            STNodeEditor.Nodes.Add(sTNode1);
                                        }
                                    });
                                }
                            }

                        }
                        STNodeEditor.ContextMenuStrip.Items.Add(toolStripItem);

                    }

                }
            }

            // Add "Import Template as Module" submenu
            AddImportModuleContextMenu();

            STNodeEditor.ContextMenuStrip.Opening += (s, e) =>
            {
                if (IsOptionDisConnected) e.Cancel = true;
                if (IsHover())
                    e.Cancel = true;
                IsOptionDisConnected = false;
            };
            STNodeEditor.OptionDisConnected += (s, e) =>
            {
                IsOptionDisConnected = true;
            };
        }
        bool IsOptionDisConnected;


        private System.Drawing.Point lastMousePosition;

        public bool IsHover()
        {
            lastMousePosition = System.Windows.Forms.Cursor.Position;
            var p = STNodeEditor.PointToClient(System.Windows.Forms.Cursor.Position);
            p = STNodeEditor.ControlToCanvas(p);

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

        private void AddImportModuleContextMenu()
        {
            STNodeEditor.ContextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var importModuleItem = new System.Windows.Forms.ToolStripMenuItem("导入模板为模块");
            importModuleItem.DropDownOpening += (s, e) =>
            {
                importModuleItem.DropDownItems.Clear();
                foreach (var tp in TemplateFlow.Params)
                {
                    string name = tp.Key;
                    var param = tp.Value;
                    importModuleItem.DropDownItems.Add(name, null, (s2, e2) =>
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
                    });
                }
                if (importModuleItem.DropDownItems.Count == 0)
                {
                    var emptyItem = new System.Windows.Forms.ToolStripMenuItem("(无可用模板)") { Enabled = false };
                    importModuleItem.DropDownItems.Add(emptyItem);
                }
            };
            STNodeEditor.ContextMenuStrip.Items.Add(importModuleItem);
        }

        #region AutoLayout
        public ConnectionInfo[] ConnectionInfo { get; set; }
        public float CanvasScale { get => STNodeEditor.CanvasScale; set { STNodeEditor.ScaleCanvas(value, STNodeEditor.CanvasValidBounds.X + STNodeEditor.CanvasValidBounds.Width / 2, STNodeEditor.CanvasValidBounds.Y + STNodeEditor.CanvasValidBounds.Height / 2); OnPropertyChanged(); } }
        public void AutoSize()
        {
            // Calculate the centers
            var boundsCenterX = STNodeEditor.Bounds.Width / 2;
            var boundsCenterY = STNodeEditor.Bounds.Height / 2;

            // Calculate the scale factor to fit CanvasValidBounds within Bounds
            var scaleX = (float)STNodeEditor.Bounds.Width / (float)STNodeEditor.CanvasValidBounds.Width;
            var scaleY = (float)STNodeEditor.Bounds.Height / (float)STNodeEditor.CanvasValidBounds.Height;
            CanvasScale = Math.Min(scaleX, scaleY);
            CanvasScale = CanvasScale > 1 ? 1 : CanvasScale;
            // Apply the scale
            STNodeEditor.ScaleCanvas(CanvasScale, STNodeEditor.CanvasValidBounds.X + STNodeEditor.CanvasValidBounds.Width / 2, STNodeEditor.CanvasValidBounds.Y + STNodeEditor.CanvasValidBounds.Height / 2);

            var validBoundsCenterX = STNodeEditor.CanvasValidBounds.Width / 2;

            // Align to top-left with a small margin
            var offsetX = 10 - STNodeEditor.CanvasValidBounds.X * CanvasScale;
            var offsetY = 10 - STNodeEditor.CanvasValidBounds.Y * CanvasScale;


            // Move the canvas
            STNodeEditor.MoveCanvas(offsetX, STNodeEditor.CanvasOffset.Y, bAnimation: true, CanvasMoveArgs.Left);
            STNodeEditor.MoveCanvas(offsetX, offsetY, bAnimation: true, CanvasMoveArgs.Top);
        }

        public void ApplyTreeLayout(int startX, int startY, int horizontalSpacing, int verticalSpacing)
        {
            ConnectionInfo = STNodeEditor.GetConnectionInfo();
            STNode rootNode = GetRootNode();
            if (rootNode == null) return;

            var layout = new SugiyamaLayout(ConnectionInfo, startX, startY, horizontalSpacing, verticalSpacing,
                STNodeEditor.Width, STNodeEditor.Height);
            layout.Execute(rootNode);
            AutoSize();
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
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程起始结点");
                return false;
            }

            if (!isContainsCVEndNode)
            {
                log.Warn("CheckFlow: 找不到流程结束结点 (CVEndNode)");
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程结束结点");
                return false;
            }

            // 检查从起点到终点的路径
            if (!IsPathExists(startNode, endNode))
            {
                log.Warn("CheckFlow: 无法找到从起始结点到结束结点的有效路径");
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法找到从起始结点到结束结点的有效路径");
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
