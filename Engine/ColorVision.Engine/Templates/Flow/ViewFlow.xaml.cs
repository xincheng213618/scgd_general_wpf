#pragma warning disable CA1720,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.Batch;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        public RelayCommand ImportModuleCommand { get; set; }


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
            ImportModuleCommand = new RelayCommand(a => ImportModule(), a => TemplateFlow.Params.Count > 0);

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
                string.Format(Properties.Resources.Flow_ConfirmDeleteFlow, flowParam.Name), "ColorVision",
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

        public void ImportModule()
        {
            var templateFlow = new TemplateFlow();
            templateFlow.Load();
            var items = templateFlow.TemplateParams;
            if (items.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoFlowTemplate, "ColorVision");
                return;
            }

            var dialog = new TemplateSelectionDialog(items)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
            {
                string base64 = dialog.SelectedTemplate.Value.DataBase64;
                if (string.IsNullOrEmpty(base64))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_TemplateNoFlowData, "ColorVision");
                    return;
                }

                try
                {
                    byte[] canvasData = Convert.FromBase64String(base64);
                    STNodeEditorHelper.ImportCanvasAsModule(canvasData);
                }
                catch (Exception ex)
                {
                    log.Error("ImportModule failed", ex);
                    MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.Flow_ImportModuleFailed, ex.Message), "ColorVision");
                }
            }
        }

        public void AutoAlignment()
        {
            STNodeEditorHelper.ApplyTreeLayout(startX: 0, startY: 0, Config.horizontalSpacing, Config.verticalSpacing);
            STNodeEditorHelper.AutoSize();
        }

        public void Save()
        {
            log.Info("Save: 开始保存流程");

            // Force focus back to the view so WPF property editors commit pending values.
            var focusedElement = Keyboard.FocusedElement;
            log.Debug($"Save: 当前焦点元素: {focusedElement?.GetType().Name ?? "null"}");
            if (focusedElement == null || focusedElement == STNodeEditorMain)
            {
                log.Debug("Save: 先转移焦点以提交待保存的属性修改");
                this.Focus();
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
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoFlowParamSelected);
                    return;
                }

                byte[] canvasData = STNodeEditorMain.GetCanvasData();
                if (canvasData == null || canvasData.Length == 0)
                {
                    log.Error("Save: GetCanvasData 返回空数据");
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_GetCanvasDataFailed);
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
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.Flow_SaveFailed, ex.Message));
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
            STNodeEditorMain.ConfigureCreatedNode = ConfigureCreatedNode;
            FlowEngineControl.AttachNodeEditor(STNodeEditorMain.CoreEditor);


            var manager = DockViewManager.GetInstance();
            manager.AddView(0, this);
            manager.ViewTitles[this] = ColorVision.Engine.Properties.Resources.Workflow;

            STNodeEditorHelper = new STNodeEditorHelper(this, STNodeEditorMain.CoreEditor);

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
            // (e.g. the WPF editor surface) - don't hide in that case.
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
            if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                AutoAlignment();
                e.Handled = true;
                return;
            }

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
                MessageBox.Show(Properties.Resources.Flow_RunFlowFirst);
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

        private void ConfigureCreatedNode(STNode node)
        {
            if (node is CVBaseServerNode vBaseServerNode)
            {
                var matchedService = MqttRCService.GetInstance().ServiceTokens.FirstOrDefault(s => s.Devices.Any(d => d.Key == vBaseServerNode.DeviceCode));
                if (matchedService != null)
                {
                    vBaseServerNode.Token = matchedService.Token;
                }
            }
            else if (node is MQTTStartNode startNode)
            {
                startNode.Server = MQTTControl.Config.Host;
                startNode.Port = MQTTControl.Config.Port;
            }
        }

    }


}
