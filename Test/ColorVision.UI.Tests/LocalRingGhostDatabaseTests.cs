using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.RingGhost;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class LocalRingGhostDatabaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundFlowKeepsImageResultAndSideDataOnItsDatabaseAfterReconnect(bool rejectDetail)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-ring-ghost-db-");
        string resultDirectory = Path.Combine(directory.FullName, "results");
        try
        {
            StaTest.Run(() =>
            {
                MySqlConnectedEndpoint? current = null;
                MySqlConnectedEndpoint MakeEndpoint(string name) => new(candidate => ReferenceEquals(candidate, current),
                    () => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={Path.Combine(directory.FullName, name)};Pooling=False",
                        DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
                    }));
                var original = MakeEndpoint("original.db");
                var replacement = MakeEndpoint("replacement.db");
                using var originalDb = original.CreateDbClient();
                using var replacementDb = replacement.CreateDbClient();
                current = original;
                var action = new CVStartCFC(directory.Name);
                using var resources = action.RuntimeResources;
                var batch = new MeasureBatchModel { Name = "original", Code = action.SerialNumber };
                var otherBatch = new MeasureBatchModel { Name = "replacement", Code = action.SerialNumber };
                string originalPath = Path.Combine(directory.FullName, "original.cvraw");
                string replacementPath = Path.Combine(directory.FullName, "replacement.cvraw");
                int imageId = Seed(originalDb, originalPath);
                Assert.Equal(imageId, Seed(replacementDb, replacementPath));
                FlowMeasurementBatch.Insert(batch, original).Bind(resources);
                _ = FlowMeasurementBatch.Insert(otherBatch, replacement);
                Assert.Equal(batch.Id, otherBatch.Id);
                int unrelatedMasterId = replacementDb.Insertable(new AlgResultMasterModel
                {
                    BatchId = otherBatch.Id, ImgFileType = ViewResultAlgType.Ghost, Result = "untouched B", CreateDate = DateTime.Now,
                }).ExecuteReturnIdentity();
                if (rejectDetail)
                    originalDb.Ado.ExecuteCommand("CREATE TRIGGER reject_detail BEFORE INSERT ON t_scgd_algorithm_result_detail_common BEGIN SELECT RAISE(ABORT, 'synthetic ghost detail failure'); END");

                var services = new SyntheticServices();
                var node = new LocalRingGhostNode(services) { ResultDirectory = resultDirectory };
                node.Create();
                action.MasterValue(null!, imageId, (int)CVCommCore.CVResultType.Camera_Img);
                List<ResultMessage> messages = [];
                using var subscription = ResultMessageBus.Default.Subscribe(message =>
                {
                    if (message.SerialNumber == action.SerialNumber && message.EventName == "Ghost") messages.Add(message);
                });

                current = replacement;
                Assert.False(original.IsCurrent);
                if (rejectDetail)
                {
                    Exception error = Assert.ThrowsAny<Exception>(() => node.ExecuteSynchronously(action));
                    Assert.Contains("synthetic ghost detail failure", error.ToString());
                    Assert.Empty(originalDb.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Empty(originalDb.Queryable<DetailCommonModel>().ToList());
                    Assert.Empty(Directory.EnumerateFiles(resultDirectory));
                    Assert.Empty(messages);
                    Assert.False(action.TryGetCurrentFrame(out _));
                    Assert.False(action.Data.ContainsKey("GMR1"));
                }
                else
                {
                    var (saved, measurement) = node.ExecuteSynchronously(action);
                    AlgResultMasterModel master = Assert.Single(originalDb.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Equal(saved.MasterId, master.Id);
                    Assert.Equal(unrelatedMasterId, master.Id);
                    Assert.Equal(batch.Id, master.BatchId);
                    Assert.Equal(originalPath, master.ImgFile);
                    Assert.Equal(LocalRingGhostResult.ResultVersion, master.version);
                    DetailCommonModel detail = Assert.Single(originalDb.Queryable<DetailCommonModel>().ToList());
                    Assert.Equal(master.Id, detail.PId);
                    Assert.Equal(saved.ResultFilePath, (string?)JObject.Parse(detail.ResultJson)["ResultFileName"]);
                    Assert.Equal(saved.ResultFilePath, Assert.Single(Directory.EnumerateFiles(resultDirectory)));
                    Assert.Equal((.75, 0d), (measurement.GMR1, measurement.GMR2));
                    Assert.Equal(.75, LocalRingGhostResult.Read(saved.ResultFilePath).GMR1);
                    ResultMessage notice = Assert.Single(messages);
                    Assert.Equal((ResultRoutes.LocalFlow, ResultKinds.Algorithm, master.Id, (int)ViewResultAlgType.Ghost),
                        (notice.Route, notice.ResultKind, notice.Data.MasterId, notice.Data.MasterResultType));
                    Assert.Same(original, notice.DatabaseEndpoint);
                }
                Assert.Equal(originalPath, services.LoadedPath);
                Assert.Equal("untouched B", Assert.Single(replacementDb.Queryable<AlgResultMasterModel>().ToList()).Result);
                Assert.Empty(replacementDb.Queryable<DetailCommonModel>().ToList());
            });
        }
        finally
        {
            if (Directory.Exists(resultDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(resultDirectory)) File.Delete(file);
                Directory.Delete(resultDirectory);
            }
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static int Seed(SqlSugarClient db, string imagePath)
    {
        db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(AlgResultMasterModel), typeof(DetailCommonModel));
        File.WriteAllText(imagePath, "synthetic frame source");
        return db.Insertable(new MeasureResultImgModel { FileUrl = imagePath }).ExecuteReturnIdentity();
    }

    private sealed class SyntheticServices : ILocalRingGhostNodeServices
    {
        private readonly LocalRingGhostNodeServices production = new();
        public string? LoadedPath { get; private set; }

        public LocalFlowFrame LoadFrame(string path)
        {
            LoadedPath = path;
            var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 16, Height = 16, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            }, 256, 0);
            frame.CvRawFilePath = path;
            return frame;
        }

        public MeasureResultImgModel? GetImageResult(int id, MySqlConnectedEndpoint? endpoint) =>
            production.GetImageResult(id, endpoint);

        public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation) => new()
        {
            AlgorithmId = DisplayMetrologyIds.RingGhost,
            AlgorithmVersion = new(1, 0, 0),
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmTableArtifact("ring-ghost-candidates", [],
                    [new Dictionary<string, JsonElement> { ["平均信号比_%"] = JsonSerializer.SerializeToElement(.75) }]),
                new AlgorithmGeometryArtifact("display-regions", AlgorithmCoordinateSpace.Pixel,
                    new[] { "primary-inner", "primary-outer", "ghost-inner", "ghost-outer" }.Select(Region).ToArray()),
            ],
        };

        private static AlgorithmGeometry Region(string id) =>
            new(id, AlgorithmGeometryKind.Polyline, [new(1, 1), new(4, 1), new(1, 4)]);

        public LocalRingGhostSavedResult Save(LocalRingGhostSaveRequest request) => production.Save(request);

        public void Publish(string serialNumber, string nodeId, int zIndex, int masterId, MySqlConnectedEndpoint? endpoint) =>
            production.Publish(serialNumber, nodeId, zIndex, masterId, endpoint);
    }
}
