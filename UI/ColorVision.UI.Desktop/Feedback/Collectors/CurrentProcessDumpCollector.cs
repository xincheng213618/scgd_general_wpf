using ColorVision.Common.NativeMethods;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.UI.Desktop.Feedback.Collectors;

/// <summary>
/// Captures a live full-memory snapshot only when explicitly selected for feedback packaging.
/// </summary>
public sealed class CurrentProcessDumpCollector : IFeedbackLogCollector
{
    private readonly Action<string, int> _writeDump;

    public CurrentProcessDumpCollector() : this(DumpHelper.WriteMiniDump) { }

    internal CurrentProcessDumpCollector(Action<string, int> writeDump) => _writeDump = writeDump;

    public string Name => Properties.Resources.ResourceManager.GetString("FeedbackCurrentProcessDump", CultureInfo.CurrentUICulture) ?? "Current process memory dump";

    public string Description => Properties.Resources.ResourceManager.GetString("FeedbackCurrentProcessDumpDescription", CultureInfo.CurrentUICulture) ?? "Capture a full memory snapshot while packaging and compress it to investigate memory growth.";

    public int Order => 31;

    public bool IsSelectedByDefault => false;

    public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
    {
        string processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "ColorVision";
        string fileName = $"{processName}_{Environment.ProcessId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.dmp";
        string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_ProcessDump_{fileName}");
        try
        {
            _writeDump(tempPath, (int)Diagnostics.MiniDumpType.MiniDumpWithFullMemory);
            return [($"ProcessDump/{fileName}", tempPath)];
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            string errorPath = Path.ChangeExtension(tempPath, ".txt");
            try
            {
                File.WriteAllText(errorPath, $"Current process memory dump collection failed at {DateTimeOffset.Now:O}.{Environment.NewLine}{ex}", Encoding.UTF8);
                return [("ProcessDump/collection-error.txt", errorPath)];
            }
            catch
            {
                TryDeleteFile(errorPath);
                return [];
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { }
    }
}
