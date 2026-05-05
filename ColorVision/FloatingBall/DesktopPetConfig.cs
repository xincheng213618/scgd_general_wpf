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

        [DisplayName("宠物名称")]
        [Category("桌面宠物")]
        public string PetName { get => _petName; set { _petName = value; OnPropertyChanged(); } }
        private string _petName = "小彩";

        [DisplayName("始终置顶")]
        [Category("桌面宠物")]
        public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; OnPropertyChanged(); } }
        private bool _alwaysOnTop = true;

        [DisplayName("显示消息通知")]
        [Category("桌面宠物")]
        public bool ShowNotifications { get => _showNotifications; set { _showNotifications = value; OnPropertyChanged(); } }
        private bool _showNotifications = true;

        [DisplayName("启动问候")]
        [Category("桌面宠物")]
        public bool ShowStartupGreeting { get => _showStartupGreeting; set { _showStartupGreeting = value; OnPropertyChanged(); } }
        private bool _showStartupGreeting = true;

        [DisplayName("启用待机提示")]
        [Category("桌面宠物")]
        public bool EnableIdleTips { get => _enableIdleTips; set { _enableIdleTips = value; OnPropertyChanged(); } }
        private bool _enableIdleTips = true;

        [DisplayName("待机提示间隔分钟")]
        [Category("桌面宠物")]
        public int IdleTipIntervalMinutes { get => _idleTipIntervalMinutes; set { _idleTipIntervalMinutes = value; OnPropertyChanged(); } }
        private int _idleTipIntervalMinutes = 30;

        [DisplayName("消息显示秒数")]
        [Category("桌面宠物")]
        public int MessageDisplaySeconds { get => _messageDisplaySeconds; set { _messageDisplaySeconds = value; OnPropertyChanged(); } }
        private int _messageDisplaySeconds = 6;

        [DisplayName("宠物缩放")]
        [Category("桌面宠物")]
        public double PetScale { get => _petScale; set { _petScale = value; OnPropertyChanged(); } }
        private double _petScale = 1.0;

        [DisplayName("宠物透明度")]
        [Category("桌面宠物")]
        public double PetOpacity { get => _petOpacity; set { _petOpacity = value; OnPropertyChanged(); } }
        private double _petOpacity = 1.0;

        [DisplayName("启用 Live2D 渲染")]
        [Category("Live2D")]
        public bool EnableLive2DRenderer { get => _enableLive2DRenderer; set { _enableLive2DRenderer = value; OnPropertyChanged(); } }
        private bool _enableLive2DRenderer = true;

        [DisplayName("Live2D 模型或 HTML 路径")]
        [Category("Live2D")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string Live2DPath { get => _live2DPath; set { _live2DPath = value; OnPropertyChanged(); } }
        private string _live2DPath = string.Empty;

        [DisplayName("Live2D 最大帧率")]
        [Category("Live2D")]
        public int Live2DMaxFps { get => _live2DMaxFps; set { _live2DMaxFps = value; OnPropertyChanged(); } }
        private int _live2DMaxFps = 30;

        [DisplayName("Live2D 渲染分辨率")]
        [Category("Live2D")]
        public double Live2DRenderScale { get => _live2DRenderScale; set { _live2DRenderScale = value; OnPropertyChanged(); } }
        private double _live2DRenderScale = 0.75;

        [DisplayName("Live2D 轻量动效")]
        [Category("Live2D")]
        public bool EnableLive2DMotionEffects { get => _enableLive2DMotionEffects; set { _enableLive2DMotionEffects = value; OnPropertyChanged(); } }
        private bool _enableLive2DMotionEffects = true;
    }
}
