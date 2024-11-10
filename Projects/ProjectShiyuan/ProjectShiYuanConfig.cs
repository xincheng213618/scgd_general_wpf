using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ProjectShiYuanConfig: ViewModelBase, IConfig
    {
        public static ProjectShiYuanConfig Instance => ConfigService.Instance.GetRequiredService<ProjectShiYuanConfig>();

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

    }
}
