using log4net;
using System.Diagnostics;
using System.IO;

namespace ProjectARVRPro.Exports.Lux;

internal static class LuxDeviceReportWriter
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxDeviceReportWriter));

    // Both directory access and writing can block on a mapped drive or UNC share.
    // Keep the request busy until the write ends; abandoning it on a timer could let
    // a late write overwrite the next request's report for the same SN.
    internal static Task WriteAsync(string path, Action<string> write, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var elapsed = Stopwatch.StartNew();
        log.Info($"LUX 结果保存开始: Path={path}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            cancellationToken.ThrowIfCancellationRequested();
            write(path);
            log.Info($"LUX 结果保存完成: Path={path}, ElapsedMs={elapsed.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            log.Error($"LUX 结果保存失败: Path={path}, ElapsedMs={elapsed.ElapsedMilliseconds}", ex);
            throw;
        }
    }, cancellationToken);
}
