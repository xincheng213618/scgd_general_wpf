using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public class IInitializerFlow : IInitializer
    {
        public string Name => "Flow";
        public IEnumerable<string> Dependencies => Array.Empty<string>();
        public int Order => 10;

        public Task InitializeAsync()
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;

            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            return Task.CompletedTask;
        }
    }

    public class FlowEngineConfig : ViewModelBase, IConfig
    {
        public static FlowEngineConfig Instance => ConfigService.Instance.GetRequiredService<FlowEngineConfig>();

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public FlowEngineConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        [DisplayName("修改保存提示")]
        public bool IsAutoEditSave { get => _IsAutoEditSave; set { _IsAutoEditSave = value; OnPropertyChanged(); } }
        private bool _IsAutoEditSave;

        [DisplayName("自动适配")]
        public bool IsAutoSize { get => _IsAutoSize; set { _IsAutoSize = value; OnPropertyChanged(); } }
        private bool _IsAutoSize;

        [DisplayName("显示nickName")]
        public bool IsShowNickName { get => _IsShowNickName; set { _IsShowNickName = value; OnPropertyChanged(); } }
        private bool _IsShowNickName;


        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; OnPropertyChanged(); } }
        private int _LastSelectFlow;

        public Dictionary<string, long> FlowRunTime { get; set; } = new Dictionary<string, long>();

        [Browsable(false)]
        public int TemplateFlowParamsIndex { get => _TemplateFlowParamsIndex; set { _TemplateFlowParamsIndex = value; OnPropertyChanged(); } }
        private int _TemplateFlowParamsIndex;

        [Browsable(false)]
        public int TemplateLargeFlowParamsIndex { get => _TemplateLargeFlowParamsIndex; set { _TemplateLargeFlowParamsIndex = value; OnPropertyChanged(); } }
        private int _TemplateLargeFlowParamsIndex;

        [JsonIgnore,Browsable(false)]
        public bool IsReady { get => _IsReady; set { if (value == _IsReady) return; _IsReady = value; OnPropertyChanged(); } }
        private bool _IsReady;
    }


    public class FlowEngineManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowEngineManager));

        private static FlowEngineManager _instance;
        private static readonly object _locker = new();
        public static FlowEngineManager GetInstance() { lock (_locker) { return _instance ??= new FlowEngineManager(); } }


        public static FlowEngineConfig Config => FlowEngineConfig.Instance;
        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; } = TemplateFlow.Params;

        public int TemplateFlowParamsIndex { get => Config.TemplateFlowParamsIndex; set { Config.TemplateFlowParamsIndex = value; OnPropertyChanged(); } }

       

        public ContextMenu ContextMenu { get; set; }

        public RelayCommand EditFlowCommand { get; set; }

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

        public Version ServiceVersion => new Version(ServiceConfig.RegistrationCenterServiceInfo.FileVersion ?? string.Empty);

        public FlowEngineManager()
        {
            ContextMenu = new ContextMenu();

            EditFlowCommand = new RelayCommand(a => EditFlow());
            EditTemplateFlowCommand = new RelayCommand(a=> EditTemplateFlow());


            MeasureBatchManagerCommand = new RelayCommand(a=> MeasureBatchManager());

            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Inquire, Command = MeasureBatchManagerCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Property, Command = Config.EditCommand });

            FlowEngineControl = new FlowEngineControl(false);

            View = new ViewFlow(FlowEngineControl);
            View.View.Title = ColorVision.Engine.Properties.Resources.Flow;
            ServiceConfig = ServiceConfig.Instance;
            OpenServiceCommand = new RelayCommand(a => ColorVision.Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(ServiceConfig.RegistrationCenterService),a=>File.Exists(ServiceConfig.RegistrationCenterService));
            ContextMenu.Items.Add(new MenuItem() { Header = "OpenService", Command = OpenServiceCommand });
            WindowsServiceX64 = new WindowsServiceBase(ServiceConfig.CVMainService_x64Info);
            WindowsServiceDev = new WindowsServiceBase(ServiceConfig.CVMainService_devInfo);
            WindowsServiceReg = new WindowsServiceBase(ServiceConfig.RegistrationCenterServiceInfo);
            OpenCameraLogCommand = new RelayCommand(a => OpenCameraLog()); 
        }


        public void OpenCameraLog()
        {
            string baseDir = Directory.GetParent(ServiceConfig.CVMainService_x64).FullName;
            string latestLogPath = LogFileHelper.GetMostRecentLogFile(Path.Combine(baseDir,"log"), "CVMainWindowsService_x64_camera");
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

        public void EditFlow()
        {
            new FlowEngineToolWindow(FlowParams[TemplateFlowParamsIndex].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
            _=View.DisplayFlow.Refresh();
        }

        public void EditTemplateFlow()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateFlowParamsIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            _ = View.DisplayFlow.Refresh();
        }
    }
}
