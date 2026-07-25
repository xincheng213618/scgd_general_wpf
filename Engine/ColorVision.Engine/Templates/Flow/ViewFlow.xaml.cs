#pragma warning disable CA1720,CA1822,CA1863,CS4014,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.Batch;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Diagnostics;
using System.IO;
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
        public RelayCommand OpenDocumentCommand { get; set; }
        public RelayCommand NewDocumentCommand { get; set; }

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
        public STNodeEditor STNodeEditorMain => EditorCanvas.NodeEditor;
        public FlowControl FlowControl => _isStandalone ? _standaloneFlowControl! : FlowEngineManager.FlowControl;
        public bool IsStandalone => _isStandalone;
        public Visibility RuntimeVisibility => _isStandalone ? Visibility.Collapsed : Visibility.Visible;
        public Visibility StandaloneVisibility => _isStandalone ? Visibility.Visible : Visibility.Collapsed;

        private readonly bool _isStandalone;
        private readonly FlowControl? _standaloneFlowControl;
        private readonly FlowNodeManager? _standaloneNodeManager;
        private readonly Stopwatch _standaloneStopwatch = new();
        private FlowParam? _standaloneFlowParam;
        private string? _standaloneFilePath;
        private string _standaloneDocumentName = string.Empty;
        private bool _saveStandaloneFlowParam;
        private CVCommonNode? _executionDetailsNode;
        private CVCommonNode? _standaloneLastNode;
        private CVCommonNode? _standaloneLastFailedNode;

        public ViewFlow(FlowEngineManager flowEngineManager) : this(flowEngineManager, false)
        {
        }

        internal ViewFlow(FlowEngineManager flowEngineManager, bool isStandalone)
        {
            _isStandalone = isStandalone;
            FlowEngineManager = flowEngineManager;
            if (isStandalone)
            {
                _standaloneNodeManager = new FlowNodeManager();
                FlowEngineControl = new FlowEngineControl(false, _standaloneNodeManager);
                _standaloneFlowControl = new FlowControl(MQTTControl.GetInstance(), FlowEngineControl);
            }
            else
            {
                FlowEngineControl = FlowEngineManager.FlowEngineControl;
            }
            Config = FlowEngineManager.Config;

            InitializeComponent();
            EditorCanvas.PropertyPanelMargin = new Thickness(0, 54, 10, 108);
            EditorCanvas.AttachEditCommandRouting(this);

            AutoSizeCommand = new RelayCommand(a => STNodeEditorHelper.AutoSize());
            OpenDocumentCommand = new RelayCommand(a => OpenDocument(), a => _isStandalone);
            NewDocumentCommand = new RelayCommand(a => NewDocument(), a => _isStandalone);
            RefreshCommand = new RelayCommand(a => Refresh());
            ClearCommand = new RelayCommand(a => Clear());
            SaveCommand = new RelayCommand(a => Save());
            AutoAlignmentCommand = new RelayCommand(a => AutoAlignment());
            OpenFlowTemplateCommand = new RelayCommand(a => OpenFlowTemplate());
            NewFlowCommand = new RelayCommand(a => NewFlow());
            DeleteFlowCommand = new RelayCommand(a => DeleteFlow(), a => !_isStandalone && FlowEngineManager.GetInstance().SlectFlowParam != null);
            ExportFlowCommand = new RelayCommand(a => ExportFlow(), a => !_isStandalone && FlowEngineManager.GetInstance().SlectFlowParam != null);
            ImportFlowCommand = new RelayCommand(a => ImportFlow(), a => !_isStandalone);
            ImportModuleCommand = new RelayCommand(a => ImportModule(), a => TemplateFlow.Params.Count > 0);

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => Save(), (s, e) => { e.CanExecute = STNodeEditorHelper != null; }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) =>
            {
                if (_isStandalone)
                    NewDocument();
                else
                    Clear();
            }, (s, e) => { e.CanExecute = true; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) =>
            {
                if (_isStandalone)
                {
                    Window? window = Window.GetWindow(this);
                    if (window != null)
                        window.Close();
                    else
                        Clear();
                }
                else
                {
                    Clear();
                }
            }, (s, e) => { e.CanExecute = true; }));

            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) =>
            {
                e.CanExecute = STNodeEditorMain.CanUndo;
                if (e.Parameter is MenuItem menuItem && menuItem.ItemsSource != STNodeEditorMain.UndoHistory)
                    menuItem.ItemsSource = STNodeEditorMain.UndoHistory;
            }));

        }

        public void OpenFlowTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            Refresh();
        }

        public async void NewFlow()
        {
            try
            {
                var templateFlow = new TemplateFlow();
                templateFlow.Load();
                string baseName = $"Flow_{DateTime.Now:yyyyMMdd_HHmmss}";
                string name = baseName;
                int suffix = 2;
                while (templateFlow.ExitsTemplateName(name))
                    name = $"{baseName}_{suffix++}";

                var existingIds = templateFlow.TemplateParams.Select(item => item.Id).ToHashSet();
                templateFlow.Create(name);
                TemplateModel<FlowParam>? createdFlow = templateFlow.TemplateParams
                    .LastOrDefault(item => !existingIds.Contains(item.Id));
                if (createdFlow == null)
                    return;

                if (DisplayFlow != null)
                    await DisplayFlow.SelectCreatedFlowTemplateAsync(createdFlow);
                else
                    Refresh();
            }
            catch (Exception ex)
            {
                log.Error("Create flow failed.", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
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
            STNodeEditorHelper.ApplyTreeLayout();
            STNodeEditorHelper.AutoSize();
        }

        public void Save()
        {
            TrySave();
        }

        internal bool TrySave()
        {
            if (_isStandalone)
            {
                return SaveStandaloneDocument();
            }

            log.Info("Save: 开始保存流程");

            Keyboard.ClearFocus();
            Focus();

            try
            {
                if (!STNodeEditorHelper.CheckFlow())
                {
                    log.Warn("Save: CheckFlow验证失败, 取消保存");
                    return false;
                }

                var flowParam = FlowEngineManager.GetInstance().SlectFlowParam;
                if (flowParam == null)
                {
                    log.Error("Save: SlectFlowParam 为 null, 无法保存");
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoFlowParamSelected);
                    return false;
                }

                byte[] canvasData = STNodeEditorMain.GetCanvasData();
                if (canvasData == null || canvasData.Length == 0)
                {
                    log.Error("Save: GetCanvasData 返回空数据");
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_GetCanvasDataFailed);
                    return false;
                }

                log.Info($"Save: 画布数据大小={canvasData.Length} bytes, FlowParam.Id={flowParam.Id}, Name={flowParam.Name}");
                flowParam.DataBase64 = Convert.ToBase64String(canvasData);
                TemplateFlow.Save2DB(flowParam);
                STNodeEditorMain.MarkSaved();
                log.Info("Save: 流程保存成功");
                return true;
            }
            catch (Exception ex)
            {
                log.Error("Save: 保存流程时发生异常", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.Flow_SaveFailed, ex.Message));
                return false;
            }
        }


        public void Refresh()
        {
            if (_isStandalone)
            {
                ReloadStandaloneDocument();
                return;
            }

            RefreshFlow?.Invoke(this, new EventArgs());
        }

        public void Clear()
        {
            if (_isStandalone)
            {
                if (!ConfirmStandaloneDocumentReplacement())
                    return;
                StopStandaloneFlow();
                FlowEngineControl.FlowClear();
                _standaloneNodeManager!.ClearDevice();
                STNodeEditorMain.ClearHistory();
            }
            else
            {
                STNodeEditorMain.Nodes.Clear();
            }
        }

        public void OpenStandaloneFile(string filePath)
        {
            TryOpenStandaloneFile(filePath);
        }

        private bool TryOpenStandaloneFile(string filePath)
        {
            if (!_isStandalone)
                throw new InvalidOperationException("Only a standalone ViewFlow can open a file document.");
            if (!ConfirmStandaloneDocumentReplacement())
                return false;

            _standaloneFlowParam = null;
            _standaloneFilePath = filePath;
            _standaloneDocumentName = Path.GetFileName(filePath);
            _saveStandaloneFlowParam = false;

            StopStandaloneFlow();
            FlowEngineControl.FlowClear();
            if (filePath.EndsWith(".cvflow", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var (stnData, _) = FlowPackageHelper.ImportFlowPackage(filePath);
                    if (stnData != null && stnData.Length > 0)
                    {
                        _standaloneFlowParam = new FlowParam
                        {
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            DataBase64 = Convert.ToBase64String(stnData)
                        };
                        _standaloneFilePath = null;
                        FlowEngineControl.LoadFromBase64(
                            _standaloneFlowParam.DataBase64,
                            MqttRCService.GetInstance().ServiceTokens);
                        STNodeEditorMain.ClearHistory();
                        UpdateStandaloneWindowTitle(Path.GetFileName(filePath));
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    log.Debug("Open cvflow package as legacy canvas file.", ex);
                }
            }

            FlowEngineControl.LoadFromFile(filePath, MqttRCService.GetInstance().ServiceTokens);
            STNodeEditorMain.ClearHistory();
            UpdateStandaloneWindowTitle(Path.GetFileName(filePath));
            return true;
        }

        public void OpenStandaloneFlowParam(FlowParam flowParam, bool saveToTemplate)
        {
            TryOpenStandaloneFlowParam(flowParam, saveToTemplate);
        }

        private bool TryOpenStandaloneFlowParam(FlowParam flowParam, bool saveToTemplate)
        {
            if (!_isStandalone)
                throw new InvalidOperationException("Only a standalone ViewFlow can open a template document.");
            if (!ConfirmStandaloneDocumentReplacement())
                return false;

            _standaloneFlowParam = flowParam;
            _standaloneFilePath = null;
            _standaloneDocumentName = flowParam.Name;
            _saveStandaloneFlowParam = saveToTemplate;

            StopStandaloneFlow();
            FlowEngineControl.FlowClear();
            try
            {
                FlowEngineControl.LoadFromBase64(
                    flowParam.DataBase64,
                    MqttRCService.GetInstance().ServiceTokens);
                STNodeEditorMain.ClearHistory();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
                return false;
            }
            UpdateStandaloneWindowTitle(flowParam.Name);
            return true;
        }

        public bool HasStandaloneChanges()
        {
            if (!_isStandalone)
                return false;

            return STNodeEditorMain.IsModified;
        }

        private void OpenDocument()
        {
            using System.Windows.Forms.OpenFileDialog dialog = new()
            {
                Filter = "Flow files (*.stn;*.cvflow)|*.stn;*.cvflow|STN files (*.stn)|*.stn|CVFlow files (*.cvflow)|*.cvflow",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TryOpenStandaloneFile(dialog.FileName);
        }

        private void NewDocument()
        {
            if (!ConfirmStandaloneDocumentReplacement())
                return;
            _standaloneFlowParam = null;
            _standaloneFilePath = null;
            _standaloneDocumentName = Properties.Resources.New;
            _saveStandaloneFlowParam = false;
            StopStandaloneFlow();
            FlowEngineControl.FlowClear();
            _standaloneNodeManager!.ClearDevice();
            STNodeEditorMain.ClearHistory();
            UpdateStandaloneWindowTitle(Properties.Resources.New);
        }

        private void ReloadStandaloneDocument()
        {
            if (_standaloneFlowParam != null)
            {
                TryOpenStandaloneFlowParam(_standaloneFlowParam, _saveStandaloneFlowParam);
            }
            else if (!string.IsNullOrEmpty(_standaloneFilePath))
            {
                TryOpenStandaloneFile(_standaloneFilePath);
            }
        }

        private bool SaveStandaloneDocument()
        {
            Keyboard.ClearFocus();
            Focus();
            if (!STNodeEditorHelper.CheckFlow())
                return false;

            byte[] canvasData = STNodeEditorMain.GetCanvasData();
            if (canvasData == null || canvasData.Length == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_GetCanvasDataFailed);
                return false;
            }

            try
            {
                if (_saveStandaloneFlowParam && _standaloneFlowParam != null)
                {
                    _standaloneFlowParam.DataBase64 = Convert.ToBase64String(canvasData);
                    TemplateFlow.Save2DB(_standaloneFlowParam);
                }
                else
                {
                    if (string.IsNullOrEmpty(_standaloneFilePath))
                    {
                        using System.Windows.Forms.SaveFileDialog dialog = new()
                        {
                            Filter = "STN files (*.stn)|*.stn",
                            DefaultExt = "stn",
                            AddExtension = true,
                            Title = Properties.Resources.Save
                        };
                        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                            return false;
                        _standaloneFilePath = dialog.FileName;
                        _standaloneFlowParam = null;
                        _standaloneDocumentName = Path.GetFileName(dialog.FileName);
                    }

                    File.WriteAllBytes(_standaloneFilePath, canvasData);
                    UpdateStandaloneWindowTitle(Path.GetFileName(_standaloneFilePath));
                }

                STNodeEditorMain.MarkSaved();
                MessageBox.Show(Properties.Resources.SaveSucess);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("Save standalone flow failed.", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.Flow_SaveFailed, ex.Message));
                return false;
            }
        }

        internal bool ConfirmStandaloneDocumentReplacement()
        {
            if (!_isStandalone || !HasStandaloneChanges())
                return true;
            if (!ShouldConfirmStandaloneDocumentReplacement(
                    isStandalone: true,
                    hasChanges: true,
                    editSavePromptEnabled: FlowEngineConfig.Instance.IsAutoEditSave))
                return true;

            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                Properties.Resources.SaveChangesPrompt,
                "ColorVision",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            return result switch
            {
                MessageBoxResult.Yes => TrySave(),
                MessageBoxResult.No => true,
                _ => false
            };
        }

        internal static bool ShouldConfirmStandaloneDocumentReplacement(
            bool isStandalone,
            bool hasChanges,
            bool editSavePromptEnabled)
        {
            return isStandalone && hasChanges && editSavePromptEnabled;
        }

        private void UpdateStandaloneWindowTitle(string? documentName)
        {
            Window? window = Window.GetWindow(this);
            if (window != null)
            {
                window.Title = string.IsNullOrWhiteSpace(documentName)
                    ? Properties.Resources.FlowEditor
                    : $"{Properties.Resources.FlowEditor} - {documentName}";
            }
        }

        public STNodeEditorHelper STNodeEditorHelper { get; set; }

        
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;            
            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    AutoAlignment();
                    e.Handled = true;
                }
            };

            STNodeEditorHelper = new STNodeEditorHelper(STNodeEditorMain);

            // Keep node properties in the ViewFlow right-side overlay instead of AvalonDock.
            STNodeEditorHelper.UseDockPanel = false;
            STNodeEditorHelper.SignStackPanel = EditorCanvas.NodePropertyPanel.SignStackPanel;
            STNodeEditorHelper.PropertyEditorPanel = EditorCanvas.NodePropertyPanelContainer;

            FlowEngineControl.AttachNodeEditor(STNodeEditorMain);

            if (!_isStandalone)
            {
                var manager = DockViewManager.GetInstance();
                manager.AddView(0, this);
                manager.ViewTitles[this] = ColorVision.Engine.Properties.Resources.Workflow;

                this.Loaded += (s, e) =>
                {
                    DockViewManager.GetInstance().ActiveViewChanged += OnActiveViewChanged;
                    FlowEngineManager.PublishCopilotContext();
                };
                this.Unloaded += (s, e) =>
                {
                    DockViewManager.GetInstance().ActiveViewChanged -= OnActiveViewChanged;
                    CopilotLiveContextRegistry.Clear(CopilotFlowAgentExtension.SourceId);
                };
            }
            else
            {
                MqttRCService.GetInstance().ServiceTokensUpdated += MqttRCService_ServiceTokensUpdated;
            }
        }

        private void MqttRCService_ServiceTokensUpdated(object? sender, EventArgs e)
        {
            void UpdateTokens() => _standaloneNodeManager?.UpdateDevice(MqttRCService.GetInstance().ServiceTokens);

            if (Dispatcher.CheckAccess())
                UpdateTokens();
            else
                Dispatcher.BeginInvoke(UpdateTokens);
        }

        private void OnActiveViewChanged(System.Windows.Controls.Control? activeView)
        {
            if (activeView == this)
            {
                FlowEngineManager.PublishCopilotContext();
                STNodeEditorHelper?.RefreshActiveNodePropertyPanel();
                return;
            }

            // Only hide when another registered view becomes active.
            // If activeView is null it means focus moved to a non-view control.
            if (activeView != null && activeView != this)
            {
                CopilotLiveContextRegistry.Clear(CopilotFlowAgentExtension.SourceId);
                STNodeEditorHelper?.HidePropertyEditor();
            }
        }

        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (STNodeEditorMain.ActiveNode == null && STNodeEditorMain.GetSelectedNode().Length == 0)
            {
                if (e.Key == Key.Add)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, STNodeEditorMain.ClientSize.Width / 2f, STNodeEditorMain.ClientSize.Height / 2f);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, STNodeEditorMain.ClientSize.Width / 2f, STNodeEditorMain.ClientSize.Height / 2f);
                    e.Handled = true;
                }
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
            if (_isStandalone)
            {
                StopStandaloneFlow();
                DetachStandaloneExecutionNodeTracking();
                if (_standaloneFlowControl != null)
                    _standaloneFlowControl.FlowCompleted -= StandaloneFlowControl_FlowCompleted;
                MqttRCService.GetInstance().ServiceTokensUpdated -= MqttRCService_ServiceTokensUpdated;
                _standaloneNodeManager?.ClearDevice();
                FlowEngineControl.Dispose();
            }
            else
            {
                if (FlowEngineManager.FlowControl?.IsFlowRun == true)
                    FlowEngineManager.FlowControl.Stop();
                FlowEngineControl.DetachNodeEditor(STNodeEditorMain);
            }
            STNodeEditorHelper?.Dispose();
            EditorCanvas.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isStandalone)
            {
                RunStandaloneFlow();
            }
            else
            {
                FlowEngineManager.DisplayFlow.RunFlow();
            }

        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isStandalone)
            {
                StopStandaloneFlow(true);
            }
            else
            {
                FlowEngineManager.DisplayFlow.StopFlow();
            }

        }

        private void RunStandaloneFlow()
        {
            if (!MqttRCService.GetInstance().IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.RegistryCenterNotConnected);
                return;
            }

            if (FlowControl.IsFlowRun)
                return;

            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                ShowExecutionSummary(Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                return;
            }

            _standaloneNodeManager!.UpdateDevice(MqttRCService.GetInstance().ServiceTokens);

            string startNode = FlowEngineControl.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.WorkflowStartNodeNotFound_RunFailed, "ColorVision");
                return;
            }

            foreach (STNode node in STNodeEditorMain.Nodes)
            {
                foreach (STNodeOption option in node.GetAllInputOptions())
                    option.Data = null;
                foreach (STNodeOption option in node.GetAllOutputOptions())
                    option.Data = null;

                if (node is CVBaseServerNode serverNode)
                    serverNode.TitleColor = System.Drawing.Color.Blue;
            }

            ResetStandaloneExecutionNodeTracking();
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                node.nodeRunEvent -= StandaloneNodeRunEvent;
                node.nodeRunEvent += StandaloneNodeRunEvent;
                node.nodeEndEvent -= StandaloneNodeEndEvent;
                node.nodeEndEvent += StandaloneNodeEndEvent;
            }

            string serialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            _standaloneFlowControl!.FlowCompleted -= StandaloneFlowControl_FlowCompleted;
            _standaloneFlowControl.FlowCompleted += StandaloneFlowControl_FlowCompleted;
            _standaloneStopwatch.Restart();
            ShowExecutionSummary($"Run {_standaloneDocumentName}");

            try
            {
                _standaloneFlowControl.Start(serialNumber);
            }
            catch (Exception ex)
            {
                _standaloneStopwatch.Stop();
                _standaloneFlowControl.FlowCompleted -= StandaloneFlowControl_FlowCompleted;
                _standaloneFlowControl.Stop();
                DetachStandaloneExecutionNodeTracking();
                log.Error("Run standalone flow failed.", ex);
                ShowExecutionSummary(ex.Message);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
        }

        private void StopStandaloneFlow(bool updateLog = false)
        {
            if (_standaloneFlowControl?.IsFlowRun != true)
                return;

            _standaloneFlowControl.FlowCompleted -= StandaloneFlowControl_FlowCompleted;
            _standaloneFlowControl.Stop();
            _standaloneStopwatch.Stop();
            DetachStandaloneExecutionNodeTracking();
            if (updateLog)
                ShowExecutionSummary(Properties.Resources.ExecutionCancelled);
        }

        private void StandaloneFlowControl_FlowCompleted(object? sender, FlowControlData data)
        {
            _standaloneStopwatch.Stop();
            _standaloneFlowControl!.FlowCompleted -= StandaloneFlowControl_FlowCompleted;
            DetachStandaloneExecutionNodeTracking();
            string errorNode = string.IsNullOrWhiteSpace(data.ErrorNodeName)
                ? string.Empty
                : $"{Environment.NewLine}{Properties.Resources.Flow_NodeLabel}{data.ErrorNodeName}";
            string message =
                $"{_standaloneDocumentName} {data.EventName}{errorNode}{Environment.NewLine}{data.Params}{Environment.NewLine}{_standaloneStopwatch.ElapsedMilliseconds}ms";
            ShowExecutionSummary(message, data.ErrorNodeName, _standaloneLastFailedNode ?? _standaloneLastNode);
        }

        private void StandaloneNodeRunEvent(object sender, FlowEngineNodeRunEventArgs e)
        {
            _standaloneLastNode = sender as CVCommonNode;
        }

        private void StandaloneNodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
        {
            if (sender is CVCommonNode node && e?.RecvStatusCode != 0)
                _standaloneLastFailedNode = node;
        }

        private void ResetStandaloneExecutionNodeTracking()
        {
            DetachStandaloneExecutionNodeTracking();
            _standaloneLastNode = null;
            _standaloneLastFailedNode = null;
            ShowExecutionSummary(string.Empty);
        }

        private void DetachStandaloneExecutionNodeTracking()
        {
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                node.nodeRunEvent -= StandaloneNodeRunEvent;
                node.nodeEndEvent -= StandaloneNodeEndEvent;
            }
        }

        public void ShowExecutionSummary(string message, string? executionNodeName = null, CVCommonNode? preferredNode = null)
        {
            logTextBox.Text = message;
            _executionDetailsNode = STNodeEditorHelper.ResolveExecutionNode(
                STNodeEditorMain,
                executionNodeName,
                preferredNode);
            ErrorNodeDetailsButton.Visibility = _executionDetailsNode == null
                ? Visibility.Collapsed
                : Visibility.Visible;
            ErrorNodeDetailsButton.ToolTip = _executionDetailsNode == null
                ? null
                : $"{Properties.Resources.Flow_NodeLabel}{_executionDetailsNode.OnGetDrawTitle()}";
        }

        private void ErrorNodeDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_executionDetailsNode != null)
                STNodeEditorHelper.OpenNodeExecutionDetails(_executionDetailsNode, focusNode: true);
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
