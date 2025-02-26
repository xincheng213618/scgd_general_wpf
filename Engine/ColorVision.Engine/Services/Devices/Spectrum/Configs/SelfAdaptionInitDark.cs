using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class SelfAdaptionInitDark :ViewModelBase
    {
        /// <summary>
        /// 积分起始时间
        /// </summary>
        public float BeginIntegralTime { get => _BeginIntegralTime; set { _BeginIntegralTime = value; NotifyPropertyChanged(); } }
        private float _BeginIntegralTime;
        /// <summary>
        /// 平均次数
        /// </summary>
        public int NumberOfAverage { get => _NumberOfAverage; set { _NumberOfAverage = value; NotifyPropertyChanged(); } }
        private int _NumberOfAverage;
        /// <summary>
        /// 间隔时间
        /// </summary>
        public int StepTime { get => _StepTime; set { _StepTime = value; NotifyPropertyChanged(); } }
        private int _StepTime;
        /// <summary>
        /// 步数
        /// </summary>
        public int StepCount { get => _StepCount; set { _StepCount = value; NotifyPropertyChanged(); } }
        private int _StepCount;
    }
}
