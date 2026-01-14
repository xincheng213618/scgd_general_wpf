using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    /// <summary>
    /// 许可证通知配置
    /// </summary>
    public class LicenseNotificationConfig : ViewModelBase, IConfig
    {
        /// <summary>
        /// 提前多少天提示许可证即将过期（默认5天）
        /// </summary>
        [Category("许可证通知"), DisplayName("提前提示天数"), Description("许可证过期前多少天开始提示")]
        public int WarningDaysBeforeExpiry { get => _WarningDaysBeforeExpiry; set { _WarningDaysBeforeExpiry = value; OnPropertyChanged(); } }
        private int _WarningDaysBeforeExpiry = 5;

        /// <summary>
        /// 是否不再显示过期许可证提示
        /// </summary>
        [Category("许可证通知"), DisplayName("是否不再显示过期许可证提示")]
        public bool DontShowExpiredAgain { get => _DontShowExpiredAgain; set { _DontShowExpiredAgain = value; OnPropertyChanged(); } }
        private bool _DontShowExpiredAgain = false;

        /// <summary>
        /// 是否不再显示即将过期许可证提示
        /// </summary>
        [Category("许可证通知"), DisplayName("是否不再显示即将过期许可证提示")]
        public bool DontShowExpiringAgain { get => _DontShowExpiringAgain; set { _DontShowExpiringAgain = value; OnPropertyChanged(); } }
        private bool _DontShowExpiringAgain = false;
    }
}
