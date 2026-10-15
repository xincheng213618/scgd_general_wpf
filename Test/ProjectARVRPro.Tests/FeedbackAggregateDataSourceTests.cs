using Microsoft.Data.Sqlite;
using ProjectARVRPro.Offline;
using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.Tests;

public sealed class FeedbackAggregateDataSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVision.AggregateTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void OpensPublishedGenerationAndExistingReaderKeepsItsOwnSnapshotAfterUpdate()
    {
        CreateGeneration("first", "2026-09-12", 1);
        Publish("first");
        ArvrOfflineDataSource first = ArvrOfflineDataSource.Open(_root, Path.Combine(_root, "cache"));
        Assert.Contains("MACHINE", first.Label);
        Assert.Equal(new DateTime(2026, 9, 12), first.LatestDate);
        CreateGeneration("second", "2026-09-30", 2);
        Publish("second");
        ArvrOfflineDataSource second = ArvrOfflineDataSource.Open(_root, Path.Combine(_root, "cache"));
        Assert.Equal(new DateTime(2026, 9, 30), second.LatestDate);
        Assert.Equal("SN1", first.Statistics.GetRecord(1)!.SN);
        Assert.Null(first.Statistics.GetRecord(2));
        Assert.Equal("SN2", second.Statistics.GetRecord(2)!.SN);
        Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
    }

    [Theory]
    [InlineData("../outside/Database")]
    [InlineData("versions/missing/Database")]
    [InlineData("versions/first")]
    public void RejectsInvalidGenerationWithoutOpeningAnotherDatabase(string directory)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "aggregate.json"), JsonSerializer.Serialize(new
        {
            kind = "ColorVisionFeedbackAggregate", formatVersion = 1, machine = "MACHINE",
            databaseDirectory = directory, sources = new[] { new { feedbackId = "first" } },
        }));
        Assert.Throws<InvalidDataException>(() => ArvrOfflineDataSource.Open(_root, Path.Combine(_root, "cache")));
    }

    private void Publish(string name)
    {
        File.WriteAllText(Path.Combine(_root, "aggregate.json"), JsonSerializer.Serialize(new
        {
            kind = "ColorVisionFeedbackAggregate", formatVersion = 1, machine = "MACHINE",
            databaseDirectory = $"versions/{name}/Database", sources = new[] { new { feedbackId = name } },
        }));
    }

    private void CreateGeneration(string name, string date, int id)
    {
        string directory = Path.Combine(_root, "versions", name, "Database");
        Directory.CreateDirectory(directory);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(directory, "ProjectARVRPro.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE ObjectiveTestResultRecord(Id INTEGER PRIMARY KEY,SN TEXT,ResultId INTEGER,BatchId INTEGER,CreateTime TEXT,UpdateTime TEXT,TotalResult INTEGER);
            CREATE TABLE ARVRReuslt(Id INTEGER PRIMARY KEY,SN TEXT,BatchId INTEGER,Model TEXT,CreateTime TEXT,RunTime INTEGER);
            INSERT INTO ObjectiveTestResultRecord VALUES(@id,@sn,@id,@id,@date,@date,1);
            INSERT INTO ARVRReuslt VALUES(@id,@sn,@id,'PG',@date,1000);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@sn", $"SN{id}");
        command.Parameters.AddWithValue("@date", date + " 12:00:00");
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.AggregateTests")) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(_root);
        if (resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
