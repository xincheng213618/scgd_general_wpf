using Newtonsoft.Json;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class PhyExpTimeCfg : ViewModelBase
    {

        [JsonProperty("autoExpFlag")]
        public bool AutoExpFlag { get => _AutoExpFlag; set { _AutoExpFlag = value; OnPropertyChanged(); } }
        private bool _AutoExpFlag = true;

        /// <summary>
        /// 自动曝光
        /// </summary>
        [JsonProperty("autoExpTimeBegin")]
        public float AutoExpTimeBegin { get => _AutoExpTimeBegin; set { _AutoExpTimeBegin = value; OnPropertyChanged(); } }
        private float _AutoExpTimeBegin = 10;

        /// <summary>
        ///自动同步频率
        /// </summary>
        [JsonProperty("autoExpSyncFreq")]
        public float AutoExpSyncFreq { get => _AutoExpSyncFreq; set { _AutoExpSyncFreq = value; OnPropertyChanged(); } }
        private float _AutoExpSyncFreq = -1;

        [JsonProperty("autoExpSaturation")]
        public float AutoExpSaturation { get => _AutoExpSaturation; set { _AutoExpSaturation = value; OnPropertyChanged(); } }
        private float _AutoExpSaturation = 70.0f;

        [JsonProperty("autoExpSatMaxAD")]
        public uint AutoExpSatMaxAD { get => _AutoExpSatMaxAD; set { _AutoExpSatMaxAD = value; OnPropertyChanged(); } }
        private uint _AutoExpSatMaxAD = 65000;

        /// <summary>
        ///误差值
        /// </summary>
        [JsonProperty("autoExpMaxPecentage")]
        public float AutoExpMaxPecentage { get => _AutoExpMaxPecentage; set { _AutoExpMaxPecentage = value; OnPropertyChanged(); } }
        private float _AutoExpMaxPecentage = 0.01f;

        [JsonProperty("autoExpSatDev")]
        public float AutoExpSatDev { get => _AutoExpSatDev; set { _AutoExpSatDev = value; OnPropertyChanged(); } }
        private float _AutoExpSatDev = 20.0f;
        /// <summary>
        /// 最大曝光
        /// </summary>
        [JsonProperty("maxExpTime")]
        public float MaxExpTime { get => _MaxExpTime; set { _MaxExpTime = value; OnPropertyChanged(); } }
        private float _MaxExpTime = 60000;

        /// <summary>
        /// 最小曝光
        /// </summary>
        [JsonProperty("minExpTime")]
        public float MinExpTime { get => _MinExpTime; set { _MinExpTime = value; OnPropertyChanged(); } }
        private float _MinExpTime = 0.2f;

        /// <summary>
        /// burst的阈值
        /// </summary>
        [JsonProperty("burstThreshold")]
        public float BurstThreshold { get => _BurstThreshold; set { _BurstThreshold = value; OnPropertyChanged(); } }
        private float _BurstThreshold = 200.0f;




    }
}