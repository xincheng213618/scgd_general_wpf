#pragma warning disable CA1720,CA1822,CA1863,CS4014,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing
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
        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand RunFlowCommand { get; set; }
        public RelayCommand StopFlowCommand { get; set; }

        public RelayCommand ClearCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand VersionHistoryCommand { get; set; }
        public RelayCommand IncidentManagementCommand { get; set; }

        public RelayCommand AutoAlignmentCommand { get; set; }

        public RelayCommand OpenFlowTemplateCommand { get; set; }

        public RelayCommand NewFlowCommand { get; set; }
        public RelayCommand DeleteFlowCommand { get; set; }
        public RelayCommand ExportFlowCommand { get; set; }
        public RelayCommand ImportFlowCommand { get; set; }
        public RelayCommand ImportModuleCommand { get; set; }


        public STNodeEditor STNodeEditorMain => EditorCanvas.NodeEditor;
        public FlowControl FlowControl => _isStandalone ? _standaloneFlowControl! : FlowEngineManager.FlowControl;
        public CVCommonNode? LastNode => _executionSession.LastNode;
        public bool IsStandalone => _isStandalone;
        public bool IsExecutionActive =>
            FlowControl.IsFlowRun || _executionSession.IsRunActive;
        public bool IsCurrentBatchActive =>
            _executionSession.IsCurrentBatchActive;

        /// <summary>
        /// The graph engine has stopped. Post-processing may still be running.
        /// </summary>
        public event EventHandler<FlowControlData>? EngineExecutionCompleted
        {
            add => _executionSession.EngineExecutionCompleted += value;
            remove => _executionSession.EngineExecutionCompleted -= value;
        }

        /// <summary>
        /// Compatibility alias for <see cref="EngineExecutionCompleted"/>.
        /// This event does not represent the final business outcome.
        /// </summary>
        [Obsolete(
            "FlowExecutionCompleted reports graph-engine completion only. "
            + "Use EngineExecutionCompleted for that phase, or RunFinalized "
            + "for the final business outcome.")]
        public event EventHandler<FlowControlData>? FlowExecutionCompleted
        {
            add => _executionSession.EngineExecutionCompleted += value;
            remove => _executionSession.EngineExecutionCompleted -= value;
        }

        /// <summary>
        /// The graph engine and all post-processing have reached a final
        /// business outcome.
        /// </summary>
        public event EventHandler<FlowRunFinalizedData>? RunFinalized
        {
            add => _executionSession.RunFinalized += value;
            remove => _executionSession.RunFinalized -= value;
        }

        private readonly bool _isStandalone;
        private readonly FlowControl? _standaloneFlowControl;
        private readonly FlowNodeManager? _standaloneNodeManager;
        private readonly FlowNodeContextMenuService _nodeContextMenuService;
        private readonly FlowExecutionNavigator _executionNavigator;
        private readonly FlowGraphLayoutService _layoutService;
        private readonly FlowExecutionSession _executionSession;
        private FlowParam? _standaloneFlowParam;
        private string? _standaloneFilePath;
        private string _standaloneDocumentName = string.Empty;
        private bool _saveStandaloneFlowParam;
        private CVCommonNode? _executionDetailsNode;
        private string? _standaloneStartNodeName;
        private bool _runtimeSelectionInitialized;
        private string? _documentLoadedContentHash;

        public bool TryFocusNode(Guid nodeGuid)
        {
            return _executionNavigator.TryFocusNode(nodeGuid);
        }

        internal void SetDocumentBaseline(FlowParam? flowParam)
        {
            if (flowParam == null
                || string.IsNullOrWhiteSpace(flowParam.DataBase64))
            {
                _documentLoadedContentHash = null;
                return;
            }
            try
            {
                _documentLoadedContentHash =
                    FlowSemanticHash.ComputeBinaryHash(
                        Convert.FromBase64String(
                            flowParam.DataBase64));
            }
            catch (FormatException)
            {
                _documentLoadedContentHash = null;
            }
        }

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
            _executionNavigator = new FlowExecutionNavigator(STNodeEditorMain);
            _nodeContextMenuService = new FlowNodeContextMenuService(STNodeEditorMain, _executionNavigator);
            _layoutService = new FlowGraphLayoutService(STNodeEditorMain);
            _executionSession = new FlowExecutionSession(flowEngineManager, this);

            AutoSizeCommand = new RelayCommand(a => _layoutService.FitToViewport());
            RefreshCommand = new RelayCommand(a => Refresh());
            RunFlowCommand = new RelayCommand(a => RunFlow());
            StopFlowCommand = new RelayCommand(a => StopFlow());
            ClearCommand = new RelayCommand(a => Clear());
            SaveCommand = new RelayCommand(a => Save());
            VersionHistoryCommand = new RelayCommand(
                _ => ShowVersionHistory(),
                _ => GetActiveFlowParam()?.FlowKey != null
                    && !IsExecutionActive);
            IncidentManagementCommand =
                new RelayCommand(_ => ShowIncidentManagement());
            AutoAlignmentCommand = new RelayCommand(a => AutoAlignment());
            OpenFlowTemplateCommand = new RelayCommand(a => OpenFlowTemplate());
            NewFlowCommand =
                new RelayCommand(_ => ExecuteNewFlowCommand());
            DeleteFlowCommand = new RelayCommand(
                a => DeleteFlow(),
                a => !_isStandalone && GetActiveFlowParam() != null);
            ExportFlowCommand = new RelayCommand(
                a =>
                {
                    if (_isStandalone)
                        SaveStandaloneDocument();
                    else
                        ExportFlow();
                },
                a => _isStandalone
                    ? STNodeEditorMain.Nodes.Count > 0
                    : GetActiveFlowParam() != null);
            ImportFlowCommand = new RelayCommand(a =>
            {
                if (_isStandalone)
                    OpenDocument();
                else
                    ImportFlow();
            });
            ImportModuleCommand = new RelayCommand(a => ImportModule(), a => TemplateFlow.Params.Count > 0);

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => Save(), (s, e) => { e.CanExecute = true; }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) =>
            {
                NewFlowCommand.Execute(null);
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
                if (!ConfirmDocumentReplacement())
                    return;

                var templateFlow = new TemplateFlow();
                templateFlow.Load();
                int oldCount = templateFlow.Count;
                templateFlow.OpenCreate();
                if (templateFlow.Count == oldCount)
                    return;

                TemplateModel<FlowParam> createdFlow = templateFlow.TemplateParams[^1];
                await SelectFlowTemplateAsync(createdFlow, allowEmptyFlow: true);
            }
            catch (Exception ex)
            {
                log.Error("Create flow failed.", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
        }

        private void ExecuteNewFlowCommand()
        {
            FlowNewCommandRouter.Execute(
                _isStandalone,
                NewDocument,
                NewFlow);
        }

        public void DeleteFlow()
        {
            var flowParam = GetActiveFlowParam();
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
            var flowParam = GetActiveFlowParam();
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
                    FlowEditorOperations.ImportCanvasAsModule(STNodeEditorMain, canvasData);
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
            _layoutService.Apply();
            _layoutService.FitToViewport();
        }

        internal void FitLoadedFlowToViewport()
        {
            EditorCanvas.FitCanvasToNodesAfterLayout();
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
                if (!FlowValidator.Validate(STNodeEditorMain))
                {
                    log.Warn("Save: CheckFlow验证失败, 取消保存");
                    return false;
                }

                var flowParam = GetActiveFlowParam();
                if (flowParam == null)
                {
                    log.Error("Save: SelectedFlowParam 为 null, 无法保存");
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
                string previousData = flowParam.DataBase64;
                try
                {
                    flowParam.DataBase64 =
                        Convert.ToBase64String(canvasData);
                    TemplateFlow.Save2DB(
                        flowParam,
                        new FlowTemplateSaveCondition(
                            _documentLoadedContentHash));
                }
                catch
                {
                    flowParam.DataBase64 = previousData;
                    throw;
                }
                _documentLoadedContentHash =
                    flowParam.LoadedContentHash;
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

            _ = RefreshRuntimeAsync();
        }

        private FlowParam? GetActiveFlowParam()
        {
            return _isStandalone
                ? _standaloneFlowParam
                : _executionSession.ActiveFlowParam;
        }

        private void ShowVersionHistory()
        {
            if (IsExecutionActive)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "流程正在运行，请在运行结束后查看或恢复版本。",
                    "流程版本");
                return;
            }

            FlowParam? flowParam = GetActiveFlowParam();
            if (flowParam == null
                || string.IsNullOrWhiteSpace(flowParam.FlowKey))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "当前文档还没有可用的流程版本。",
                    "流程版本");
                return;
            }

            new FlowVersionHistoryWindow(
                flowParam,
                Refresh,
                _documentLoadedContentHash,
                () => IsExecutionActive)
            {
                Owner = Application.Current.GetActiveWindow()
            }.Show();
        }

        private void ShowIncidentManagement()
        {
            bool FocusIncidentNode(FlowIncidentDetail detail)
            {
                FlowParam? activeFlow = GetActiveFlowParam();
                FlowRunRecord? incidentRun = detail.Run;
                if (activeFlow == null || incidentRun == null)
                    return false;

                bool isSameFlow =
                    !string.IsNullOrWhiteSpace(activeFlow.FlowKey)
                    && string.Equals(
                        activeFlow.FlowKey,
                        incidentRun.FlowKey,
                        StringComparison.Ordinal);
                if (!isSameFlow
                    && (activeFlow.Id <= 0
                        || activeFlow.Id != incidentRun.TemplateId))
                {
                    return false;
                }

                DockViewManager.GetInstance().ActiveView(this);
                return _executionNavigator.TryFocusExecutionNode(
                    detail.Incident.NodeId);
            }

            new FlowIncidentManagementWindow(FocusIncidentNode)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.Show();
        }

        internal Task RefreshRuntimeAsync()
        {
            return _executionSession.RefreshAsync();
        }

        internal Task SelectFlowTemplateAsync(
            TemplateModel<FlowParam> flowTemplate,
            bool allowEmptyFlow = false)
        {
            return _executionSession.SelectFlowTemplateAsync(flowTemplate, allowEmptyFlow);
        }

        internal async Task<FlowControlData?> RunFlowAndWaitAsync(
            TemplateModel<FlowParam>? flowTemplate = null)
        {
            if (flowTemplate != null)
                await _executionSession.SelectFlowTemplateAsync(flowTemplate);
            return await _executionSession.RunFlowAndWaitAsync();
        }

        internal async Task<FlowRunFinalizedData?> RunFlowAndWaitForFinalizationAsync(
            TemplateModel<FlowParam>? flowTemplate = null)
        {
            if (flowTemplate != null)
                await _executionSession.SelectFlowTemplateAsync(flowTemplate);
            return await _executionSession.RunFlowAndWaitForFinalizationAsync();
        }

        public void Clear()
        {
            if (_isStandalone)
            {
                if (!ConfirmDocumentReplacement())
                    return;
                StopStandaloneFlow();
                ShowExecutionSummary(string.Empty);
                FlowEngineControl.FlowClear();
                RefreshStandaloneStartNodeSelection();
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
            if (!ConfirmDocumentReplacement())
                return false;

            _standaloneFlowParam = null;
            _documentLoadedContentHash = null;
            _standaloneFilePath = filePath;
            _standaloneDocumentName = Path.GetFileName(filePath);
            _saveStandaloneFlowParam = false;

            StopStandaloneFlow();
            ShowExecutionSummary(string.Empty);
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
                        SetDocumentBaseline(
                            _standaloneFlowParam);
                        FlowEngineControl.LoadFromBase64(
                            _standaloneFlowParam.DataBase64,
                            MqttRCService.GetInstance().ServiceTokens);
                        FitLoadedFlowToViewport();
                        RefreshStandaloneStartNodeSelection();
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

            _documentLoadedContentHash = null;
            FlowEngineControl.LoadFromFile(
                filePath,
                MqttRCService.GetInstance().ServiceTokens);
            FitLoadedFlowToViewport();
            RefreshStandaloneStartNodeSelection();
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
            if (!ConfirmDocumentReplacement())
                return false;

            _standaloneFlowParam = flowParam;
            _standaloneFilePath = null;
            _standaloneDocumentName = flowParam.Name;
            _saveStandaloneFlowParam = saveToTemplate;

            StopStandaloneFlow();
            ShowExecutionSummary(string.Empty);
            FlowEngineControl.FlowClear();
            try
            {
                FlowEngineControl.LoadFromBase64(
                    flowParam.DataBase64,
                    MqttRCService.GetInstance().ServiceTokens);
                SetDocumentBaseline(flowParam);
                FitLoadedFlowToViewport();
                RefreshStandaloneStartNodeSelection();
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

        internal TemplateModel<FlowParam>? GetStandaloneExecutionTemplate()
        {
            if (!_isStandalone || STNodeEditorMain.Nodes.Count == 0)
                return null;
            if (_standaloneFlowParam != null)
                return new TemplateModel<FlowParam>(_standaloneFlowParam.Name, _standaloneFlowParam);

            string name = string.IsNullOrWhiteSpace(_standaloneDocumentName)
                ? Properties.Resources.New
                : _standaloneDocumentName;
            return new TemplateModel<FlowParam>(name, new FlowParam { Name = name });
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
            if (!ConfirmDocumentReplacement())
                return;
            _standaloneFlowParam = null;
            _documentLoadedContentHash = null;
            _standaloneFilePath = null;
            _standaloneDocumentName = Properties.Resources.New;
            _saveStandaloneFlowParam = false;
            StopStandaloneFlow();
            ShowExecutionSummary(string.Empty);
            FlowEngineControl.FlowClear();
            RefreshStandaloneStartNodeSelection();
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
            if (!FlowValidator.Validate(STNodeEditorMain))
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
                    string previousData =
                        _standaloneFlowParam.DataBase64;
                    try
                    {
                        _standaloneFlowParam.DataBase64 =
                            Convert.ToBase64String(canvasData);
                        TemplateFlow.Save2DB(
                            _standaloneFlowParam,
                            new FlowTemplateSaveCondition(
                                _documentLoadedContentHash));
                    }
                    catch
                    {
                        _standaloneFlowParam.DataBase64 =
                            previousData;
                        throw;
                    }
                    _documentLoadedContentHash =
                        _standaloneFlowParam.LoadedContentHash;
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

        internal bool ConfirmDocumentReplacement()
        {
            return FlowDocumentReplacementGuard.Confirm(
                STNodeEditorMain.IsModified,
                () => MessageBox.Show(
                    Window.GetWindow(this)
                        ?? Application.Current.GetActiveWindow(),
                    Properties.Resources.SaveChangesPrompt,
                    "ColorVision",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question),
                TrySave);
        }

        internal bool ConfirmStandaloneDocumentReplacement()
        {
            return ConfirmDocumentReplacement();
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

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = this;
            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    AutoAlignment();
                    e.Handled = true;
                }
            };

            FlowEngineControl.AttachNodeEditor(STNodeEditorMain);

            if (!_isStandalone)
            {
                var manager = DockViewManager.GetInstance();
                manager.AddView(0, this);
                manager.ViewTitles[this] = ColorVision.Engine.Properties.Resources.Workflow;

                this.Loaded += (s, e) =>
                {
                    InitializeRuntimeSelection();
                    DockViewManager.GetInstance().ActiveViewChanged += OnActiveViewChanged;
                    FlowEngineManager.Copilot.PublishContext();
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

        private void InitializeRuntimeSelection()
        {
            if (_runtimeSelectionInitialized)
                return;

            _runtimeSelectionInitialized = true;
            _executionSession.InitializeSelection();
        }

        internal void SelectRuntimeFlowTemplate(TemplateModel<FlowParam>? flowTemplate)
        {
            if (flowTemplate == null
                || _executionSession.RequestedTemplateId == flowTemplate.Id)
                return;

            _runtimeSelectionInitialized = true;
            _executionSession.OnFlowSelectionChanged(flowTemplate);
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
                FlowEngineManager.Copilot.PublishContext();
                EditorCanvas.RefreshNodePropertyPanel();
                return;
            }

            // Only hide when another registered view becomes active.
            // If activeView is null it means focus moved to a non-view control.
            if (activeView != null && activeView != this)
            {
                CopilotLiveContextRegistry.Clear(CopilotFlowAgentExtension.SourceId);
                EditorCanvas.HideNodePropertyPanel();
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
            PostProcessManager.GetInstance().Edit();
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
            _executionSession.Dispose();
            _nodeContextMenuService.Dispose();
            EditorCanvas.Dispose();
            GC.SuppressFinalize(this);
        }

        private async void RunFlow()
        {
            await ExecuteRunCommandAsync(
                async () =>
                {
                    if (_isStandalone)
                    {
                        _executionSession.SelectStartNode(
                            RefreshStandaloneStartNodeSelection());
                    }
                    await _executionSession.RunFlowAsync();
                },
                ReportRunFlowFailure);
        }

        internal static async Task ExecuteRunCommandAsync(
            Func<Task> runAsync,
            Action<Exception> reportFailure)
        {
            try
            {
                await runAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    reportFailure(ex);
                }
                catch (Exception reportException)
                {
                    log.Error("报告流程执行失败时发生异常。", reportException);
                }
            }
        }

        private void ReportRunFlowFailure(Exception ex)
        {
            string message = $"流程执行失败：{ex.Message}";
            log.Error("流程命令执行失败。", ex);
            ShowExecutionSummary(message);
            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                message,
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void StopFlow()
        {
            _executionSession.StopFlow();
        }

        private string? RefreshStandaloneStartNodeSelection()
        {
            string[] startNodeNames = FlowEngineControl.GetStartNodeNames();
            if (string.IsNullOrWhiteSpace(_standaloneStartNodeName)
                || !startNodeNames.Contains(_standaloneStartNodeName))
                _standaloneStartNodeName = startNodeNames.FirstOrDefault();
            return _standaloneStartNodeName;
        }

        private void StopStandaloneFlow(bool updateLog = false)
        {
            _executionSession.StopFlow(updateLog);
            _executionSession.DetachNodeEvents();
        }

        public void ShowExecutionSummary(string message, string? executionNodeName = null, CVCommonNode? preferredNode = null)
        {
            logTextBox.Text = message;
            _executionDetailsNode = _isStandalone
                ? null
                : FlowExecutionNavigator.ResolveExecutionNode(
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
                _executionNavigator.OpenNodeExecutionDetails(_executionDetailsNode, focusNode: true);
        }

        private void Button_Click_NodeAnalysis(object sender, RoutedEventArgs e)
        {
            bool FocusFlowNode(FlowNodeRecord record)
            {
                DockViewManager.GetInstance().ActiveView(this);
                return _executionNavigator.TryFocusExecutionNode(record.NodeId, record.NodeName);
            }

            FlowParam? selectedFlow = _isStandalone
                ? GetActiveFlowParam()
                : _executionSession.RequestedFlowParam;

            FlowExecutionAnalysisWindow window;
            if (selectedFlow != null)
            {
                window = new FlowExecutionAnalysisWindow(
                    new FlowIdentity(
                        selectedFlow.Id,
                        selectedFlow.FlowKey,
                        selectedFlow.Name),
                    FocusFlowNode);
            }
            else if (FlowEngineManager.Batch != null)
                window = new FlowExecutionAnalysisWindow(FlowEngineManager.Batch, FocusFlowNode);
            else
                window = new FlowExecutionAnalysisWindow(FocusFlowNode);

            window.Owner = Application.Current.GetActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
        }

    }

    internal static class FlowNewCommandRouter
    {
        public static void Execute(
            bool isStandalone,
            Action newDocument,
            Action newFlow)
        {
            ArgumentNullException.ThrowIfNull(newDocument);
            ArgumentNullException.ThrowIfNull(newFlow);
            if (isStandalone)
                newDocument();
            else
                newFlow();
        }
    }

    internal static class FlowDocumentReplacementGuard
    {
        public static bool Confirm(
            bool isModified,
            Func<MessageBoxResult> prompt,
            Func<bool> save)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(save);
            if (!isModified)
                return true;

            return prompt() switch
            {
                MessageBoxResult.Yes => save(),
                MessageBoxResult.No => true,
                _ => false,
            };
        }
    }
}
