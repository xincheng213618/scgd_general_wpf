using System.IO;
using System.Text;

namespace Spectrum.DirectSpectrometer;

internal static class DirectSpectrometerLogger
{
    private static readonly object SyncRoot = new();
    private static StreamWriter? _writer;

    public static void Initialize(string logFilePath)
    {
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        StreamWriter writer;
        try
        {
            writer = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        lock (SyncRoot)
        {
            StreamWriter? previousWriter = _writer;
            _writer = writer;
            try
            {
                previousWriter?.Dispose();
            }
            catch
            {
            }
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

    public static T Measure<T>(string operation, Func<T> action)
    {
        return Measure(operation, action, out _);
    }

    public static T Measure<T>(string operation, Func<T> action, out long elapsedMilliseconds)
    {
        var start = DateTime.Now;
        var threadId = Environment.CurrentManagedThreadId;
        Info($"START | Thread={threadId} | Time={start:yyyy-MM-dd HH:mm:ss.fff} | Operation={operation}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = action();
            stopwatch.Stop();
            elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            var end = DateTime.Now;
            Info($"END   | Thread={threadId} | Time={end:yyyy-MM-dd HH:mm:ss.fff} | Duration={elapsedMilliseconds}ms | Operation={operation} | Result={result}");
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            var end = DateTime.Now;
            Write("ERROR", $"FAIL  | Thread={threadId} | Time={end:yyyy-MM-dd HH:mm:ss.fff} | Duration={elapsedMilliseconds}ms | Operation={operation} | Exception={ex}");
            throw;
        }
    }

    public static void Flush()
    {
        lock (SyncRoot)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }
    }

    public static void Close()
    {
        lock (SyncRoot)
        {
            StreamWriter? writer = _writer;
            _writer = null;
            try
            {
                writer?.Dispose();
            }
            catch
            {
            }
        }
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (SyncRoot)
        {
            try
            {
                _writer?.WriteLine(line);
                if (level == "ERROR")
                    _writer?.Flush();
            }
            catch
            {
            }
        }
    }
}
