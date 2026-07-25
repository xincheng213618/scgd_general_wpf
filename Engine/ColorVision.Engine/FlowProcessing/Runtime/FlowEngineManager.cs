#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Logging;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowEngineManager : ViewModelBase
{
    private static readonly object SyncRoot = new();
    private static FlowEngineManager? _instance;
    private MeasureBatchModel? _batch;
    private FlowParam? _selectedFlowParam;
    private double _batchProgress;

    public static FlowEngineManager? Current
    {
        get
        {
            lock (SyncRoot)
                return _instance;
        }
    }

    public static FlowEngineManager GetInstance()
    {
        lock (SyncRoot)
            return _instance ??= new FlowEngineManager();
    }

    public static FlowEngineConfig Config => FlowEngineConfig.Instance;

    public FlowControl FlowControl { get; }
    public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; } = TemplateFlow.Params;
    public ContextMenu ContextMenu { get; }
    public RelayCommand EditTemplateFlowCommand { get; }
    public RelayCommand MeasureBatchManagerCommand { get; }
    public RelayCommand OpenServiceCommand { get; }
    public RelayCommand OpenCameraLogCommand { get; }
    public RelayCommand AskCopilotFlowCommand { get; }
    public ViewFlow View { get; }
    public FlowEngineControl FlowEngineControl { get; }
    public ServiceConfig ServiceConfig { get; }
    public ObservableCollection<CVBaseServerNode> CVBaseServerNodes { get; } = [];
    public WindowsServiceBase WindowsServiceX64 { get; }
    public WindowsServiceBase WindowsServiceDev { get; }
    public WindowsServiceBase WindowsServiceReg { get; }
    public FlowCopilotService Copilot { get; }
    public DisplayFlow DisplayFlow { get; }

    public int TemplateFlowParamsIndex
    {
        get => Config.TemplateFlowParamsIndex;
        set
        {
            Config.TemplateFlowParamsIndex = value;
            OnPropertyChanged();
        }
    }

    public MeasureBatchModel? Batch
    {
        get => _batch;
        set
        {
            _batch = value;
            OnPropertyChanged();
            if (value != null)
                BatchRecord?.Invoke(this, value);
        }
    }

    public event EventHandler<MeasureBatchModel>? BatchRecord;

    public FlowParam? SelectedFlowParam
    {
        get => _selectedFlowParam;
        set
        {
            _selectedFlowParam = value;
            OnPropertyChanged();
        }
    }

    public double BatchProgress
    {
        get => _batchProgress;
        set
        {
            _batchProgress = value;
            OnPropertyChanged();
        }
    }

    public Version ServiceVersion =>
        Version.TryParse(ServiceConfig.RegistrationCenterServiceInfo.FileVersion, out Version? version)
            ? version
            : new Version();

    private FlowEngineManager()
    {
        FlowEngineControl = new FlowEngineControl(false);
        View = new ViewFlow(this);
        FlowControl = new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);
        DisplayFlow = new DisplayFlow(this);
        Copilot = new FlowCopilotService(this);

        ServiceConfig = ServiceConfig.Instance;
        WindowsServiceX64 = new WindowsServiceBase(ServiceConfig.CVMainService_x64Info);
        WindowsServiceDev = new WindowsServiceBase(ServiceConfig.CVMainService_devInfo);
        WindowsServiceReg = new WindowsServiceBase(ServiceConfig.RegistrationCenterServiceInfo);

        EditTemplateFlowCommand = new RelayCommand(_ => EditSelectedFlowTemplate());
        MeasureBatchManagerCommand = new RelayCommand(_ => OpenBatchManager());
        AskCopilotFlowCommand = new RelayCommand(_ => Copilot.AskAboutCurrentFlow());
        OpenServiceCommand = new RelayCommand(
            _ => PlatformHelper.OpenFolderAndSelectFile(ServiceConfig.RegistrationCenterService),
            _ => File.Exists(ServiceConfig.RegistrationCenterService));
        OpenCameraLogCommand = new RelayCommand(_ => OpenCameraLog());

        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = Properties.Resources.Inquire, Command = MeasureBatchManagerCommand });
        ContextMenu.Items.Add(new MenuItem { Header = Properties.Resources.Flow_AskAiAnalyzeCurrentFlow, Command = AskCopilotFlowCommand });
        ContextMenu.Items.Add(new MenuItem { Header = Properties.Resources.Property, Command = Config.EditCommand });
        ContextMenu.Items.Add(new MenuItem { Header = "OpenService", Command = OpenServiceCommand });
    }

    private void OpenCameraLog()
    {
        string? baseDirectory = Path.GetDirectoryName(ServiceConfig.CVMainService_x64);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return;

        string? latestLogPath = ServiceLogFileLocator.GetMostRecentLogFile(
            Path.Combine(baseDirectory, "log"),
            "CVMainWindowsService_x64_camera");
        if (!string.IsNullOrEmpty(latestLogPath))
            new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312")).Show();
    }

    private static void OpenBatchManager()
    {
        var frame = new Frame();
        frame.Navigate(new MeasureBatchManagerPage(frame));
        new Window
        {
            Title = Properties.Resources.Inquire,
            Owner = Application.Current.GetActiveWindow(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Height = 720,
            Width = 1280,
            Content = frame,
        }.Show();
    }

    private void EditSelectedFlowTemplate()
    {
        if (TemplateFlowParamsIndex < 0 || TemplateFlowParamsIndex >= FlowParams.Count)
            return;

        new TemplateEditorWindow(new TemplateFlow(), TemplateFlowParamsIndex)
        {
            Owner = Application.Current.GetActiveWindow(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }.ShowDialog();
        _ = View.DisplayFlow.Refresh();
    }
}
