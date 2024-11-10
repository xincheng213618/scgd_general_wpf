using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class MotorConfig : ViewModelBase
    {
        public bool IsUseMotor { get => _IsUseMotor; set { _IsUseMotor = value; NotifyPropertyChanged(); } }
        private bool _IsUseMotor;

        public FOCUS_COMMUN eFOCUSCOMMUN { get => _eFOCUSCOMMUN; set { _eFOCUSCOMMUN = value; NotifyPropertyChanged(); } }
        private FOCUS_COMMUN _eFOCUSCOMMUN;

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

        [JsonIgnore]
        public int Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private int _Position;

        public int DwTimeOut { get => _dwTimeOut; set { _dwTimeOut = value; NotifyPropertyChanged(); } }
        private int _dwTimeOut = 5000;

        // 电机运行加速度
        [JsonProperty("Run_nAcc")]
        public int RunAcceleration { get => _RunAcceleration; set { _RunAcceleration = value; NotifyPropertyChanged(); } }
        private int _RunAcceleration = 409600;

        // 电机平稳运行速度
        [JsonProperty("Run_nSpeed")]
        public int RunSpeed { get => _RunSpeed; set { _RunSpeed = value; NotifyPropertyChanged(); } }
        private int _RunSpeed = 500000;

        // 电机减速度
        [JsonProperty("Run_ndec")]
        public int Deceleration { get => _Deceleration; set { _Deceleration = value; NotifyPropertyChanged(); } }
        private int _Deceleration = 409600;

        // 回原点时的加速度
        [JsonProperty("Home_nAcc")]
        public int HomeAcceleration { get => _HomeAcceleration; set { _HomeAcceleration = value; NotifyPropertyChanged(); } }
        private int _HomeAcceleration = 409600;

        // 高速回原点
        [JsonProperty("Home_nHighSpeed")]
        public int HomeHightSpeed { get => _HomeHightSpeed; set { _HomeHightSpeed = value; NotifyPropertyChanged(); } }
        private int _HomeHightSpeed = 2000;

        // 低速高速回原点
        [JsonProperty("Home_nLowSpeed")]
        public int HomeLowSpeed { get => _HomeLowSpeed; set { _HomeLowSpeed = value; NotifyPropertyChanged(); } }
        private int _HomeLowSpeed = 2000;
    }
}