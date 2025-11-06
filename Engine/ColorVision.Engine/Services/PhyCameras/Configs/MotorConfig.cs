#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public enum GoHome_WAY : int
    {
        GoHome_Mode_1 = 1,
        GoHome_Mode_2,
        GoHome_Mode_3,
        GoHome_Mode_4,
        negative_limit = 17,
        positive_limit = 18,
        GoHome_limit_1,
        GoHome_limit_2,
        GoHome_limit_3,
        GoHome_limit_4,
        GoHome_positive_limit_1,
        GoHome_positive_limit_2,
        GoHome_positive_limit_3,
        GoHome_positive_limit_4,
        GoHome_negative_limit_1,
        GoHome_negative_limit_2,
        GoHome_negative_limit_3,
        GoHome_negative_limit_4,
    };

    public enum FindFuncModel
    {
        FindMaxDiff = 0,
        FindDichotomy = 1,
        FindMaxSearch = 2,
    }

    [DisplayName("MotorConfig")]
    public class MotorConfigBase: ViewModelBase
    {


        [DisplayName("QuaryMethods")]
        public FindFuncModel FindFuncModel { get => _FindFuncModel; set { _FindFuncModel = value; OnPropertyChanged(); } }
        private FindFuncModel _FindFuncModel;

        public FOCUS_COMMUN eFOCUSCOMMUN { get => _eFOCUSCOMMUN; set { _eFOCUSCOMMUN = value; OnPropertyChanged(); } }
        private FOCUS_COMMUN _eFOCUSCOMMUN;

        [DisplayName("Serial"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [DisplayName("BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 115200;

        [JsonIgnore]
        [Browsable(false)]
        public int Position { get => _Position; set { _Position = value; OnPropertyChanged(); } }
        private int _Position;

        [JsonIgnore]
        [Browsable(false)]
        public double VIDPosition { get => _VIDPosition; set { _VIDPosition = value; OnPropertyChanged(); } }
        private double _VIDPosition ;

        [DisplayName("Timeout")]
        public int DwTimeOut { get => _dwTimeOut; set { _dwTimeOut = value; OnPropertyChanged(); } }
        private int _dwTimeOut = 5000;

        // 电机运行加速度
        [JsonProperty("Run_nAcc")]
        [DisplayName("MotorAcceleration")]
        public int RunAcceleration { get => _RunAcceleration; set { _RunAcceleration = value; OnPropertyChanged(); } }
        private int _RunAcceleration = 409600;

        // 电机平稳运行速度
        [JsonProperty("Run_nSpeed")]
        [DisplayName("MotorSteadySpeed")]
        public int RunSpeed { get => _RunSpeed; set { _RunSpeed = value; OnPropertyChanged(); } }
        private int _RunSpeed = 500000;

        // 电机减速度
        [JsonProperty("Run_ndec")]
        [DisplayName("MotorDeceleration")]
        public int Deceleration { get => _Deceleration; set { _Deceleration = value; OnPropertyChanged(); } }
        private int _Deceleration = 409600;

        // 回原点时的加速度
        [JsonProperty("Home_nAcc")]
        [DisplayName("HomingAcceleration")]
        public int HomeAcceleration { get => _HomeAcceleration; set { _HomeAcceleration = value; OnPropertyChanged(); } }
        private int _HomeAcceleration = 409600;

        [DisplayName("HomingMethod")]
        public GoHome_WAY GoHomeWay { get => _GoHomeWay; set { _GoHomeWay = value; OnPropertyChanged(); } }
        private GoHome_WAY _GoHomeWay = GoHome_WAY.negative_limit;

        [DisplayName("HomingTimeout")]
        public int HomeTimeout { get => _HomeTimeout; set { _HomeTimeout = value; OnPropertyChanged(); } }
        private int _HomeTimeout = 5000;

        // 高速回原点
        [JsonProperty("Home_nHighSpeed")]
        [DisplayName("HighSpeedHoming")]
        public int HomeHightSpeed { get => _HomeHightSpeed; set { _HomeHightSpeed = value; OnPropertyChanged(); } }
        private int _HomeHightSpeed = 2000;

        // 低速回原点
        [JsonProperty("Home_nLowSpeed")]
        [DisplayName("LowSpeedHoming")]
        public int HomeLowSpeed { get => _HomeLowSpeed; set { _HomeLowSpeed = value; OnPropertyChanged(); } }
        private int _HomeLowSpeed = 2000;

        [DisplayName("MotorMoveRangeLowerLimit")]
        public int MinPosition { get => _MinPosition; set { _MinPosition = value; OnPropertyChanged(); } }
        private int _MinPosition;

        [DisplayName("MotorMoveRangeupperLimit")]
        public int MaxPosition { get => _MaxPosition; set { _MaxPosition = value; OnPropertyChanged(); } }
        private int _MaxPosition = 7800;

        [DisplayName("FocusImageCount")]
        public int AutoFocusSaveImageNum { get => _AutoFocusSaveImageNum; set { _AutoFocusSaveImageNum = value; OnPropertyChanged(); } }
        private int _AutoFocusSaveImageNum = 2;
    }


    [DisplayName("MotorConfig")]
    public class MotorConfig : MotorConfigBase
    {

        [DisplayName("ConfigMotor")]
        public bool IsUseMotor { get => _IsUseMotor; set { _IsUseMotor = value; if(!value) IsCameraLinkage =false; OnPropertyChanged(); } }
        private bool _IsUseMotor;

        [DisplayName("CameraLinkage")]
        public bool IsCameraLinkage { get => _IsCameraLinkage; set { _IsCameraLinkage = value; OnPropertyChanged(); } }
        private bool _IsCameraLinkage;


        [DisplayName("VID")]
        public MotorVID VID { get => _VID; set { _VID = value; OnPropertyChanged(); } }
        private MotorVID _VID = new MotorVID();

    }


    public class MotorVID : ViewModelBase
    {
        [DisplayName("IsUseVID")]
        public bool IsUseVID { get => _IsUseVID; set { _IsUseVID = value; OnPropertyChanged(); } }
        private bool _IsUseVID ;
        [DisplayName("MappingFileName"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string MappingFileName { get=>_MappingFileName; set { _MappingFileName = value; OnPropertyChanged(); } }
        private string _MappingFileName = string.Empty;
        [DisplayName("Fit")]
        public int Fit { get => _Fit; set { _Fit = value; OnPropertyChanged(); } } 
        private int _Fit = 5;
    }
}