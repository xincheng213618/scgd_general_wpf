namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 后台会话清理服务
    /// 定期清理过期的会话
    /// </summary>
    public class SessionCleanupService : IDisposable
    {
        private readonly ISessionService _sessionService;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval;
        private int _isRunning = 0;
        private bool _disposed = false;

        public SessionCleanupService(ISessionService sessionService, TimeSpan? cleanupInterval = null)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromHours(1); // 默认每小时清理一次
            
            // 创建定时器，延迟5分钟后首次执行，然后按照设定的间隔执行
            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                TimeSpan.FromMinutes(5),
                _cleanupInterval
            );
        }

        private async void CleanupCallback(object? state)
        {
            if (_disposed || Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return;

            try
            {
                await _sessionService.CleanupExpiredSessionsAsync();
            }
            catch
            {
                // 清理失败不应影响应用程序，静默处理
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        /// <summary>
        /// 立即执行一次清理
        /// </summary>
        public async Task CleanupNowAsync()
        {
            if (_disposed || Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return;

            try
            {
                await _sessionService.CleanupExpiredSessionsAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
