using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace ProjectARVRPro.Offline;

/// <summary>One imported set of diagnostic files; never changes live database paths or configuration.</summary>
public sealed class ArvrOfflineDataSource
{
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

    private ArvrOfflineDataSource(string sourcePath, string directoryPath)
    {
        SourcePath = sourcePath;
        DirectoryPath = directoryPath;
        var parent = new DirectoryInfo(System.IO.Directory.Exists(sourcePath) ? sourcePath : Path.GetDirectoryName(sourcePath)!);
        if (parent.Name.Equals("Database", StringComparison.OrdinalIgnoreCase) && parent.Parent != null) parent = parent.Parent;
        Label = Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : System.IO.Directory.Exists(sourcePath) ? parent.Name : $"{parent.Name} / {Path.GetFileName(sourcePath)}";
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
        if (Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var candidates = archive.Entries.Where(entry => entry.Name.Equals(DatabaseNames[0], StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count != 1) throw new InvalidDataException("反馈包需包含一份 ProjectARVRPro.db。请确认现场项目已更新并勾选 ARVRPro 测试记录。");
            string prefix = candidates[0].FullName[..^candidates[0].Name.Length];
            System.IO.Directory.CreateDirectory(directory);
            foreach (string name in DatabaseNames)
            {
                var matching = archive.Entries.Where(entry => entry.FullName.Equals(prefix + name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matching.Count > 1) throw new InvalidDataException($"反馈包中存在重复的 {name}。");
                if (matching.Count == 1) matching[0].ExtractToFile(Path.Combine(directory, name));
                ZipArchiveEntry? status = archive.GetEntry(prefix + name + ".export.json");
                status?.ExtractToFile(Path.Combine(directory, name + ".export.json"));
            }
        }
        else
        {
            bool isDirectory = System.IO.Directory.Exists(sourcePath);
            string sourceDirectory = isDirectory ? sourcePath : Path.GetDirectoryName(sourcePath)!;
            if (isDirectory && !File.Exists(Path.Combine(sourceDirectory, DatabaseNames[0])) && System.IO.Directory.Exists(Path.Combine(sourceDirectory, "Database")))
                sourceDirectory = Path.Combine(sourceDirectory, "Database");
            string projectSource = isDirectory ? Path.Combine(sourceDirectory, DatabaseNames[0]) : sourcePath;
            if (!File.Exists(projectSource)) throw new FileNotFoundException("所选资料中没有 ProjectARVRPro.db。", projectSource);
            System.IO.Directory.CreateDirectory(directory);
            foreach (string name in DatabaseNames)
            {
                string original = name == DatabaseNames[0] ? projectSource : Path.Combine(sourceDirectory, name);
                if (!File.Exists(original)) continue;
                // SQLite's backup captures committed WAL content; copying a live .db file alone can lose records.
                using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = original, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, name), Pooling = false }.ToString());
                source.Open(); destination.Open(); source.BackupDatabase(destination);
                if (File.Exists(original + ".export.json")) File.Copy(original + ".export.json", Path.Combine(directory, name + ".export.json"));
            }
        }
        return new ArvrOfflineDataSource(sourcePath, directory);
    }

    public string ResolveFlowSerialNumber(ProjectARVRReuslt result)
    {
        if (string.IsNullOrWhiteSpace(result.SN) || result.BatchId <= 0)
            throw new InvalidDataException("此条 PG 记录缺少 SN 或批次号，无法关联节点。");
        var source = new ReadOnlySqliteDatabase(FlowDatabasePath ?? throw new InvalidOperationException("本份资料没有 FlowNodeRecords.db。"));
        using var db = source.OpenClient();
        string[] serials = source.Query<FlowNodeRecord>(db).Where(row => row.BatchId == result.BatchId).Select(row => row.SerialNumber).ToList()
            .Where(serial => serial == result.SN || serial?.StartsWith(result.SN + "_", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal).ToArray();
        return serials.Length == 1 ? serials[0] : throw new InvalidDataException(serials.Length == 0
            ? "配套节点库中没有匹配此批次和 SN 的记录。请检查反馈时间范围。"
            : "此批次和 SN 对应多个运行标识，无法确定唯一关联。请核对资料来源。");
    }

    private string? FindDatabase(string name)
    {
        string path = Path.Combine(DirectoryPath, name);
        return File.Exists(path) ? path : null;
    }
}
