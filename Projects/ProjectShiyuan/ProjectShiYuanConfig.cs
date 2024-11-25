using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.Engine.Services.Flow;
using System.Windows;
using ColorVision.Common.Utilities;
using System;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ProjectShiYuanConfig: ViewModelBase, IConfig
    {
        public static ProjectShiYuanConfig Instance => ConfigService.Instance.GetRequiredService<ProjectShiYuanConfig>();
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }


        public ProjectShiYuanConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            OpenLogCommand = new RelayCommand(a => OpenLog());
        }
        public static void OpenLog()
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFlowEngineTool()
        {
            new FlowEngineToolWindow(FlowParam.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;


        public bool IsOpenConnect { get => _IsOpenConnect;set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;

        public string FlowName { get => _FlowName; set { _FlowName = value; NotifyPropertyChanged(); } }
        private string _FlowName;

        public int DeviceId { get => _DeviceId; set { _DeviceId = value; NotifyPropertyChanged(); } }
        private int _DeviceId;

        public string PortName { get => _PortName; set { _PortName = value; NotifyPropertyChanged(); } }
        private string _PortName;

        public string TestName { get => _TestName; set { _TestName = value; NotifyPropertyChanged(); } }
        private string _TestName = "WBROtest";

        public string DataPath { get => _DataPath; set { _DataPath = value; NotifyPropertyChanged(); } }
        private string _DataPath;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;

        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;



    }
}
