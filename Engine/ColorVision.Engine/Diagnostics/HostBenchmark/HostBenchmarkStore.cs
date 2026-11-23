using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

internal sealed record HostBenchmarkHistory(Guid? ReferenceId, HostBenchmarkRun[] Runs)
{
    public int SchemaVersion { get; init; } = 1;
}

internal sealed class HostBenchmarkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string historyPath;
    private readonly string lockName;

    internal HostBenchmarkStore(string? path = null)
    {
        historyPath = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ColorVision", "HostBenchmark", "history.json"));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(historyPath.ToUpperInvariant()));
        lockName = "Local\\ColorVision.HostBenchmark." + Convert.ToHexString(hash);
    }

    internal HostBenchmarkHistory Load() => WithLock(ReadHistory);

    internal HostBenchmarkHistory Add(HostBenchmarkRun run) => WithLock(() =>
    {
        HostBenchmarkHistory history = ReadHistory();
        if (history.Runs.Any(item => item.Id == run.Id)) return history;

        var updated = new HostBenchmarkHistory(history.ReferenceId ?? run.Id,
            history.Runs.Append(run).OrderByDescending(item => item.MeasuredAtUtc).ToArray());
        WriteJson(historyPath, updated);
        return updated;
    });

    internal HostBenchmarkHistory SetReference(Guid id) => WithLock(() =>
    {
        HostBenchmarkHistory history = ReadHistory();
        FindRun(history, id);
        HostBenchmarkHistory updated = history with { ReferenceId = id };
        WriteJson(historyPath, updated);
        return updated;
    });

    internal void MarkShareAttempted(Guid id) => WithLock(() =>
    {
        HostBenchmarkHistory history = ReadHistory();
        HostBenchmarkRun run = FindRun(history, id);
        if (run.ShareAttempted || run.PublicShare is not null) return history;
        var updated = history with
        {
            Runs = history.Runs.Select(item => item.Id == id ? item with { ShareAttempted = true } : item).ToArray()
        };
        WriteJson(historyPath, updated);
        return updated;
    });

    internal void MarkPublicShare(Guid id, HostBenchmarkPublicShare share) => WithLock(() =>
    {
        HostBenchmarkHistory history = ReadHistory();
        HostBenchmarkRun run = FindRun(history, id);
        if (run.PublicShare == share) return history;
        var updated = history with
        {
            Runs = history.Runs.Select(item => item.Id == id ? item with { PublicShare = share } : item).ToArray()
        };
        WriteJson(historyPath, updated);
        return updated;
    });

    internal void Export(string path, Guid id)
    {
        string target = Path.GetFullPath(path);
        if (string.Equals(target, historyPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("导出文件不能覆盖基准测试历史。", nameof(path));

        HostBenchmarkHistory history = Load();
        HostBenchmarkRun run = FindRun(history, id);
        HostBenchmarkRun? reference = history.Runs.FirstOrDefault(item => item.Id == history.ReferenceId);
        HostBenchmarkComparison? standard = HostBenchmarkScoring.CompareToStandard(run);
        var export = new HostBenchmarkExport(1, run, reference, HostBenchmarkScoring.Compare(run, reference),
            standard is null ? null : HostBenchmarkScoring.StandardVersion, standard);
        WriteJson(target, export);
    }

    private HostBenchmarkHistory ReadHistory()
    {
        if (!File.Exists(historyPath)) return new HostBenchmarkHistory(null, []);

        using var stream = File.OpenRead(historyPath);
        HostBenchmarkHistory? history = JsonSerializer.Deserialize<HostBenchmarkHistory>(stream, JsonOptions);
        if (history is null || history.SchemaVersion != 1 || history.Runs is null
            || history.Runs.Any(run => run is null || run.Metrics is null || run.Metrics.Any(metric => metric is null)))
            throw new InvalidDataException("基准测试历史的数据格式无效或版本不兼容。");

        return history;
    }

    private static HostBenchmarkRun FindRun(HostBenchmarkHistory history, Guid id)
        => history.Runs.FirstOrDefault(item => item.Id == id) ?? throw new ArgumentException("找不到所选的基准测试记录。", nameof(id));

    private static void WriteJson<T>(string target, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, data, JsonOptions);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private T WithLock<T>(Func<T> operation)
    {
        using var mutex = new Mutex(false, lockName);
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
                throw new IOException("读取基准测试历史超时。");
        }
        catch (AbandonedMutexException)
        {
            // The prior instance exited. The history is replaced atomically, so it can be reopened.
        }

        try { return operation(); }
        finally { mutex.ReleaseMutex(); }
    }

    private sealed record HostBenchmarkExport(int SchemaVersion, HostBenchmarkRun Run, HostBenchmarkRun? Reference,
        HostBenchmarkComparison? Comparison, string? ScoringVersion, HostBenchmarkComparison? StandardComparison);
}
