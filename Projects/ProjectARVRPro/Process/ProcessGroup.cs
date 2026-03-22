using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// 代表一组有序的流程配置，用于不同产品/场景的测试方案切换。
    /// </summary>
    public class ProcessGroup : ViewModelBase
    {
        /// <summary>
        /// 组名（唯一标识）
        /// </summary>
        public string Name { get => _Name; set { if (_Name != value) { _Name = value; OnPropertyChanged(); } } }
        private string _Name;

        /// <summary>
        /// 组内的流程列表
        /// </summary>
        public ObservableCollection<ProcessMeta> ProcessMetas { get; set; } = new();
    }
}
