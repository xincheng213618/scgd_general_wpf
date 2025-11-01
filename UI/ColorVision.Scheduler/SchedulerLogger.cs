using Microsoft.Extensions.Logging;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// Simple logger wrapper for ColorVision.Scheduler
    /// Uses ILogger if available, falls back to debug output
    /// </summary>
    public class SchedulerLogger
    {
        private readonly ILogger? _logger;
        private readonly string _categoryName;

        public SchedulerLogger(string categoryName, ILogger? logger = null)
        {
            _categoryName = categoryName;
            _logger = logger;
        }

        public void LogInformation(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation("[{CategoryName}] {Message}", _categoryName, message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] [{_categoryName}] {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (_logger != null)
            {
                _logger.LogWarning("[{CategoryName}] {Message}", _categoryName, message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] [{_categoryName}] {message}");
            }
        }

        public void LogError(string message, Exception? exception = null)
        {
            if (_logger != null)
            {
                _logger.LogError(exception, "[{CategoryName}] {Message}", _categoryName, message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] [{_categoryName}] {message}");
                if (exception != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
                }
            }
        }

        public void LogDebug(string message)
        {
            if (_logger != null)
            {
                _logger.LogDebug("[{CategoryName}] {Message}", _categoryName, message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] [{_categoryName}] {message}");
            }
        }
    }
}
