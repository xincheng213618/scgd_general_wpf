using ColorVision.Database;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;

namespace ProjectARVRPro.Offline;

/// <summary>One imported set of diagnostic files; never changes live database paths or configuration.</summary>
public sealed class ArvrOfflineDataSource
{
    private const long ParallelArchiveEntryMinimumBytes = 32 * 1024 * 1024;
    private static readonly string[] DatabaseNames = ["ProjectARVRPro.db", "FlowNodeRecords.db", "SocketMessages.db", "MsgRecords.db"];
    public string SourcePath { get; }
    public string DirectoryPath { get; }
    public string Label { get; }
    public string ProjectDatabasePath => Path.Combine(DirectoryPath, DatabaseNames[0]);
    public string? FlowDatabasePath => FindDatabase("FlowNodeRecords.db");
    public string? SocketDatabasePath => FindDatabase("SocketMessages.db");
    public string? MqttDatabasePath => FindDatabase("MsgRecords.db");
    public DateTime LatestDate { get; }
    public ResultStatisticsDataStore Statistics { get; }
    public string Description => $"现场只读：{Label} · 节点库{(FlowDatabasePath != null ? "已包含" : "未提供")} · Socket{(SocketDatabasePath != null ? "已包含" : "未提供")} · MQTT{(MqttDatabasePath != null ? "已包含" : "未提供")}";

