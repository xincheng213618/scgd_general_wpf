using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectLUX.Process
{
    /// <summary>
    /// Base class for process configurations with SaveCsv support.
    /// </summary>
    public abstract class ProcessConfigBase : ViewModelBase
    {
        [DisplayName("保存CSV")]
        [Description("是否保存测试结果到CSV文件")]
        public bool SaveCsv { get => _SaveCsv; set { _SaveCsv = value; OnPropertyChanged(); } }
        private bool _SaveCsv = false;
    }
}
