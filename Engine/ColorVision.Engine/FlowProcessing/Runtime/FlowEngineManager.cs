#pragma warning disable CA1822,CA1859,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Logging;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ST.Library.UI.NodeEditor;

namespace ColorVision.Engine.FlowProcessing
{
    public class FlowEngineManager : ViewModelBase
    {
        private static FlowEngineManager _instance;
        private static readonly object _locker = new();
        public static FlowEngineManager? Current { get { lock (_locker) { return _instance; } } }
        public static FlowEngineManager GetInstance()
        {
            lock (_locker)
            {
                if (_instance == null)
                    _instance = new FlowEngineManager();
                return _instance;
            }
        }

        public FlowControl FlowControl { get; set; }

        public static FlowEngineConfig Config => FlowEngineConfig.Instance;
        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; } = TemplateFlow.Params;

        public int TemplateFlowParamsIndex { get => Config.TemplateFlowParamsIndex; set { Config.TemplateFlowParamsIndex = value; OnPropertyChanged(); } }

       

        public ContextMenu ContextMenu { get; set; }
        public RelayCommand EditTemplateFlowCommand { get; set; }

        public RelayCommand MeasureBatchManagerCommand { get; set; }
        public ViewFlow View { get; set; }
        public FlowEngineControl FlowEngineControl { get; set; }

        public MeasureBatchModel Batch { get => _Batch; set { _Batch = value; OnPropertyChanged(); BatchRecord?.Invoke(this, _Batch); } }
        private MeasureBatchModel _Batch;
        public event EventHandler<MeasureBatchModel> BatchRecord;

        public FlowParam SlectFlowParam { get => _SlectFlowParam; set { _SlectFlowParam = value; OnPropertyChanged(); } }
        private FlowParam _SlectFlowParam;

        public double BatchProgress { get => _BatchProgress; set { _BatchProgress = value; OnPropertyChanged(); } }
        private double _BatchProgress ;

        public ServiceConfig ServiceConfig { get; set; }


        public ObservableCollection<CVBaseServerNode> CVBaseServerNodes { get; set; } = new ObservableCollection<CVBaseServerNode>();

        [DisplayName("OpenService")]
        public RelayCommand OpenServiceCommand { get; set; }

        public WindowsServiceBase WindowsServiceX64 { get; set; }
        public WindowsServiceBase WindowsServiceDev { get; set; }
        public WindowsServiceBase WindowsServiceReg { get; set; }
        public RelayCommand OpenCameraLogCommand { get; set; }
        public RelayCommand AskCopilotFlowCommand { get; set; }
        public FlowCopilotService Copilot { get; }

        public Version ServiceVersion => new Version(ServiceConfig.RegistrationCenterServiceInfo.FileVersion ?? string.Empty);

        public FlowEngineManager()
        {
            ContextMenu = new ContextMenu();
            EditTemplateFlowCommand = new RelayCommand(a=> EditTemplateFlow());


            MeasureBatchManagerCommand = new RelayCommand(a=> MeasureBatchManager());

            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Inquire, Command = MeasureBatchManagerCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Property, Command = Config.EditCommand });

            FlowEngineControl = new FlowEngineControl(false);

            View = new ViewFlow(this);
            Copilot = new FlowCopilotService(this);
            AskCopilotFlowCommand = new RelayCommand(a => Copilot.AskAboutCurrentFlow());
            ContextMenu.Items.Insert(1, new MenuItem() { Header = Properties.Resources.Flow_AskAiAnalyzeCurrentFlow, Command = AskCopilotFlowCommand });

            FlowControl = new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

            ServiceConfig = ServiceConfig.Instance;
            OpenServiceCommand = new RelayCommand(a => ColorVision.Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(ServiceConfig.RegistrationCenterService),a=>File.Exists(ServiceConfig.RegistrationCenterService));
            ContextMenu.Items.Add(new MenuItem() { Header = "OpenService", Command = OpenServiceCommand });
            WindowsServiceX64 = new WindowsServiceBase(ServiceConfig.CVMainService_x64Info);
            WindowsServiceDev = new WindowsServiceBase(ServiceConfig.CVMainService_devInfo);
            WindowsServiceReg = new WindowsServiceBase(ServiceConfig.RegistrationCenterServiceInfo);
            OpenCameraLogCommand = new RelayCommand(a => OpenCameraLog());

            DisplayFlow = new DisplayFlow(this);
        }

        public DisplayFlow DisplayFlow { get; set; }

        public void OpenCameraLog()
        {
            string baseDir = Directory.GetParent(ServiceConfig.CVMainService_x64).FullName;
            string? latestLogPath = ServiceLogFileLocator.GetMostRecentLogFile(Path.Combine(baseDir,"log"), "CVMainWindowsService_x64_camera");
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
        }

        public void MeasureBatchManager()
        {
            Frame frame = new Frame();

            MeasureBatchManagerPage batchDataHistory = new MeasureBatchManagerPage(frame);
            frame.Navigate(batchDataHistory);

            Window window = new Window() { Title = ColorVision.Engine.Properties.Resources.Inquire, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Height = 720, Width = 1280 };
            window.Content = frame;
            window.Show();
        }

        public void EditTemplateFlow()
        {
            if (TemplateFlowParamsIndex < 0 || TemplateFlowParamsIndex >= FlowParams.Count)
                return;
            new TemplateEditorWindow(new TemplateFlow(), TemplateFlowParamsIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            _ = View.DisplayFlow.Refresh();
        }
    }
}
