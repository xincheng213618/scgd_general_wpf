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
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{

    public class STNodeEditorHelper:ViewModelBase
    {
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

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) => Copy(), (s, e) => { e.CanExecute = true; }));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (s, e) => Paste(), (s, e) => { e.CanExecute = CopyNodes.Count >0;}));
            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => SelectAll(), (s, e) => { e.CanExecute = true; }));

            Paraent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => sTNodeEditor.Nodes.Clear(), (s, e) => { e.CanExecute = true; }));
        }

        private List<STNode> CopyNodes = new List<STNode>();

        public void SelectAll()
        {
            foreach (var item in STNodeEditor.Nodes.OfType<STNode>())
            {
                STNodeEditor.AddSelectedNode(item);
            }
        }

        public void Copy()
        {
            CopyNodes.Clear();
            foreach (var item in STNodeEditor.GetSelectedNode())
            {
                CopyNodes.Add(item);
            }
        }

        public void Paste()
        {
            int offset = 10;

            foreach (var item in CopyNodes)
            {
                Type type = item.GetType();

                STNode sTNode1 = (STNode)Activator.CreateInstance(type);
                if (sTNode1 != null)
                {
                    sTNode1.Create();
                    PropertyInfo[] properties = type.GetProperties();
                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanRead && property.CanWrite)
                        {
                            object value = property.GetValue(item);
                            property.SetValue(sTNode1, value);
                        }
                    }
                    sTNode1.Left = item.Left + offset;
                    sTNode1.Top = item.Top + offset;
                    sTNode1.IsSelected = true;
                    STNodeEditor.Nodes.Add(sTNode1);
                    if (CopyNodes.Count == 1)
                    {
                        item.IsSelected = false;
                        STNodeEditor.RemoveSelectedNode(item);
                        STNodeEditor.AddSelectedNode(sTNode1);
                        STNodeEditor.SetActiveNode(sTNode1);
                    }
                    else
                    {
                        STNodeEditor.RemoveSelectedNode(item);
                        STNodeEditor.AddSelectedNode(sTNode1);
                    }
                }
            }

            CopyNodes.Clear();
            foreach (var item in STNodeEditor.GetSelectedNode())
            {
                CopyNodes.Add(item);
            }



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
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程起始结点");
                return false;
            }

            if (!isContainsCVEndNode)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到流程结束结点");
                return false;
            }

            // 检查从起点到终点的路径
            if (!IsPathExists(startNode, endNode))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法找到从起始结点到结束结点的有效路径");
                return false;
            }
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