    private ArvrOfflineDataSource(string sourcePath, string directoryPath, string? aggregateLabel)
    {
        SourcePath = sourcePath;
        DirectoryPath = directoryPath;
        var parent = new DirectoryInfo(System.IO.Directory.Exists(sourcePath) ? sourcePath : Path.GetDirectoryName(sourcePath)!);
        if (parent.Name.Equals("Database", StringComparison.OrdinalIgnoreCase) && parent.Parent != null) parent = parent.Parent;
        Label = aggregateLabel ?? (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : System.IO.Directory.Exists(sourcePath) ? parent.Name : $"{parent.Name} / {Path.GetFileName(sourcePath)}");
        Statistics = new ResultStatisticsDataStore(ProjectDatabasePath, readOnly: true);
        var source = new ReadOnlySqliteDatabase(ProjectDatabasePath);
        using var db = source.OpenClient();
        object? latest = db.Ado.GetScalar("SELECT MAX(UpdateTime) FROM ObjectiveTestResultRecord");
        LatestDate = DateTime.TryParse(Convert.ToString(latest, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.Date : DateTime.Today;
    }

    public static ArvrOfflineDataSource Open(string path, string? cacheRoot = null)
    {
        string sourcePath = Path.GetFullPath(path);
        string root = Path.GetFullPath(cacheRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "OfflineData"));
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        string? aggregateLabel = null;
        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var candidates = archive.Entries.Where(entry => entry.Name.Equals(DatabaseNames[0], StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count != 1) throw new InvalidDataException("反馈包需包含一份 ProjectARVRPro.db。请确认现场项目已更新并勾选 ARVRPro 测试记录。");
            string prefix = candidates[0].FullName[..^candidates[0].Name.Length];
            var files = new List<(string Name, ZipArchiveEntry? Database, ZipArchiveEntry? Status)>();
            foreach (string name in DatabaseNames)
            {
                var matching = archive.Entries.Where(entry => entry.FullName.Equals(prefix + name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matching.Count > 1) throw new InvalidDataException($"反馈包中存在重复的 {name}。");
                files.Add((name, matching.SingleOrDefault(), archive.GetEntry(prefix + name + ".export.json")));
            }
            OfflineFileMaintenanceProvider.CreateImportDirectory(directory);
            ExtractArchiveFiles(sourcePath, directory, files);
        }
        else
        {
            bool isDirectory = System.IO.Directory.Exists(sourcePath);
            string sourceDirectory = isDirectory ? sourcePath : Path.GetDirectoryName(sourcePath)!;
            bool copyPublishedGeneration = false;
            if (isDirectory)
            {
                sourceDirectory = FeedbackAggregateDirectory.Resolve(sourceDirectory, out aggregateLabel);
                string nested = Path.Combine(sourceDirectory, "Database");
                if (!File.Exists(Path.Combine(sourceDirectory, DatabaseNames[0])) && System.IO.Directory.Exists(nested))
                    sourceDirectory = nested;
                else if (aggregateLabel != null)
                    copyPublishedGeneration = DatabaseNames.All(name => !HasSqliteSidecar(Path.Combine(sourceDirectory, name)));
            }
            string projectSource = isDirectory ? Path.Combine(sourceDirectory, DatabaseNames[0]) : sourcePath;
            if (!File.Exists(projectSource)) throw new FileNotFoundException("所选资料中没有 ProjectARVRPro.db。", projectSource);
            OfflineFileMaintenanceProvider.CreateImportDirectory(directory);
            foreach (string name in DatabaseNames)
            {
                string original = name == DatabaseNames[0] ? projectSource : Path.Combine(sourceDirectory, name);
                if (!File.Exists(original)) continue;
                string target = Path.Combine(directory, name);
                if (copyPublishedGeneration)
                {
                    File.Copy(original, target);
                    // Keep the app-owned copy removable even when the published file is marked read-only.
                    FileAttributes attributes = File.GetAttributes(target);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(target, attributes & ~FileAttributes.ReadOnly);
                }
                else
                {
                    BackupDatabase(original, target);
                }
                if (File.Exists(original + ".export.json")) File.Copy(original + ".export.json", Path.Combine(directory, name + ".export.json"));
            }
        }
        return new ArvrOfflineDataSource(sourcePath, directory, aggregateLabel);
    }

    private static void ExtractArchiveFiles(string sourcePath, string directory,
        IReadOnlyList<(string Name, ZipArchiveEntry? Database, ZipArchiveEntry? Status)> files)
    {
        var large = files.Where(file => file.Database?.CompressedLength >= ParallelArchiveEntryMinimumBytes)
            .OrderByDescending(file => file.Database!.CompressedLength).ToArray();
        if (large.Length < 2)
        {
            foreach (var file in files)
                Extract(file.Name, file.Database, file.Status);
            return;
        }

        // Keep the first archive open with read sharing and let the two workers seek independently.
        var largest = large[0];
        using var secondStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024);
        using var secondArchive = new ZipArchive(secondStream, ZipArchiveMode.Read);
        ZipArchiveEntry secondEntry = secondArchive.GetEntry(largest.Database!.FullName)
            ?? throw new InvalidDataException($"反馈包缺少 {largest.Name}。");
        ZipArchiveEntry? secondStatus = largest.Status == null ? null : secondArchive.GetEntry(largest.Status.FullName);
        try
        {
            Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 2 },
                () =>
                {
                    foreach (var file in files)
                        if (!ReferenceEquals(file.Database, largest.Database))
                            Extract(file.Name, file.Database, file.Status);
                },
                () => Extract(largest.Name, secondEntry, secondStatus));
        }
        catch (AggregateException error)
        {
            ExceptionDispatchInfo.Capture(error.InnerExceptions[0]).Throw();
        }

        void Extract(string name, ZipArchiveEntry? database, ZipArchiveEntry? status)
        {
            database?.ExtractToFile(Path.Combine(directory, name));
            status?.ExtractToFile(Path.Combine(directory, name + ".export.json"));
        }
    }

    private static bool HasSqliteSidecar(string path) =>
        File.Exists(path + "-wal") || File.Exists(path + "-shm") || File.Exists(path + "-journal");

    private static void BackupDatabase(string original, string target)
    {
        // SQLite backup includes committed WAL content from ordinary files and directories.
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = original, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString());
        source.Open();
        destination.Open();
        // The destination is a new, rebuildable cache copy. Keep SQLite journaling enabled.
        using var command = destination.CreateCommand();
        command.CommandText = "PRAGMA synchronous=OFF";
        command.ExecuteNonQuery();
        source.BackupDatabase(destination);
    }

    public string ResolveFlowSerialNumber(ProjectARVRReuslt result) => ArvrFlowAnalysisHistory.ResolveSerialNumber(
        FlowDatabasePath ?? throw new FileNotFoundException("本份资料没有 FlowNodeRecords.db。"), result);

    private string? FindDatabase(string name)
    {
        string path = Path.Combine(DirectoryPath, name);
        return File.Exists(path) ? path : null;
    }
}
