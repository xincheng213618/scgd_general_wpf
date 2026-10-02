using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.Offline;

/// <summary>Resolve one immutable generation before copying any database; never runs the merge script.</summary>
internal static class FeedbackAggregateDirectory
{
    internal static string Resolve(string directory, out string? label)
    {
        label = null;
        string manifestPath = Path.Combine(directory, "aggregate.json");
        if (!File.Exists(manifestPath)) return directory;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            if (root.GetProperty("kind").GetString() != "ColorVisionFeedbackAggregate" || root.GetProperty("formatVersion").GetInt32() != 1)
                throw new InvalidDataException("聚合资料格式不受支持。");
            string relative = root.GetProperty("databaseDirectory").GetString() ?? string.Empty;
            string versionsRoot = Path.GetFullPath(Path.Combine(directory, "versions")) + Path.DirectorySeparatorChar;
            string databaseDirectory = Path.GetFullPath(Path.Combine(directory, relative));
            if (Path.IsPathRooted(relative) || !databaseDirectory.StartsWith(versionsRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(databaseDirectory).Equals("Database", StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(databaseDirectory))
                throw new InvalidDataException("聚合资料的数据库版本不存在或路径越界。");
            // Generation directories are immutable. A concurrent manual update only replaces the manifest pointer.
            label = $"{root.GetProperty("machine").GetString()} · 聚合 {root.GetProperty("sources").GetArrayLength()} 份反馈";
            return databaseDirectory;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("聚合资料清单无效。请检查 aggregate.json。", ex);
        }
    }
}
