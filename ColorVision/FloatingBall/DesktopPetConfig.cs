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

        [DisplayName("宠物素材")]
        [Description("使用 ColorVision 默认素材、Codex 内置素材或兼容 Codex pet.json 的自定义素材。")]
        [Category("宠物素材")]
        [Browsable(false)]
        public string SelectedPetId { get => _selectedPetId; set { _selectedPetId = value; OnPropertyChanged(); } }
        private string _selectedPetId = DesktopPetAssetCatalog.DefaultAssetId;

        [DisplayName("关联 Copilot")]
        [Description("让宠物跟随 Copilot 的运行、等待确认、完成和失败状态。")]
        [Category("Copilot")]
        public bool EnableCopilotIntegration { get => _enableCopilotIntegration; set { _enableCopilotIntegration = value; OnPropertyChanged(); } }
        private bool _enableCopilotIntegration = true;

        [DisplayName("显示 Copilot 提醒")]
        [Description("在 Copilot 需要确认、完成或失败时显示宠物气泡。")]
        [Category("Copilot")]
        [PropertyVisibility(nameof(EnableCopilotIntegration))]
        public bool ShowCopilotNotifications { get => _showCopilotNotifications; set { _showCopilotNotifications = value; OnPropertyChanged(); } }
        private bool _showCopilotNotifications = true;

    }
}
