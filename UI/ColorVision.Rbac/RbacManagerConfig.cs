#pragma warning disable CA1805
using ColorVision.Common.MVVM;
using ColorVision.Rbac.Dtos;
using ColorVision.UI;

namespace ColorVision.Rbac
{

    public class RbacManagerConfig : ViewModelBase, IConfig
    {
        public static RbacManagerConfig Instance => ConfigService.Instance.GetRequiredService<RbacManagerConfig>();

        public LoginResultDto LoginResult { get => _LoginResult; set { _LoginResult = value; OnPropertyChanged(); } }
        private LoginResultDto _LoginResult = new LoginResultDto();

        /// <summary>
        /// 当前会话Token
        /// </summary>
        public string SessionToken { get => _SessionToken; set { _SessionToken = value; OnPropertyChanged(); } }
        private string _SessionToken = string.Empty;

        /// <summary>
        /// 是否记住登录状态
        /// </summary>
        public bool RememberMe { get => _RememberMe; set { _RememberMe = value; OnPropertyChanged(); } }
        private bool _RememberMe = false;

        /// <summary>
        /// 保存的用户名（用于自动登录时显示）
        /// </summary>
        public string SavedUsername { get => _SavedUsername; set { _SavedUsername = value; OnPropertyChanged(); } }
        private string _SavedUsername = string.Empty;

        /// <summary>
        /// 本机累计启动 ColorVision 的次数。
        /// </summary>
        public long ApplicationLaunchCount { get => _ApplicationLaunchCount; set { _ApplicationLaunchCount = value; OnPropertyChanged(); } }
        private long _ApplicationLaunchCount;

        /// <summary>
        /// 本机首次记录到的 ColorVision 启动时间。
        /// </summary>
        public DateTime? FirstApplicationLaunchAt { get => _FirstApplicationLaunchAt; set { _FirstApplicationLaunchAt = value; OnPropertyChanged(); } }
        private DateTime? _FirstApplicationLaunchAt;

        /// <summary>
        /// 本机最近一次 ColorVision 启动时间。
        /// </summary>
        public DateTime? LastApplicationLaunchAt { get => _LastApplicationLaunchAt; set { _LastApplicationLaunchAt = value; OnPropertyChanged(); } }
        private DateTime? _LastApplicationLaunchAt;

        /// <summary>
        /// 已完整结束会话的累计运行秒数；当前会话时长由 ApplicationUsageTracker 动态叠加。
        /// </summary>
        public long AccumulatedRunSeconds { get => _AccumulatedRunSeconds; set { _AccumulatedRunSeconds = value; OnPropertyChanged(); } }
        private long _AccumulatedRunSeconds;
    }
}

