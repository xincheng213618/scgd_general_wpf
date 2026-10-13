using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Desktop.Diagnostics;

public sealed class CrashDumpFileMaintenanceProvider : IFileMaintenanceProvider
{
    private readonly Func<(string Folder, string Executable)> _configuration;
    private string _folder = string.Empty;
    private string _executable = string.Empty;

    public CrashDumpFileMaintenanceProvider() : this(() =>
    {
        var configuration = new CrashDumpConfiguration();
        return (configuration.DumpFolder, configuration.ProcessExecutableName);
    }) { }

    internal CrashDumpFileMaintenanceProvider(Func<(string Folder, string Executable)> configuration) => _configuration = configuration;

    public void PrepareScan() => (_folder, _executable) = _configuration();
    public IEnumerable<FileMaintenanceCategory> GetCategories() => [new Category(this)];

    private sealed class Category(CrashDumpFileMaintenanceProvider owner) : FileMaintenanceCategory
    {
        public override string Id => "Desktop.CrashDumps";
        public override string Title => CrashDumpText.Get("崩溃转储");
        public override string Description => CrashDumpText.Get("仅清理保存目录中属于当前程序的历史 Dump，保留其他程序的转储。");
        public override int? DefaultRetentionDays => 30;
        public override IReadOnlyList<string> SourceDirectories => [owner._folder];

        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken) =>
            FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan(
                [new(Id, owner._folder, "*.dmp", RetentionDays: retentionDays, IsProtected: path => !CrashDumpFileCollector.IsColorVisionDump(Path.GetFileName(path), owner._executable))],
                cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));

        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken)
        {
            if (confirmed.Token is not MaintenanceFileCleanupScanResult files) throw new InvalidOperationException("Invalid crash dump scan.");
            return FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup(files, cancellationToken));
        }
    }
}
