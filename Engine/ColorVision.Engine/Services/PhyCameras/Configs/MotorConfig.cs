using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{

    public enum FindFuncModel
    {
        FindMaxDiff = 0,
        FindDichotomy = 1,
        FindMaxSearch = 2,
    }

    [DisplayName("电机配置")]
    public class MotorConfigBase: ViewModelBase
    {


        [DisplayName("查询方法")]
        public FindFuncModel FindFuncModel { get => _FindFuncModel; set { _FindFuncModel = value; NotifyPropertyChanged(); } }
        private FindFuncModel _FindFuncModel;

        public FOCUS_COMMUN eFOCUSCOMMUN { get => _eFOCUSCOMMUN; set { _eFOCUSCOMMUN = value; NotifyPropertyChanged(); } }
        private FOCUS_COMMUN _eFOCUSCOMMUN;

        [DisplayName("串口"), PropertyEditorType(PropertyEditorType.TextSerialPort)]
        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        [DisplayName("波特率"), PropertyEditorType(PropertyEditorType.TextBaudRate)]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

        [JsonIgnore]
        [Browsable(false)]
        public int Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private int _Position;

        [JsonIgnore]
        [Browsable(false)]
        public double VIDPosition { get => _VIDPosition; set { _VIDPosition = value; NotifyPropertyChanged(); } }
        private double _VIDPosition ;

        [DisplayName("超时时间")]
        public int DwTimeOut { get => _dwTimeOut; set { _dwTimeOut = value; NotifyPropertyChanged(); } }
        private int _dwTimeOut = 5000;

        // 电机运行加速度
        [JsonProperty("Run_nAcc")]
        [DisplayName("电机运行加速度")]
        public int RunAcceleration { get => _RunAcceleration; set { _RunAcceleration = value; NotifyPropertyChanged(); } }
        private int _RunAcceleration = 409600;

        // 电机平稳运行速度
        [JsonProperty("Run_nSpeed")]
        [DisplayName("电机平稳运行速度")]
        public int RunSpeed { get => _RunSpeed; set { _RunSpeed = value; NotifyPropertyChanged(); } }
        private int _RunSpeed = 500000;

        // 电机减速度
        [JsonProperty("Run_ndec")]
        [DisplayName("电机减速度")]
        public int Deceleration { get => _Deceleration; set { _Deceleration = value; NotifyPropertyChanged(); } }
        private int _Deceleration = 409600;

        // 回原点时的加速度
        [JsonProperty("Home_nAcc")]
        [DisplayName("回原点时的加速度")]
        public int HomeAcceleration { get => _HomeAcceleration; set { _HomeAcceleration = value; NotifyPropertyChanged(); } }
        private int _HomeAcceleration = 409600;

        // 高速回原点
        [JsonProperty("Home_nHighSpeed")]
        [DisplayName("高速回原点")]
        public int HomeHightSpeed { get => _HomeHightSpeed; set { _HomeHightSpeed = value; NotifyPropertyChanged(); } }
        private int _HomeHightSpeed = 2000;

        // 低速回原点
        [JsonProperty("Home_nLowSpeed")]
        [DisplayName("低速回原点")]
        public int HomeLowSpeed { get => _HomeLowSpeed; set { _HomeLowSpeed = value; NotifyPropertyChanged(); } }
        private int _HomeLowSpeed = 2000;

        [DisplayName("电机移动区间下限")]
        public int MinPosition { get => _MinPosition; set { _MinPosition = value; NotifyPropertyChanged(); } }
        private int _MinPosition;

        [DisplayName("电机移动区间上限")]
        public int MaxPosition { get => _MaxPosition; set { _MaxPosition = value; NotifyPropertyChanged(); } }
        private int _MaxPosition = 7800;
    }

    [DisplayName("电机配置")]
    public class MotorConfig : MotorConfigBase
    {

        [DisplayName("配置电机")]
        public bool IsUseMotor { get => _IsUseMotor; set { _IsUseMotor = value; if(!value) IsCameraLinkage =false; NotifyPropertyChanged(); } }
        private bool _IsUseMotor;

        [DisplayName("相机联动")]
        public bool IsCameraLinkage { get => _IsCameraLinkage; set { _IsCameraLinkage = value; NotifyPropertyChanged(); } }
        private bool _IsCameraLinkage = true;


        [DisplayName("VID")]
        public MotorVID VID { get => _VID; set { _VID = value; NotifyPropertyChanged(); } }
        private MotorVID _VID = new MotorVID();

    }


    public class MotorVID : ViewModelBase
    {
        [DisplayName("IsUseVID")]
        public bool IsUseVID { get => _IsUseVID; set { _IsUseVID = value; NotifyPropertyChanged(); } }
        private bool _IsUseVID ;
        [DisplayName("MappingFileName")]
        public string MappingFileName { get=>_MappingFileName; set { _MappingFileName = value; NotifyPropertyChanged(); } }
        private string _MappingFileName = string.Empty;
        [DisplayName("Fit")]
        public int Fit { get => _Fit; set { _Fit = value; NotifyPropertyChanged(); } } 
        private int _Fit = 5;
    }
}