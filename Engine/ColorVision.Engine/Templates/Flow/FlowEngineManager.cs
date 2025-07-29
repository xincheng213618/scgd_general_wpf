using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
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
        public bool IsAutoEditSave { get => _IsAutoEditSave; set { _IsAutoEditSave = value; NotifyPropertyChanged(); } }
        private bool _IsAutoEditSave;
        [DisplayName("自动适配")]
        public bool IsAutoSize { get => _IsAutoSize; set { _IsAutoSize = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSize;

        [DisplayName("显示nickName")]
        public bool IsShowNickName { get => _IsShowNickName; set { _IsShowNickName = value; NotifyPropertyChanged(); } }
        private bool _IsShowNickName;

        [DisplayName("IsShowLargeFlow")]
        public bool IsShowLargeFlow { get => _IsShowLargeFlow; set { _IsShowLargeFlow = value; NotifyPropertyChanged(); } }
        
        private bool _IsShowLargeFlow;

        [DisplayName("流程运行时自动刷新")]
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;

        [DisplayName("流程运行显示监控")]
        public bool FlowPreviewMsg { get => _FlowPreviewMsg; set { _FlowPreviewMsg = value; NotifyPropertyChanged(); } }
        private bool _FlowPreviewMsg = true;

        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; NotifyPropertyChanged(); } }
        private int _LastSelectFlow;

        public Dictionary<string, long> FlowRunTime { get; set; } = new Dictionary<string, long>();

        [Browsable(false)]
        public int TemplateFlowParamsIndex { get => _TemplateFlowParamsIndex; set { _TemplateFlowParamsIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateFlowParamsIndex;

        [Browsable(false)]
        public int TemplateLargeFlowParamsIndex { get => _TemplateLargeFlowParamsIndex; set { _TemplateLargeFlowParamsIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateLargeFlowParamsIndex;

    }
    public class FlowEngineManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowEngineManager));

        private static FlowEngineManager _instance;
        private static readonly object _locker = new();
        public static FlowEngineManager GetInstance() { lock (_locker) { return _instance ??= new FlowEngineManager(); } }

        public static FlowEngineConfig Config => FlowEngineConfig.Instance;
        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; } = TemplateFlow.Params;

        public int TemplateFlowParamsIndex { get => Config.TemplateFlowParamsIndex; set { Config.TemplateFlowParamsIndex = value; NotifyPropertyChanged(); } }
        public static ObservableCollection<TemplateModel<TJLargeFlowParam>> LargeFlowParams => TemplateLargeFlow.Params;
        public int TemplateLargeFlowParamsIndex { get => Config.TemplateLargeFlowParamsIndex; set { Config.TemplateLargeFlowParamsIndex = value; NotifyPropertyChanged(); } }
        public RelayCommand EditFlowCommand { get; set; }

        public RelayCommand EditTemplateFlowCommand { get; set; }
        public RelayCommand EditLargeFlowCommand { get; set; }
        public RelayCommand EditTemplateLargeFlowCommand { get; set; }
        public ViewFlow View { get; set; }

        public FlowEngineManager()
        {
            EditFlowCommand = new RelayCommand(a => EditFlow());
            EditTemplateFlowCommand = new RelayCommand(a=> EditTemplateFlow());
            EditLargeFlowCommand = new RelayCommand(a => EditLargeFlow());
            EditTemplateLargeFlowCommand = new RelayCommand(a => EditTemplateLargeFlow());

            View = new ViewFlow();
            View.View.Title = $"流程窗口 ";
        }
        public void EditFlow()
        {
            new FlowEngineToolWindow(FlowParams[TemplateFlowParamsIndex].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public void EditTemplateFlow()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateFlowParamsIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
        public void EditLargeFlow()
        {
            new EditLargeFlow(LargeFlowParams[TemplateLargeFlowParamsIndex].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }
        public void EditTemplateLargeFlow()
        {
            new TemplateEditorWindow(new TemplateLargeFlow(), TemplateLargeFlowParamsIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }




    }
}
