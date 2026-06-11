using ColorVision.UI;
using log4net;
using System.Windows;
using System.Windows.Threading;

namespace ProjectKB.Auth
{
    /// <summary>
    /// ProjectKB 插件内权限管理器
    /// 管理员登录/登出、空闲超时自动登出
    /// </summary>
    public class KBAuthManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(KBAuthManager));

        private static KBAuthManager? _instance;
        private static readonly object _locker = new();
        public static KBAuthManager GetInstance() { lock (_locker) { _instance ??= new KBAuthManager(); return _instance; } }

        private DispatcherTimer? _idleTimer;
        private readonly KBAuthConfig _config;

        /// <summary>
        /// 当前是否为管理员模式
        /// </summary>
        public bool IsAdmin { get => _isAdmin; private set { if (_isAdmin == value) return; _isAdmin = value; IsAdminChanged?.Invoke(this, EventArgs.Empty); } }
        private bool _isAdmin;

        /// <summary>
        /// 权限状态变更事件
        /// </summary>
        public event EventHandler? IsAdminChanged;

        /// <summary>
        /// 登出时触发（用于显示提示）
        /// </summary>
        public event EventHandler? AutoLoggedOut;

        public KBAuthManager()
        {
            _config = KBAuthConfig.Instance;
            _config.EnsureInitialized();
        }

        /// <summary>
        /// 登录（验证密码）
        /// </summary>
        public bool Login(string password)
        {
            if (_config.VerifyPassword(password))
            {
                IsAdmin = true;
                StartIdleTimer();
                ResetIdleTimer();
                log.Info("管理员登录成功");
                return true;
            }

            log.Warn("管理员登录失败：密码错误");
            return false;
        }

        /// <summary>
        /// 登出
        /// </summary>
        public void Logout()
        {
            if (!IsAdmin) return;

            IsAdmin = false;
            StopIdleTimer();
            log.Info("管理员已登出，恢复产线模式");
        }

        /// <summary>
        /// 修改管理员密码
        /// </summary>
        public bool ChangePassword(string oldPassword, string newPassword)
        {
            if (!IsAdmin) return false;

            bool result = _config.ChangePassword(oldPassword, newPassword);
            if (result)
                log.Info("管理员密码已修改");
            else
                log.Warn("修改密码失败：旧密码错误");
            return result;
        }

        /// <summary>
        /// 重置空闲计时器（用户交互时调用）
        /// </summary>
        public void ResetIdleTimer()
        {
            if (!IsAdmin) return;

            if (_idleTimer != null)
            {
                _idleTimer.Stop();
                var timeout = GetTimeout();
                if (timeout > TimeSpan.Zero)
                {
                    _idleTimer.Interval = timeout;
                    _idleTimer.Start();
                }
            }
        }

        /// <summary>
        /// 获取当前超时配置（分钟）
        /// </summary>
        public int IdleTimeoutMinutes
        {
            get => _config.IdleTimeoutMinutes;
            set
            {
                _config.IdleTimeoutMinutes = value;
                ConfigService.Instance.SaveConfigs();
                if (IsAdmin)
                {
                    RestartIdleTimer();
                }
            }
        }

        private TimeSpan GetTimeout()
        {
            int minutes = _config.IdleTimeoutMinutes;
            return minutes > 0 ? TimeSpan.FromMinutes(minutes) : TimeSpan.Zero;
        }

        private void StartIdleTimer()
        {
            StopIdleTimer();

            var timeout = GetTimeout();
            if (timeout <= TimeSpan.Zero) return;

            _idleTimer = new DispatcherTimer
            {
                Interval = timeout
            };
            _idleTimer.Tick += IdleTimer_Tick;
            _idleTimer.Start();
        }

        private void StopIdleTimer()
        {
            if (_idleTimer != null)
            {
                _idleTimer.Tick -= IdleTimer_Tick;
                _idleTimer.Stop();
                _idleTimer = null;
            }
        }

        private void RestartIdleTimer()
        {
            if (IsAdmin)
            {
                StartIdleTimer();
                ResetIdleTimer();
            }
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            log.Info($"空闲超时（{_config.IdleTimeoutMinutes}分钟），自动登出");
            Logout();
            AutoLoggedOut?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopIdleTimer();
            IsAdmin = false;
        }
    }
}
