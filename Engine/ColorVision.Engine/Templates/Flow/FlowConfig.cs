using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowConfig : ViewModelBase, IConfig
    {
        public static FlowConfig Instance => ConfigService.Instance.GetRequiredService<FlowConfig>();

        public RelayCommand EditCommand { get; set; }
        public FlowConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        [DisplayName("修改保存提示")]
        public bool IsAutoEditSave { get => _IsAutoEditSave; set { _IsAutoEditSave = value; NotifyPropertyChanged(); } }
        private bool _IsAutoEditSave = true;
        [DisplayName("自动适配")]
        public bool IsAutoSize { get => _IsAutoSize; set { _IsAutoSize = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSize;

        [DisplayName("硬盘警告")]
        public bool ShowWarning { get => _ShowWarning; set { _ShowWarning = value; NotifyPropertyChanged(); } }
        private bool _ShowWarning = true;

        [DisplayName("显示nickName")]
        public bool IsShowNickName  {   get => _IsShowNickName; set { _IsShowNickName = value; NotifyPropertyChanged(); }  }
        private bool _IsShowNickName;

        [DisplayName("显示流程详细")]
        public bool IsShowDetailFlow { get => _IsShowDetailFlow; set { _IsShowDetailFlow = value; NotifyPropertyChanged(); } }
        private bool _IsShowDetailFlow ;

        [DisplayName("流程运行时自动刷新")]
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;

        [DisplayName("流程运行显示监控")]
        public bool FlowPreviewMsg { get => _FlowPreviewMsg; set { _FlowPreviewMsg = value; NotifyPropertyChanged(); } }
        private bool _FlowPreviewMsg = true;

        public bool FlowRun { get => _FlowRun; set { _FlowRun = value; NotifyPropertyChanged(); } }
        private bool _FlowRun;

        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; NotifyPropertyChanged(); } }
        private int _LastSelectFlow;
        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;


        public long Capacity { get; set; } = 10L * 1024 * 1024 * 1024; //10GB

        [JsonIgnore]
        [DisplayName("硬盘警告大小设置")]
        public string CapacityInput
        {
            get => MemorySize.MemorySizeText(Capacity);
            set
            {
                if (MemorySize.TryParseMemorySize(value, out long parsedValue))
                {
                    Capacity = parsedValue;
                }
            }
        }
    }
}
