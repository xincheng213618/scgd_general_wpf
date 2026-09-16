using ColorVision.UI.Desktop.Feedback;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;

namespace ProjectARVRPro.Tests;

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class ProjectARVRProFeedbackCollectorTests
{
    [Fact]
    public void ConfigurationSnapshotUsesProjectDirectoryAndIncludesOldCurrentConfigOnly()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-ARVRConfigFeedback-").FullName;
        string originalDirectory = ViewResultManager.DirectoryPath;
        var files = new List<(string EntryPath, string FilePath)>();
        try
        {
            ViewResultManager.DirectoryPath = directory;
            string configPath = Path.Combine(directory, "ProjectARVRProProcessGroups.json");
            const string json = """
                {"ActiveGroupIndex":0,"Groups":[{"PictureSwitchConfig":{"SuccessDelayMs":500},"ConfigJson":"{\"ExposureTimeMs\":50,\"ApiKey\":\"project-secret\"}"}],"RecipeConfig":{"Lv":123.4}}
                """;
            File.WriteAllText(configPath, json);
            File.SetLastWriteTimeUtc(configPath, DateTime.UtcNow.AddYears(-1));
            File.WriteAllText(Path.Combine(directory, "ProcessGroups.json"), "legacy file must not be collected");
            var collector = new ProjectARVRProConfigurationFeedbackCollector();
            Assert.True(new CollectorItem(collector).IsChecked);
            Assert.False((ColorVision.UI.IFeedbackLogCollector)collector is ColorVision.UI.IFeedbackLogTimeRangeCollector);
            files = collector.CollectFiles().ToList();
            var file = Assert.Single(files);
            Assert.Equal("Config/ProjectARVRProProcessGroups.json", file.EntryPath);
            JObject snapshot = JObject.Parse(File.ReadAllText(file.FilePath));
            Assert.Equal(500, snapshot["Groups"]![0]!["PictureSwitchConfig"]!.Value<int>("SuccessDelayMs"));
            JObject nested = JObject.Parse(snapshot["Groups"]![0]!.Value<string>("ConfigJson")!);
            Assert.Equal(50, nested.Value<int>("ExposureTimeMs"));
            Assert.Equal("[REDACTED]", nested.Value<string>("ApiKey"));
            Assert.Equal(123.4, snapshot["RecipeConfig"]!.Value<double>("Lv"));
            Assert.Equal(json, File.ReadAllText(configPath));
        }
        finally
        {
            ViewResultManager.DirectoryPath = originalDirectory;
            foreach (var file in files) File.Delete(file.FilePath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FeedbackExportsRecentPhasesLinkedGroupAndFullPayloadsFromActualSchema()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-ARVRFeedback-").FullName;
        string sourcePath = Path.Combine(directory, "ProjectARVRPro.db");
        string originalPath = ViewResultManager.SqliteDbPath;
        var files = new List<(string EntryPath, string FilePath)>();
        try
        {
            DateTime old = DateTime.Now.AddDays(-10);
            DateTime recent = DateTime.Now.AddHours(-1);
            int recentId;
            int linkedId;
            const string resultJson = "{\"Lv\":123.4,\"Bv\":12.3,\"note\":\"取图耗时分析\"}";
            const string groupJson = "{\"TotalResult\":true}";
            using (var db = CreateDb(sourcePath))
            {
                db.CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>();
                ResultJsonPayloadStorage.EnsureSchema(db);
                var result = new ProjectARVRReuslt
                {
                    BatchId = 81, SN = "feedback", CreateTime = old, FlowStartedAt = recent,
                    FlowCompletedAt = recent.AddSeconds(18), RunTime = 18000,
                    PictureSwitchStartedAt = recent.AddSeconds(-2), PictureSwitchCompletedAt = recent.AddSeconds(-1),
                };
                recentId = db.Insertable(result).ExecuteReturnIdentity();
                ResultJsonPayloadStorage.SaveViewResultJson(db, recentId, resultJson);
                linkedId = db.Insertable(new ProjectARVRReuslt { SN = "linked", CreateTime = old }).ExecuteReturnIdentity();
                db.Insertable(new ProjectARVRReuslt { SN = "unrelated old", CreateTime = old }).ExecuteCommand();
                int groupId = db.Insertable(new ObjectiveTestResultRecord
                {
                    ResultId = recentId, SN = "feedback", CreateTime = old, UpdateTime = old
                }).ExecuteReturnIdentity();
                ResultJsonPayloadStorage.SaveObjectiveTestResultJson(db, groupId, groupJson);
                db.Insertable(new ObjectiveTestResultRecord { ResultId = linkedId, CreateTime = old, UpdateTime = recent }).ExecuteCommand();
                db.Insertable(new ObjectiveTestResultRecord { CreateTime = old, UpdateTime = old }).ExecuteCommand();
            }

            ViewResultManager.SqliteDbPath = sourcePath;
            var collector = new ProjectARVRProFeedbackCollector();
            var item = new CollectorItem(collector);
            Assert.True(item.IsChecked);
            Assert.Equal(7, item.SelectedDays);
            Assert.Equal(directory, collector.LogDirectory);
            files = collector.CollectFiles().ToList();
            string target = Assert.Single(files, file => file.EntryPath == "Database/ProjectARVRPro.db").FilePath;
            using (var result = CreateDb(target))
            {
                Assert.Equal(new[] { recentId, linkedId }, result.Queryable<ProjectARVRReuslt>().OrderBy(row => row.Id).Select(row => row.Id).ToArray());
                Assert.Equal(2, result.Queryable<ObjectiveTestResultRecord>().Count());
                ProjectARVRReuslt row = result.Queryable<ProjectARVRReuslt>().InSingle(recentId);
                Assert.Equal(18000, row.RunTime);
                Assert.Equal(18, (row.FlowCompletedAt!.Value - row.FlowStartedAt!.Value).TotalSeconds);
                Assert.Equal(resultJson, ResultJsonPayloadStorage.LoadViewResultJson(result, recentId));
                Assert.Equal(groupJson, ResultJsonPayloadStorage.LoadObjectiveTestResultJson(result, 1));
            }
            JObject status = JObject.Parse(File.ReadAllText(files.Single(file => file.EntryPath.EndsWith(".json")).FilePath));
            Assert.Equal("ok", status.Value<string>("Status"));
            using var source = CreateDb(sourcePath);
            Assert.Equal(3, source.Queryable<ProjectARVRReuslt>().Count());
            Assert.Equal(3, source.Queryable<ObjectiveTestResultRecord>().Count());
        }
        finally
        {
            ViewResultManager.SqliteDbPath = originalPath;
            SqliteConnection.ClearAllPools();
            foreach (var file in files) File.Delete(file.FilePath);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SqlSugarClient CreateDb(string path) => new(new ConnectionConfig
    {
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString(),
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
    });
}
