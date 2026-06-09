#pragma warning disable CA1805
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
        [Display(Name = "Engine_PG_WarningDays", GroupName = "Engine_PG_LicenseNotification", Description = "Engine_PG_WarningDaysDesc", ResourceType = typeof(Properties.Resources))]
        public int WarningDaysBeforeExpiry { get => _WarningDaysBeforeExpiry; set { _WarningDaysBeforeExpiry = value; OnPropertyChanged(); } }
        private int _WarningDaysBeforeExpiry = 5;

        /// <summary>
        /// 是否不再显示过期许可证提示
        /// </summary>
        [Display(Name = "Engine_PG_DontShowExpired", GroupName = "Engine_PG_LicenseNotification", ResourceType = typeof(Properties.Resources))]
        public bool DontShowExpiredAgain { get => _DontShowExpiredAgain; set { _DontShowExpiredAgain = value; OnPropertyChanged(); } }
        private bool _DontShowExpiredAgain = false;

        /// <summary>
        /// 是否不再显示即将过期许可证提示
        /// </summary>
        [Display(Name = "Engine_PG_DontShowExpiring", GroupName = "Engine_PG_LicenseNotification", ResourceType = typeof(Properties.Resources))]
        public bool DontShowExpiringAgain { get => _DontShowExpiringAgain; set { _DontShowExpiringAgain = value; OnPropertyChanged(); } }
        private bool _DontShowExpiringAgain = false;
    }
}
