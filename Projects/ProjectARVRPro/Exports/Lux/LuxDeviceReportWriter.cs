using log4net;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace ProjectARVRPro.Exports.Lux;

internal sealed record LuxCsvWriteContext(string SN, int? BatchId, string Station, string? FlowGroup, string? SocketCode)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxCsvWriteContext));

    internal void LogResult(string path, long elapsedMs, Exception? error = null)
    {
        string outcome = error switch
        {
            null => "Succeeded",
            OperationCanceledException => "Canceled",
            _ => "Failed"
        };
        string message = JsonConvert.SerializeObject(new
        {
            Event = "LuxCsvWrite",
            Timestamp = DateTimeOffset.Now,
            Machine = Environment.MachineName,
            Station,
            FlowGroup,
            SocketCode,
            SN,
            BatchId = BatchId > 0 ? BatchId : null,
            Path = path,
            ElapsedMs = elapsedMs,
            Outcome = outcome
        });
        if (error is OperationCanceledException) log.Warn(message, error);
        else if (error != null) log.Error(message, error);
        else log.Info(message);
    }
}

internal static class LuxDeviceReportWriter
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxDeviceReportWriter));

    // Both directory access and writing can block on a mapped drive or UNC share.
    // Keep the request busy until the write ends; abandoning it on a timer could let
    // a late write overwrite the next request's report for the same SN.
    internal static Task WriteAsync(string path, Action<string> write, LuxCsvWriteContext context, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            log.Info($"LUX 结果保存开始: Path={JsonConvert.ToString(path)}");
            Write(path, write, context, cancellationToken);
        }, cancellationToken);

    internal static void Write(string path, Action<string> write, LuxCsvWriteContext context, CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicCsvFile.Write(path, write, cancellationToken);
        }
        catch (Exception ex)
        {
            context.LogResult(path, elapsed.ElapsedMilliseconds, ex);
            throw;
        }
        context.LogResult(path, elapsed.ElapsedMilliseconds);
    }
}
