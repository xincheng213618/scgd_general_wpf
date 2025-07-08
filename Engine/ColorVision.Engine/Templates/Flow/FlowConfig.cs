using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
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

        [DisplayName("流程运行时自动刷新")]
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;

        [DisplayName("流程运行显示监控")]
        public bool FlowPreviewMsg { get => _FlowPreviewMsg; set { _FlowPreviewMsg = value; NotifyPropertyChanged(); } }
        private bool _FlowPreviewMsg = true;

        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; NotifyPropertyChanged(); } }
        private int _LastSelectFlow;

        public Dictionary<string, long> FlowRunTime { get; set; } = new Dictionary<string, long>();

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
