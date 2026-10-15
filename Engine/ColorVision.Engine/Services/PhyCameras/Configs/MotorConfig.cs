#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Utilities;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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

    [LocalizedDisplayName(nameof(Resources.MotorConfig))]
    public class MotorConfigBase: ViewModelBase
    {
        [Browsable(false), JsonIgnore]
        public virtual bool ShowMotorSettings => true;

        [LocalizedDisplayName(nameof(Resources.QuaryMethods)), PropertyVisibility(nameof(ShowMotorSettings))]
        public FindFuncModel FindFuncModel { get => _FindFuncModel; set { _FindFuncModel = value; OnPropertyChanged(); } }
        private FindFuncModel _FindFuncModel;

        [PropertyVisibility(nameof(ShowMotorSettings))]
        public FOCUS_COMMUN eFOCUSCOMMUN { get => _eFOCUSCOMMUN; set { _eFOCUSCOMMUN = value; OnPropertyChanged(); } }
        private FOCUS_COMMUN _eFOCUSCOMMUN;

        [LocalizedDisplayName(nameof(Resources.Serial)), PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(ShowMotorSettings))]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [LocalizedDisplayName(nameof(Resources.BaudRate)), PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [JsonIgnore]
        [Browsable(false)]
        public int Position { get => _Position; set { _Position = value; OnPropertyChanged(); } }
        private int _Position;

        [JsonIgnore]
        [Browsable(false)]
        public double VIDPosition { get => _VIDPosition; set { _VIDPosition = value; OnPropertyChanged(); } }
        private double _VIDPosition ;

        [LocalizedDisplayName(nameof(Resources.Timeout)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int DwTimeOut { get => _dwTimeOut; set { _dwTimeOut = value; OnPropertyChanged(); } }
        private int _dwTimeOut = 5000;

        // 电机运行加速度
        [JsonProperty("Run_nAcc")]
        [LocalizedDisplayName(nameof(Resources.MotorAcceleration)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int RunAcceleration { get => _RunAcceleration; set { _RunAcceleration = value; OnPropertyChanged(); } }
        private int _RunAcceleration = 409600;

        // 电机平稳运行速度
        [JsonProperty("Run_nSpeed")]
        [LocalizedDisplayName(nameof(Resources.MotorSteadySpeed)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int RunSpeed { get => _RunSpeed; set { _RunSpeed = value; OnPropertyChanged(); } }
        private int _RunSpeed = 500000;

        // 电机减速度
        [JsonProperty("Run_ndec")]
        [LocalizedDisplayName(nameof(Resources.MotorDeceleration)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int Deceleration { get => _Deceleration; set { _Deceleration = value; OnPropertyChanged(); } }
        private int _Deceleration = 409600;

        // 回原点时的加速度
        [JsonProperty("Home_nAcc")]
        [LocalizedDisplayName(nameof(Resources.HomingAcceleration)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int HomeAcceleration { get => _HomeAcceleration; set { _HomeAcceleration = value; OnPropertyChanged(); } }
        private int _HomeAcceleration = 409600;

        [LocalizedDisplayName(nameof(Resources.HomingMethod)), PropertyVisibility(nameof(ShowMotorSettings))]
        public GoHome_WAY GoHomeWay { get => _GoHomeWay; set { _GoHomeWay = value; OnPropertyChanged(); } }
        private GoHome_WAY _GoHomeWay = GoHome_WAY.negative_limit;

        [LocalizedDisplayName(nameof(Resources.HomingTimeout)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int HomeTimeout { get => _HomeTimeout; set { _HomeTimeout = value; OnPropertyChanged(); } }
        private int _HomeTimeout = 5000;

        // 高速回原点
        [JsonProperty("Home_nHighSpeed")]
        [LocalizedDisplayName(nameof(Resources.HighSpeedHoming)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int HomeHightSpeed { get => _HomeHightSpeed; set { _HomeHightSpeed = value; OnPropertyChanged(); } }
        private int _HomeHightSpeed = 2000;

        // 低速回原点
        [JsonProperty("Home_nLowSpeed")]
        [LocalizedDisplayName(nameof(Resources.LowSpeedHoming)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int HomeLowSpeed { get => _HomeLowSpeed; set { _HomeLowSpeed = value; OnPropertyChanged(); } }
        private int _HomeLowSpeed = 2000;

        [LocalizedDisplayName(nameof(Resources.MotorMoveRangeLowerLimit)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int MinPosition { get => _MinPosition; set { _MinPosition = value; OnPropertyChanged(); } }
        private int _MinPosition;

        [LocalizedDisplayName(nameof(Resources.MotorMoveRangeupperLimit)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int MaxPosition { get => _MaxPosition; set { _MaxPosition = value; OnPropertyChanged(); } }
        private int _MaxPosition = 7800;

        [LocalizedDisplayName(nameof(Resources.FocusImageCount)), PropertyVisibility(nameof(ShowMotorSettings))]
        public int AutoFocusSaveImageNum { get => _AutoFocusSaveImageNum; set { _AutoFocusSaveImageNum = value; OnPropertyChanged(); } }
        private int _AutoFocusSaveImageNum = 2;
    }


    [LocalizedDisplayName(nameof(Resources.MotorConfig))]
    public class MotorConfig : MotorConfigBase
    {

        [LocalizedDisplayName(nameof(Resources.ConfigMotor)), Display(Order = -100)]
        public bool IsUseMotor { get => _IsUseMotor; set { _IsUseMotor = value; if(!value) IsCameraLinkage =false; OnPropertyChanged(); OnPropertyChanged(nameof(ShowMotorSettings)); } }
        private bool _IsUseMotor;

        [Browsable(false), JsonIgnore]
        public override bool ShowMotorSettings => IsUseMotor;

        [LocalizedDisplayName(nameof(Resources.CameraLinkage)), Browsable(false), DefaultValue(false)]
        [Description("启用后，电机连接失败会使相机打开失败；关闭时，相机仍可继续取图。")]
        public bool IsCameraLinkage { get => _IsCameraLinkage; set { _IsCameraLinkage = value; OnPropertyChanged(); } }
        private bool _IsCameraLinkage;

        [PropertyVisibility(nameof(IsUseMotor))]
        public MotorVID VID { get => _VID; set { _VID = value; OnPropertyChanged(); } }
        private MotorVID _VID = new MotorVID();

    }

    public class MotorVID : ViewModelBase
    {
        [DisplayName("启用 VID"), Display(Order = -100)]
        public bool IsUseVID { get => _IsUseVID; set { _IsUseVID = value; OnPropertyChanged(); } }
        private bool _IsUseVID ;
        [DisplayName("映射文件"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor)), PropertyVisibility(nameof(IsUseVID))]
        public string MappingFileName { get=>_MappingFileName; set { _MappingFileName = value; OnPropertyChanged(); } }
        private string _MappingFileName = string.Empty;
        [DisplayName("拟合阶数"), PropertyVisibility(nameof(IsUseVID))]
        public int Fit { get => _Fit; set { _Fit = value; OnPropertyChanged(); } } 
        private int _Fit = 5;
    }
}
