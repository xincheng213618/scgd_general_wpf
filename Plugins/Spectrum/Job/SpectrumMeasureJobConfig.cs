using ColorVision.Scheduler;
using System.ComponentModel;

namespace Spectrum.Job
{
    /// <summary>
    /// 光谱测量定时任务配置
    /// </summary>
    public class SpectrumMeasureJobConfig : JobConfigBase
    {
        [Category("测量设置")]
        [DisplayName("测量次数")]
        [Description("执行测量的次数，默认为1次")]
        public int MeasureCount { get => _MeasureCount; set { _MeasureCount = value; OnPropertyChanged(); } }
        private int _MeasureCount = 1;

        [Category("测量设置")]
        [DisplayName("测量间隔(ms)")]
        [Description("多次测量之间的间隔时间（毫秒）")]
        public int MeasureInterval { get => _MeasureInterval; set { _MeasureInterval = value; OnPropertyChanged(); } }
        private int _MeasureInterval = 100;
    }
}
