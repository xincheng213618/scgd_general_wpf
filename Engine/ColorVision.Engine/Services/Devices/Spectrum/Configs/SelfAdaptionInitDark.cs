using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class SelfAdaptionInitDark :ViewModelBase
    {
        /// <summary>
        /// 积分起始时间
        /// </summary>
        [DisplayName("积分起始时间")]
        public float BeginIntegralTime { get => _BeginIntegralTime; set { _BeginIntegralTime = value; NotifyPropertyChanged(); } }
        private float _BeginIntegralTime = 0;
        /// <summary>
        /// 平均次数
        /// </summary>
        [DisplayName("平均次数")]
        public int NumberOfAverage { get => _NumberOfAverage; set { _NumberOfAverage = value; NotifyPropertyChanged(); } }
        private int _NumberOfAverage = 1;
        /// <summary>
        /// 间隔时间
        /// </summary>
        [DisplayName("间隔时间")]
        public int StepTime { get => _StepTime; set { _StepTime = value; NotifyPropertyChanged(); } }
        private int _StepTime = 100;
        /// <summary>
        /// 步数
        /// </summary>
        [DisplayName("步数")]
        public int StepCount { get => _StepCount; set { _StepCount = value; NotifyPropertyChanged(); } }
        private int _StepCount = 50;
    }
}
