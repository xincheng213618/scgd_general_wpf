using ColorVision.UI.Maintenance;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;

namespace ColorVision.Engine.Services;

public sealed class ServiceLogFileMaintenanceProvider : IFileMaintenanceProvider
{
    private static readonly ResourceManager Text = new("ColorVision.Engine.Services.ServiceLogMaintenanceResources", typeof(ServiceLogFileMaintenanceProvider).Assembly);
    private readonly Func<IEnumerable<string>> _directories;
    private string[] _roots = [];

    public ServiceLogFileMaintenanceProvider() : this(() => ServiceLogCollector.GetLogDirectories().Append(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ColorVision", "ServiceHost"))) { }

    internal ServiceLogFileMaintenanceProvider(Func<IEnumerable<string>> directories) => _directories = directories;
    public void PrepareScan() => _roots = _directories().Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public IEnumerable<FileMaintenanceCategory> GetCategories() => [new Category(this)];

    private sealed class Category(ServiceLogFileMaintenanceProvider owner) : FileMaintenanceCategory
    {
        public override string Id => "Engine.ServiceLogs";
        public override string Title => Text.GetString("Title", CultureInfo.CurrentUICulture) ?? "Service logs";
        public override string Description => Text.GetString("Description", CultureInfo.CurrentUICulture) ?? "Historical service logs; keep the newest file in each directory and today's logs.";
        public override int? DefaultRetentionDays => 30;
        public override IReadOnlyList<string> SourceDirectories => owner._roots;

        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken)
        {
            var latest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var issues = new List<string>();
            foreach (string root in owner._roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) continue;
                    var newest = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
                    foreach (string path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var file = new FileInfo(path);
                        string directory = file.DirectoryName!;
                        if (!newest.TryGetValue(directory, out FileInfo? current) || file.LastWriteTimeUtc > current.LastWriteTimeUtc ||
                            (file.LastWriteTimeUtc == current.LastWriteTimeUtc && StringComparer.OrdinalIgnoreCase.Compare(file.Name, current.Name) < 0))
                            newest[directory] = file;
                    }
                    foreach (FileInfo file in newest.Values) latest.Add(file.FullName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    blocked.Add(root);
                    issues.Add($"{root} — {ex.Message}");
                }
            }
            var rules = owner._roots.Select(root => new MaintenanceFileCleanupRule(Id, root, Recursive: true, RetentionDays: retentionDays,
                IsProtected: path => blocked.Contains(root) || latest.Contains(path) || IsTodaysLog(path))).ToArray();
            FileMaintenanceSnapshot snapshot = FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan(rules, cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));
            return snapshot with { Issues = snapshot.Issues.Concat(issues).Distinct().ToArray() };
        }

        private static bool IsTodaysLog(string path)
        {
            try { return string.Equals(Path.GetFileName(path), "ColorVisionServiceHost.log", StringComparison.OrdinalIgnoreCase) || File.GetLastWriteTimeUtc(path) >= DateTime.Today.ToUniversalTime(); }
            catch { return true; }
        }

        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken)
        {
            if (confirmed.Token is not MaintenanceFileCleanupScanResult files) throw new InvalidOperationException("Invalid service log scan.");
            return FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup(files, cancellationToken));
        }
    }
}
