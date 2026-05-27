using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.FloatingBall
{
    public class FloatingBallWindowConfig : WindowConfig
    {
    }

    public class DesktopPetConfig : ColorVision.Common.MVVM.ViewModelBase, IConfig
    {
        public static DesktopPetConfig Instance => ConfigService.Instance.GetRequiredService<DesktopPetConfig>();

        [DisplayName("ConfigDesktopPetName")]
        [Category("ConfigDesktopPetCategory")]
        public string PetName { get => _petName; set { _petName = value; OnPropertyChanged(); } }
        private string _petName = "小彩";

        [DisplayName("ConfigDesktopPetAlwaysOnTop")]
        [Category("ConfigDesktopPetCategory")]
        public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; OnPropertyChanged(); } }
        private bool _alwaysOnTop = true;

        [DisplayName("ConfigDesktopPetShowNotifications")]
        [Category("ConfigDesktopPetCategory")]
        public bool ShowNotifications { get => _showNotifications; set { _showNotifications = value; OnPropertyChanged(); } }
        private bool _showNotifications = true;

        [DisplayName("ConfigDesktopPetStartupGreeting")]
        [Category("ConfigDesktopPetCategory")]
        public bool ShowStartupGreeting { get => _showStartupGreeting; set { _showStartupGreeting = value; OnPropertyChanged(); } }
        private bool _showStartupGreeting = true;

        [DisplayName("ConfigDesktopPetEnableIdleTips")]
        [Category("ConfigDesktopPetCategory")]
        public bool EnableIdleTips { get => _enableIdleTips; set { _enableIdleTips = value; OnPropertyChanged(); } }
        private bool _enableIdleTips = true;

        [DisplayName("ConfigDesktopPetIdleTipInterval")]
        [Category("ConfigDesktopPetCategory")]
        public int IdleTipIntervalMinutes { get => _idleTipIntervalMinutes; set { _idleTipIntervalMinutes = value; OnPropertyChanged(); } }
        private int _idleTipIntervalMinutes = 30;

        [DisplayName("ConfigDesktopPetMessageDisplaySeconds")]
        [Category("ConfigDesktopPetCategory")]
        public int MessageDisplaySeconds { get => _messageDisplaySeconds; set { _messageDisplaySeconds = value; OnPropertyChanged(); } }
        private int _messageDisplaySeconds = 6;

        [DisplayName("ConfigDesktopPetScale")]
        [Category("ConfigDesktopPetCategory")]
        public double PetScale { get => _petScale; set { _petScale = value; OnPropertyChanged(); } }
        private double _petScale = 1.0;

        [DisplayName("ConfigDesktopPetOpacity")]
        [Category("ConfigDesktopPetCategory")]
        public double PetOpacity { get => _petOpacity; set { _petOpacity = value; OnPropertyChanged(); } }
        private double _petOpacity = 1.0;

        [DisplayName("ConfigLive2DEnable")]
        [Category("Live2D")]
        public bool EnableLive2DRenderer { get => _enableLive2DRenderer; set { _enableLive2DRenderer = value; OnPropertyChanged(); } }
        private bool _enableLive2DRenderer = true;

        [DisplayName("ConfigLive2DPath")]
        [Category("Live2D")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string Live2DPath { get => _live2DPath; set { _live2DPath = value; OnPropertyChanged(); } }
        private string _live2DPath = string.Empty;

        [DisplayName("ConfigLive2DMaxFps")]
        [Category("Live2D")]
        public int Live2DMaxFps { get => _live2DMaxFps; set { _live2DMaxFps = value; OnPropertyChanged(); } }
        private int _live2DMaxFps = 30;

        [DisplayName("ConfigLive2DRenderScale")]
        [Category("Live2D")]
        public double Live2DRenderScale { get => _live2DRenderScale; set { _live2DRenderScale = value; OnPropertyChanged(); } }
        private double _live2DRenderScale = 0.75;

        [DisplayName("ConfigLive2DMotionEffects")]
        [Category("Live2D")]
        public bool EnableLive2DMotionEffects { get => _enableLive2DMotionEffects; set { _enableLive2DMotionEffects = value; OnPropertyChanged(); } }
        private bool _enableLive2DMotionEffects = true;
    }
}
