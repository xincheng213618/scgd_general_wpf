using System.IO;
using System.Text;

namespace Spectrum.DirectSpectrometer;

internal static class DirectSpectrometerLogger
{
    private static readonly object SyncRoot = new();
    private static string? _logFilePath;

    public static void Initialize(string logFilePath)
    {
        _logFilePath = logFilePath;
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Write("INFO", "Logger initialized");
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception ex)
    {
        Write("ERROR", $"{message}\r\n{ex}");
    }

    public static string Measure(string operation, Func<string> action)
    {
        var start = DateTime.Now;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var threadId = Environment.CurrentManagedThreadId;
        Info($"START | Thread={threadId} | Time={start:yyyy-MM-dd HH:mm:ss.fff} | Operation={operation}");

        try
        {
            var result = action();
            stopwatch.Stop();
            var end = DateTime.Now;
            Info($"END   | Thread={threadId} | Time={end:yyyy-MM-dd HH:mm:ss.fff} | Duration={stopwatch.ElapsedMilliseconds}ms | Operation={operation} | Result={result}");
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var end = DateTime.Now;
            Write("ERROR", $"FAIL  | Thread={threadId} | Time={end:yyyy-MM-dd HH:mm:ss.fff} | Duration={stopwatch.ElapsedMilliseconds}ms | Operation={operation} | Exception={ex}");
            throw;
        }
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (SyncRoot)
        {
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}