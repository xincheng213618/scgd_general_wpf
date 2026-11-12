using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateUsageReaderTests
{
    [Fact]
    public void CountsAllRetainedRunsButEstimatesOnlyRecentCompletedRunsOfTheSavedVersion()
    {
        string path = NewPath();
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE FlowRunRecord(template_id INTEGER,flow_key TEXT,flow_name TEXT,status INTEGER,elapsed_ms INTEGER,
              completed_time TEXT,content_hash TEXT,final_outcome INTEGER);
            """;
        command.ExecuteNonQuery();
        for (int index = 1; index <= 25; index++)
            Insert(connection, 10, "stable", FlowStatus.Completed, index * 100, "current", $"2026-01-{index:D2}");
        Insert(connection, 10, "stable", FlowStatus.Completed, 999999, "old");
        Insert(connection, 10, "stable", FlowStatus.Failed, 999999, "current");
        Insert(connection, 10, "stable", FlowStatus.Runing, 999999, "current");
        Insert(connection, 10, "stable", FlowStatus.Completed, 999999, "current", finalOutcome: 2);
        Insert(connection, 10, "different-key", FlowStatus.Completed, 999999, "current");
        var usage = FlowTemplateUsageReader.Read(path, new FlowIdentity(10, "stable", "renamed"), "current");
        Assert.NotNull(usage);
        Assert.Equal(29, usage.ExecutionCount);
        Assert.Equal(20, usage.SampleCount);
        Assert.Equal(1550, usage.ExpectedMs); // The latest 20 samples are 600 through 2500 ms.
        var changed = FlowTemplateUsageReader.Read(path, new FlowIdentity(10, "stable", "renamed"), "new-version")!;
        Assert.Equal(29, changed.ExecutionCount);
        Assert.Null(changed.ExpectedMs);
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public void LocalIdentityNeverMixesSameNameOrReusedIdsAndMissingHistoryDoesNotCreateADatabase()
    {
        string path = NewPath();
        Assert.Null(FlowTemplateUsageReader.Read(path, new FlowIdentity(-2, "local-flow:one", "Same name"), null));
        Assert.False(File.Exists(path));
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE FlowRunRecord(template_id INTEGER,flow_key TEXT,flow_name TEXT,status INTEGER,elapsed_ms INTEGER,completed_time TEXT)";
        command.ExecuteNonQuery();
        command.CommandText = """
            INSERT INTO FlowRunRecord VALUES(-2,'local-flow:one','Same name',6,100,'2026-01-01'),
              (-2,'local-flow:two','Same name',6,900,'2026-01-01'),
              (-2,NULL,'Same name',6,900,'2026-01-01'),(10,NULL,'Legacy',6,200,'2026-01-01');
            """;
        command.ExecuteNonQuery();
        var local = FlowTemplateUsageReader.Read(path, new FlowIdentity(-2, "local-flow:one", "Renamed"), null)!;
        Assert.Equal(1, local.ExecutionCount);
        Assert.Equal(100, local.ExpectedMs);
        var legacy = FlowTemplateUsageReader.Read(path, new FlowIdentity(10, "flow:ten", "Renamed"), null)!;
        Assert.Equal(1, legacy.ExecutionCount);
        Assert.Equal(200, legacy.ExpectedMs);
        Assert.Null(FlowTemplateUsageReader.Read(path, new FlowIdentity(10, "flow:ten", "Renamed"), "current")!.ExpectedMs);
    }

    private static string NewPath() => Path.Combine(Directory.CreateTempSubdirectory("ColorVision-flow-usage-").FullName, "runs.db");

    private static void Insert(SqliteConnection connection, int templateId, string key, FlowStatus status, long duration, string hash, string date = "2026-02-01", int finalOutcome = 0)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO FlowRunRecord VALUES($id,$key,'Original name',$status,$duration,$date,$hash,$outcome)";
        command.Parameters.AddWithValue("$id", templateId);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$duration", duration);
        command.Parameters.AddWithValue("$date", date);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$outcome", finalOutcome);
        command.ExecuteNonQuery();
    }
}
