using ColorVision.UI.Maintenance;
using HostProcess = System.Diagnostics.Process;
using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.Offline;

public sealed class OfflineFileMaintenanceProvider : IFileMaintenanceProvider
{
    private readonly string _root;
    public OfflineFileMaintenanceProvider() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "OfflineData")) { }
    internal OfflineFileMaintenanceProvider(string root) => _root = Path.GetFullPath(root);
    public void PrepareScan() { }
    public IEnumerable<FileMaintenanceCategory> GetCategories() => [new Category(_root)];

    // A whole application session is protected, including queries still completing after a window closes.
    internal static void CreateImportDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        using HostProcess process = HostProcess.GetCurrentProcess();
        File.WriteAllText(directory + ".owner.json", JsonSerializer.Serialize(new Owner(process.Id, process.StartTime.ToUniversalTime().Ticks)));
    }

    internal static bool IsProtected(string directory, int retentionDays)
    {
        try
        {
            if (Directory.GetCreationTimeUtc(directory) >= DateTime.UtcNow.AddDays(-retentionDays)) return true;
            string marker = directory + ".owner.json";
            if (!File.Exists(marker))
            {
                // Older hosts do not write ownership markers. Keep legacy copies while another host may use them.
                foreach (HostProcess process in HostProcess.GetProcessesByName("ColorVision"))
                {
                    using (process) { if (process.Id != Environment.ProcessId && !process.HasExited) return true; }
                }
                return false;
            }
            Owner owner = JsonSerializer.Deserialize<Owner>(File.ReadAllText(marker)) ?? throw new InvalidDataException("Invalid offline ownership marker.");
            if (owner.ProcessId <= 0 || owner.StartTimeUtcTicks <= 0) return true;
            try
            {
                using HostProcess process = HostProcess.GetProcessById(owner.ProcessId);
                return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == owner.StartTimeUtcTicks;
            }
            catch (ArgumentException) { return false; }
        }
        catch { return true; }
    }

    private sealed record Owner(int ProcessId, long StartTimeUtcTicks);

    private sealed class Category(string root) : FileMaintenanceCategory
    {
        private static readonly string[] Names = ["ProjectARVRPro.db", "FlowNodeRecords.db", "SocketMessages.db", "MsgRecords.db"];
        public override string Id => "ProjectARVRPro.OfflineData";
        public override string Title => DisplayText.Get("现场数据副本");
        public override string Description => DisplayText.Get("清理已结束应用会话的过期只读副本，原始反馈资料保留；本次运行导入的副本受保护。");
        public override int? DefaultRetentionDays => 7;
        public override IReadOnlyList<string> SourceDirectories => [root];

        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken)
        {
            var rules = new List<MaintenanceFileCleanupRule>();
            if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
            {
                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                    foreach (string name in Names.SelectMany(name => new[] { name, name + ".export.json" }))
                        rules.Add(new(Id, directory, name, RetentionDays: retentionDays, IsProtected: _ => IsProtected(directory, retentionDays)));
                }
            }
            return FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan(rules, cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));
        }

        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken)
        {
            if (confirmed.Token is not MaintenanceFileCleanupScanResult files) throw new InvalidOperationException("Invalid offline file scan.");
            return FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup(files, cancellationToken));
        }
    }
}
