#pragma warning disable CA1720,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.Batch;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace ColorVision.Engine.Services.Flow
{



    /// <summary>
    /// CVFlowView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewFlow : System.Windows.Controls.UserControl, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewFlow));

        public FlowEngineManager FlowEngineManager { get; set; }
        public FlowEngineControl FlowEngineControl { get; set; }
        public FlowEngineConfig Config { get; set; }

        public RelayCommand AutoSizeCommand { get; set; }

        public event EventHandler RefreshFlow;

        public RelayCommand RefreshCommand { get; set; }

        public RelayCommand ClearCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public RelayCommand AutoAlignmentCommand { get; set; }

        public RelayCommand OpenFlowTemplateCommand { get; set; }

        public RelayCommand NewFlowCommand { get; set; }
        public RelayCommand DeleteFlowCommand { get; set; }
        public RelayCommand ExportFlowCommand { get; set; }
        public RelayCommand ImportFlowCommand { get; set; }


        public DisplayFlow DisplayFlow { get; set; }

        public ViewFlow(FlowEngineManager flowEngineManager)
        {
            FlowEngineManager = flowEngineManager;
            FlowEngineControl = FlowEngineManager.FlowEngineControl;
            Config = FlowEngineManager.Config;

            InitializeComponent();

            AutoSizeCommand = new RelayCommand(a => STNodeEditorHelper.AutoSize());
            RefreshCommand = new RelayCommand(a => Refresh());
            ClearCommand = new RelayCommand(a => Clear());
            SaveCommand = new RelayCommand(a => Save());
            AutoAlignmentCommand = new RelayCommand(a => AutoAlignment());
            OpenFlowTemplateCommand = new RelayCommand(a => OpenFlowTemplate());
            NewFlowCommand = new RelayCommand(a => NewFlow());
            DeleteFlowCommand = new RelayCommand(a => DeleteFlow(), a => FlowEngineManager.GetInstance().SlectFlowParam != null);
            ExportFlowCommand = new RelayCommand(a => ExportFlow(), a => FlowEngineManager.GetInstance().SlectFlowParam != null);
            ImportFlowCommand = new RelayCommand(a => ImportFlow());

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => Save(), (s, e) => { e.CanExecute = STNodeEditorHelper != null; }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(), (s, e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) => { e.CanExecute = UndoStack.Count > 0; if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
            CommandBindings.Add(new CommandBinding(EngineCommands.StartExecutionCommand, (s, e) => DisplayFlow.RunFlow(), (s, e) =>
            {
                if (DisplayFlow.FlowControl != null)
                    e.CanExecute = !DisplayFlow.FlowControl.IsFlowRun;
            }));
            CommandBindings.Add(new CommandBinding(EngineCommands.StopExecutionCommand, (s, e) => DisplayFlow.StopFlow(), (s, e) =>
            {
                if (DisplayFlow.FlowControl != null)
                    e.CanExecute = DisplayFlow.FlowControl.IsFlowRun;
            }));

            ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
            ThemeChanged(ThemeManager.Current.CurrentUITheme);

        }

        void ThemeChanged(Theme theme)
        {
            if (theme == Theme.Dark)
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 34, 34, 34);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 255, 255, 255);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 50, 50, 50);



            }
            else
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 150, 150, 150);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 200, 200, 200);
            }
        }
        #region ActionCommand

        public ObservableCollection<ActionCommand> UndoStack { get; set; } = new ObservableCollection<ActionCommand>();
        public ObservableCollection<ActionCommand> RedoStack { get; set; } = new ObservableCollection<ActionCommand>();

        public void ClearActionCommand()
        {
            UndoStack.Clear();
            RedoStack.Clear();
        }

        public void AddActionCommand(ActionCommand actionCommand)
        {
            UndoStack.Add(actionCommand);
            RedoStack.Clear();
        }

        public void Undo()
        {
            if (UndoStack.Count > 0)
            {
                var undoAction = UndoStack[^1]; // Access the last element
                UndoStack.RemoveAt(UndoStack.Count - 1); // Remove the last element
                undoAction.UndoAction();
                RedoStack.Add(undoAction);
            }
        }

        public void Redo()
        {
            if (RedoStack.Count > 0)
            {
                var redoAction = RedoStack[^1]; // Access the last element
                RedoStack.RemoveAt(RedoStack.Count - 1); // Remove the last element
                redoAction.RedoAction();
                UndoStack.Add(redoAction);
            }
        }
        #endregion

        public void OpenFlowTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            Refresh();
        }

        public void NewFlow()
        {
            var templateFlow = new TemplateFlow();
            templateFlow.Load();
            string name = $"Flow_{DateTime.Now:yyyyMMdd_HHmmss}";
            templateFlow.Create(name);
            Refresh();
        }

        public void DeleteFlow()
        {
            var flowParam = FlowEngineManager.GetInstance().SlectFlowParam;
            if (flowParam == null) return;

            if (MessageBox.Show(Application.Current.GetActiveWindow(),
                $"确认删除流程 \"{flowParam.Name}\" ?", "ColorVision",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            var templateFlow = new TemplateFlow();
            templateFlow.Load();
            int index = templateFlow.TemplateParams.ToList().FindIndex(p => p.Value.Id == flowParam.Id);
            if (index >= 0)
            {
                templateFlow.Delete(index);
            }
            Refresh();
        }

        public void ExportFlow()
        {
            var flowParam = FlowEngineManager.GetInstance().SlectFlowParam;
            if (flowParam == null) return;

            var templateFlow = new TemplateFlow();
            templateFlow.Load();
            int index = templateFlow.TemplateParams.ToList().FindIndex(p => p.Value.Id == flowParam.Id);
            if (index >= 0)
            {
                templateFlow.Export(index);
            }
        }

        public void ImportFlow()
        {
            var templateFlow = new TemplateFlow();
            templateFlow.Load();
            if (templateFlow.Import())
            {
                string importName = templateFlow.ImportName ?? $"Imported_{DateTime.Now:yyyyMMdd_HHmmss}";
                templateFlow.Create(importName);
            }
            Refresh();
        }

        public void AutoAlignment()
        {
            STNodeEditorHelper.ApplyTreeLayout(startX: 0, startY: 0, Config.horizontalSpacing, Config.verticalSpacing);
            STNodeEditorHelper.AutoSize();
        }

        public void Save()
        {
            log.Info("Save: 开始保存流程");

            // 强制将焦点从WinForms控件转移到WPF, 确保属性编辑器中的值已提交
            var focusedElement = Keyboard.FocusedElement;
            log.Debug($"Save: 当前焦点元素: {focusedElement?.GetType().Name ?? "null"}");
            if (focusedElement == null || focusedElement is System.Windows.Forms.Integration.WindowsFormsHost)
            {
                log.Debug("Save: 焦点在WinForms控件上, 先转移焦点以提交待保存的属性修改");
                this.Focus();
                // 处理WinForms的Leave等事件
                System.Windows.Forms.Application.DoEvents();
            }

            try
            {
                if (!STNodeEditorHelper.CheckFlow())
                {
                    log.Warn("Save: CheckFlow验证失败, 取消保存");
                    return;
                }

                var flowParam = FlowEngineManager.GetInstance().SlectFlowParam;
                if (flowParam == null)
                {
                    log.Error("Save: SlectFlowParam 为 null, 无法保存");
                    MessageBox.Show(Application.Current.GetActiveWindow(), "当前未选择流程参数, 无法保存");
                    return;
                }

                byte[] canvasData = STNodeEditorMain.GetCanvasData();
                if (canvasData == null || canvasData.Length == 0)
                {
                    log.Error("Save: GetCanvasData 返回空数据");
                    MessageBox.Show(Application.Current.GetActiveWindow(), "获取画布数据失败");
                    return;
                }

                log.Info($"Save: 画布数据大小={canvasData.Length} bytes, FlowParam.Id={flowParam.Id}, Name={flowParam.Name}");
                flowParam.DataBase64 = Convert.ToBase64String(canvasData);
                TemplateFlow.Save2DB(flowParam);
                log.Info("Save: 流程保存成功");
            }
            catch (Exception ex)
            {
                log.Error("Save: 保存流程时发生异常", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"保存失败: {ex.Message}");
            }
        }


        public void Refresh()
        {
            RefreshFlow?.Invoke(this, new EventArgs());
        }
        public void Clear()
        {
            STNodeEditorMain.Nodes.Clear();
        }

        public STNodeEditorHelper STNodeEditorHelper { get; set; }

        
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            
            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == WinForms.Keys.Delete)
                {
                    if (STNodeEditorMain.ActiveNode != null)
                    {
                        var node = STNodeEditorMain.ActiveNode;
                        STNodeEditorMain.Nodes.Remove(node);
                    }

                    foreach (var item in STNodeEditorMain.GetSelectedNode())
                    {
                        STNodeEditorMain.Nodes.Remove(item);
                    }
                }
                if (e.KeyCode == WinForms.Keys.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    AutoAlignment();
                }
            };

            FlowEngineControl.AttachNodeEditor(STNodeEditorMain);


            var manager = DockViewManager.GetInstance();
            manager.AddView(0, this);
            manager.ViewTitles[this] = ColorVision.Engine.Properties.Resources.Workflow;

            STNodeEditorHelper = new STNodeEditorHelper(this, STNodeEditorMain);

            // Use AvalonDock panel for node property editing
            STNodeEditorHelper.UseDockPanel = true;

            // Hide the property panel when this view loses focus to another view
            this.Loaded += (s, e) => DockViewManager.GetInstance().ActiveViewChanged += OnActiveViewChanged;
            this.Unloaded += (s, e) => DockViewManager.GetInstance().ActiveViewChanged -= OnActiveViewChanged;
        }

        private void OnActiveViewChanged(System.Windows.Controls.Control? activeView)
        {
            // Only hide when another registered view becomes active.
            // If activeView is null it means focus moved to a non-view control
            // (e.g. the embedded WinForms STNodeEditor) — don't hide in that case.
            if (activeView != null && activeView != this)
            {
                // Hide the property panel when another view becomes active
                if (WorkspaceManager.LayoutManager?.IsPanelVisible(FlowNodePropertyPanel.PanelId) == true)
                    WorkspaceManager.LayoutManager?.TogglePanel(FlowNodePropertyPanel.PanelId);
            }
        }


        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualHeight > 250) 
            {
                ProgressBar1.Visibility = Visibility.Collapsed;
                GridControl.Visibility = Visibility.Visible;
            }
            else
            {
                ProgressBar1.Visibility = Visibility.Visible;
                GridControl.Visibility = Visibility.Collapsed;
            }
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (STNodeEditorMain.ActiveNode == null && STNodeEditorMain.GetSelectedNode().Length == 0)
            {
                if (e.Key == Key.Add)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    e.Handled = true;
                }
            }
            else
            {

                foreach (var item in STNodeEditorMain.GetSelectedNode())
                {
                    if (e.Key == Key.Left)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Right)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);

                        e.Handled = true;
                    }
                }
            }
        }


        private bool IsMouseDown;
        private System.Drawing.Point lastMousePosition;
        private void STNodeEditorMain_MouseDown(object sender, WinForms.MouseEventArgs e)
        {
            lastMousePosition = e.Location;
            System.Drawing.PointF m_pt_down_in_canvas = new System.Drawing.PointF();
            m_pt_down_in_canvas.X = ((float)e.X - STNodeEditorMain.CanvasOffsetX) / STNodeEditorMain.CanvasScale;
            m_pt_down_in_canvas.Y = ((float)e.Y - STNodeEditorMain.CanvasOffsetY) / STNodeEditorMain.CanvasScale;
            NodeFindInfo nodeFindInfo = STNodeEditorMain.FindNodeFromPoint(m_pt_down_in_canvas);

            if (!string.IsNullOrEmpty(nodeFindInfo.Mark))
            {

            }
            else if (nodeFindInfo.Node != null)
            {

            }
            else if (nodeFindInfo.NodeOption != null)
            {

            }
            else if (e.Button == WinForms.MouseButtons.Left)
            {
                IsMouseDown = true;
            }
        }

        private void STNodeEditorMain_MouseUp(object sender, WinForms.MouseEventArgs e)
        {
            IsMouseDown = false;
        }

        private void STNodeEditorMain_MouseMove(object sender, WinForms.MouseEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && IsMouseDown)
            {        // 计算鼠标移动的距离
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                // 更新画布偏移
                STNodeEditorMain.MoveCanvas(
                    STNodeEditorMain.CanvasOffsetX + deltaX,
                    STNodeEditorMain.CanvasOffsetY + deltaY,
                    bAnimation: false,
                    CanvasMoveArgs.All
                );

                // 更新最后的鼠标位置
                lastMousePosition = e.Location;
            }
        }


        private void STNodeEditorMain_MouseWheel(object sender, WinForms.MouseEventArgs e)
        {
            var mousePosition = e.Location; // e.Location 已是控件坐标
            float delta = e.Delta > 0 ? 0.05f : -0.05f;
            STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + delta, mousePosition.X, mousePosition.Y);
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (FlowEngineManager.Batch != null)
            {
                Frame frame = new Frame();

                MeasureBatchPage batchDataHistory = new MeasureBatchPage(frame, FlowEngineManager.Batch);
                Window window = new Window() { Owner = Application.Current.GetActiveWindow() };
                window.Content = batchDataHistory;
                window.Show();
            }
            else
            {
                MessageBox.Show("请先执行流程");
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            BatchManager.GetInstance().Edit();
        }

        private void Button_Click_PreProcess(object sender, RoutedEventArgs e)
        {
            PreProcessManager.GetInstance().Edit();
        }


        public void Dispose()
        {
            ThemeManager.Current.CurrentUIThemeChanged -= ThemeChanged;

            STNodeEditorMain?.Dispose();
            winf1?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            FlowEngineManager.DisplayFlow.RunFlow();

        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            FlowEngineManager.DisplayFlow.StopFlow();

        }

        private void Button_Click_NodeAnalysis(object sender, RoutedEventArgs e)
        {
            if (FlowEngineManager.Batch != null)
            {
                var window = new FlowNodeAnalysisWindow(FlowEngineManager.Batch) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Show();
            }
            else
            {
                var window = new FlowNodeAnalysisWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Show();
            }
        }

    }


}
