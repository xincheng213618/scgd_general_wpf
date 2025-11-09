using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class SelfAdaptionInitDark :ViewModelBase
    {
        /// <summary>
        /// 积分起始时间
        /// </summary>
        [DisplayName("IntegrationStartTime")]
        public float BeginIntegralTime { get => _BeginIntegralTime; set { _BeginIntegralTime = value; OnPropertyChanged(); } }
        private float _BeginIntegralTime = 100;
        /// <summary>
        /// 平均次数
        /// </summary>
        [DisplayName("AverageTimes")]
        public int NumberOfAverage { get => _NumberOfAverage; set { _NumberOfAverage = value; OnPropertyChanged(); } }
        private int _NumberOfAverage = 1;
        /// <summary>
        /// 间隔时间
        /// </summary>
        [DisplayName("IntervalTime")]
        public int StepTime { get => _StepTime; set { _StepTime = value; OnPropertyChanged(); } }
        private int _StepTime = 100;
        /// <summary>
        /// 步数
        /// </summary>
        [DisplayName("Step")]
        public int StepCount { get => _StepCount; set { _StepCount = value; OnPropertyChanged(); } }
        private int _StepCount = 50;
    }
}
