namespace ColorVision.Updater.Logging;

public class UpdateLogger
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLevel;
    
    public UpdateLogger(string logFilePath, LogLevel minLevel = LogLevel.Info)
    {
        _logFilePath = logFilePath;
        _minLevel = minLevel;
        
        // 确保日志目录存在
        var logDir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
    }
    
    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    
    private void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;
        
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level,-7}] {message}";
        
        try
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // 忽略日志写入失败
        }
        
        // 同时输出到控制台
        Console.WriteLine(logEntry);
    }
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}
