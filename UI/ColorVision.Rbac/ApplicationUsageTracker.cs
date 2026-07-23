using ColorVision.UI;
using System.Diagnostics;

namespace ColorVision.Rbac
{
    /// <summary>
    /// 记录本机应用启动次数和累计运行时长。当前会话在退出时写入累计值。
    /// </summary>
    public static class ApplicationUsageTracker
    {
        private static readonly object Locker = new();
        private static DateTime? _sessionStartedAt;

        public static void StartSession()
        {
            lock (Locker)
            {
                if (_sessionStartedAt.HasValue)
                    return;

                var now = DateTime.Now;
                _sessionStartedAt = now;
                try
                {
                    var config = RbacManagerConfig.Instance;
                    config.ApplicationLaunchCount = Math.Max(0, config.ApplicationLaunchCount) + 1;
                    config.FirstApplicationLaunchAt ??= now;
                    config.LastApplicationLaunchAt = now;
                    ConfigService.Instance.Save<RbacManagerConfig>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Application usage start tracking failed: {ex.Message}");
                }
            }
        }

        public static void StopSession()
        {
            lock (Locker)
            {
                if (!_sessionStartedAt.HasValue)
                    return;

                var sessionStartedAt = _sessionStartedAt.Value;
                _sessionStartedAt = null;
                try
                {
                    var elapsed = DateTime.Now - sessionStartedAt;
                    var config = RbacManagerConfig.Instance;
                    config.AccumulatedRunSeconds = Math.Max(0, config.AccumulatedRunSeconds) + Math.Max(0, (long)elapsed.TotalSeconds);
                    ConfigService.Instance.Save<RbacManagerConfig>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Application usage stop tracking failed: {ex.Message}");
                }
            }
        }

        public static TimeSpan GetCurrentSessionDuration()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                return DateTime.Now - process.StartTime;
            }
            catch
            {
                lock (Locker)
                    return _sessionStartedAt.HasValue ? DateTime.Now - _sessionStartedAt.Value : TimeSpan.Zero;
            }
        }

        public static TimeSpan GetTotalRunDuration()
        {
            lock (Locker)
            {
                var currentSession = GetCurrentSessionDuration();
                try
                {
                    var completedSessions = TimeSpan.FromSeconds(Math.Max(0, RbacManagerConfig.Instance.AccumulatedRunSeconds));
                    return completedSessions + currentSession;
                }
                catch
                {
                    return currentSession;
                }
            }
        }
    }
}
