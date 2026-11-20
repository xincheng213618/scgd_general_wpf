using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalFindCrossDatabaseTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ImagesTemplatesResultsAndNotificationsStayWithTheBoundBatchAfterReconnect(bool detectionFailed, bool detailWriteFailed)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-find-cross-db-");
        string resultsDirectory = Path.Combine(directory.FullName, "results");
        try
        {
            StaTest.Run(() =>
            {
                MySqlConnectedEndpoint? current = null;
                MySqlConnectedEndpoint Endpoint(string file) => new(candidate => ReferenceEquals(candidate, current),
                    () => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={Path.Combine(directory.FullName, file)};Pooling=False",
                        DbType = DbType.Sqlite,
                        InitKeyType = InitKeyType.Attribute,
                        IsAutoCloseConnection = true,
                    }));
                var first = Endpoint("a.db");
                var second = Endpoint("b.db");
                current = first;
                using var a = first.CreateDbClient();
                using var b = second.CreateDbClient();
                var action = new CVStartCFC($"find-cross-db-{Guid.NewGuid():N}") { PersistResults = true };
                using var resources = action.RuntimeResources;
                string pathA = Path.Combine(directory.FullName, "camera-a.tif");
                string pathB = Path.Combine(directory.FullName, "camera-b.tif");
                File.WriteAllBytes(pathA, [0]);
                File.WriteAllBytes(pathB, [0]);
                int imageId = Seed(a, pathA, 2);
                Assert.Equal(imageId, Seed(b, pathB, 4));
                var batchA = new MeasureBatchModel { Code = action.SerialNumber, Name = action.SerialNumber };
                FlowMeasurementBatch.Insert(batchA, first).Bind(resources);
                var batchB = new MeasureBatchModel { Code = action.SerialNumber, Name = action.SerialNumber, Result = "untouched B" };
                FlowMeasurementBatch.Insert(batchB, second);
                Assert.Equal(batchA.Id, batchB.Id);
                action.Data["MasterId"] = imageId;
                action.Data["MasterResultType"] = (int)CVCommCore.CVResultType.Camera_Img;
                if (detailWriteFailed)
                    a.Ado.ExecuteCommand("CREATE TRIGGER reject_detail BEFORE INSERT ON t_scgd_algorithm_result_detail_common BEGIN SELECT RAISE(ABORT, 'synthetic detail failure'); END");

                var services = new TestCrossServices(detectionFailed);
                var node = new LocalFindCrossNode(services) { SearchRegionPoiTemplate = "Search", ResultDirectory = resultsDirectory };
                List<ResultMessage> notifications = [];
                using var subscription = ResultMessageBus.Default.Subscribe(message =>
                {
                    if (message.EventName == "FindCross" && message.SerialNumber == action.SerialNumber)
                        notifications.Add(message);
                });

                current = second;
                if (detailWriteFailed)
                {
                    Exception error = Assert.ThrowsAny<Exception>(() => node.ExecuteSynchronously(action));
                    Assert.Contains("synthetic detail failure", error.ToString());
                    Assert.Empty(a.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Empty(a.Queryable<DetailCommonModel>().ToList());
                    Assert.Empty(Directory.EnumerateFiles(resultsDirectory));
                    Assert.Empty(notifications);
                }
                else
                {
                    LocalFindCrossNodeResultData? result = null;
                    if (detectionFailed)
                    {
                        var error = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
                        Assert.Contains("SyntheticNoIntersection", error.Message);
                    }
                    else
                        result = node.ExecuteSynchronously(action);

                    var master = Assert.Single(a.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Equal(batchA.Id, master.BatchId);
                    Assert.Equal(pathA, master.ImgFile);
                    Assert.Equal("1.0", master.version);
                    Assert.Equal((detectionFailed ? "LocalFindCross" : "optCenter", detectionFailed ? -1 : 0),
                        (master.TName, master.ResultCode.GetValueOrDefault()));
                    var notification = Assert.Single(notifications);
                    Assert.Equal(master.Id, notification.Data.MasterId);
                    Assert.Same(first, notification.DatabaseEndpoint);

                    if (detectionFailed)
                    {
                        Assert.Empty(a.Queryable<DetailCommonModel>().ToList());
                        Assert.False(Directory.Exists(resultsDirectory));
                        Assert.False(action.Data.ContainsKey("LocalFindCrossResultFile"));
                    }
                    else
                    {
                        Assert.NotNull(result);
                        Assert.Equal((master.Id, imageId, pathA), (result.MasterId, result.SourceMasterId, result.ImageFilePath));
                        var detail = Assert.Single(a.Queryable<DetailCommonModel>().ToList());
                        Assert.Equal(master.Id, detail.PId);
                        Assert.Equal(result.ResultFilePath, (string?)JObject.Parse(detail.ResultJson)["ResultFileName"]);
                        Assert.Equal("Cross", (string?)JObject.Parse(File.ReadAllText(result.ResultFilePath!))["result"]?[0]?["name"]);
                    }
                }

                Assert.False(first.IsCurrent);
                Assert.Equal(pathA, services.LoadedPath);
                Assert.Equal(new RoiRect(2, 1, 2, 3), services.SearchRegion);
                Assert.Empty(b.Queryable<AlgResultMasterModel>().ToList());
                Assert.Empty(b.Queryable<DetailCommonModel>().ToList());
                Assert.Equal("untouched B", b.Queryable<MeasureBatchModel>().Single().Result);
            });
        }
        finally
        {
            if (Directory.Exists(resultsDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(resultsDirectory)) File.Delete(file);
                Directory.Delete(resultsDirectory);
            }
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static int Seed(SqlSugarClient db, string sourceFile, int roiX)
    {
        db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(PoiMasterModel),
            typeof(PoiDetailModel), typeof(AlgResultMasterModel), typeof(DetailCommonModel));
        int templateId = db.Insertable(new PoiMasterModel
        {
            Name = "Search", Type = 0, Width = 8, Height = 8, CfgJson = "{}", Remark = string.Empty,
            LeftTopX = 0, LeftTopY = 0, RightTopX = 7, RightTopY = 0,
            RightBottomX = 7, RightBottomY = 7, LeftBottomX = 0, LeftBottomY = 7,
        }).ExecuteReturnIdentity();
        db.Insertable(new PoiDetailModel
        {
            Pid = templateId,
            Name = "search region",
            Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType,
            PixX = roiX,
            PixY = 1,
            PixWidth = 2,
            PixHeight = 3,
        }).ExecuteCommand();
        return db.Insertable(new MeasureResultImgModel { FileUrl = sourceFile }).ExecuteReturnIdentity();
    }

    private sealed class TestCrossServices(bool detectionFailed) : ILocalFindCrossNodeServices
    {
        public string? LoadedPath { get; private set; }
        public RoiRect SearchRegion { get; private set; }

        public Int32Rect LoadSearchRegionTemplate(string templateName, MySqlConnectedEndpoint? endpoint) =>
            LocalFindCrossNodeServices.Instance.LoadSearchRegionTemplate(templateName, endpoint);

        public MeasureResultImgModel? GetImageResult(int masterId, MySqlConnectedEndpoint? endpoint) =>
            LocalFindCrossNodeServices.Instance.GetImageResult(masterId, endpoint);

        public LocalFlowFrame LoadFrame(string filePath)
        {
            LoadedPath = filePath;
            var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 8,
                Height = 8,
                SourceBpp = 8,
                Channels = 1,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            }, 64, 0);
            frame.CvRawFilePath = filePath;
            return frame;
        }

        public LocalFindCrossDetection Detect(HImage image, RoiRect roi, string parameterJson)
        {
            SearchRegion = roi;
            return detectionFailed
                ? new() { FailureReason = "SyntheticNoIntersection" }
                : new() { Success = true, Items = [new() { Name = "Cross", Width = 2, Height = 2, CenterX = 3, CenterY = 3 }] };
        }

        public LocalFindCrossPersistenceResult Persist(LocalFindCrossPersistenceRequest request) =>
            LocalFindCrossNodeServices.Instance.Persist(request);

        public void Publish(LocalFindCrossPublishRequest request) => LocalFindCrossNodeServices.Instance.Publish(request);
    }
}
