using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LocalPoiPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundPoiUsesItsDatabaseAfterReconnectAndRollsBackOnDetailFailure(bool failDetails)
    {
        string folder = Directory.CreateTempSubdirectory("cv-poi-database-").FullName;
        string pathA = Path.Combine(folder, "a.db"), pathB = Path.Combine(folder, "b.db");
        MySqlConnectedEndpoint? active = null;
        MySqlConnectedEndpoint a = new(endpoint => ReferenceEquals(active, endpoint), () => Open(pathA));
        MySqlConnectedEndpoint b = new(endpoint => ReferenceEquals(active, endpoint), () => Open(pathB));
        var action = new CVStartCFC("shared-batch-number");
        using var resources = action.RuntimeResources;
        try
        {
            MakeSchema(a);
            MakeSchema(b);
            active = a;
            var batchA = new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber };
            FlowMeasurementBatch.Insert(batchA, a).Bind(resources);
            var batchB = new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber };
            _ = FlowMeasurementBatch.Insert(batchB, b);
            Assert.Equal(batchA.Id, batchB.Id);
            Assert.Equal(Seed(a, batchA.Id, "original A"), Seed(b, batchB.Id, "original B"));

            active = b;
            Assert.False(a.IsCurrent);
            Assert.Same(a, LocalFlowResultPersistence.ResolveEndpoint(action));
            var result = new LocalPoiResultSet
            {
                Points =
                [
                    new() { PoiId = 4, Name = "first", PointType = POIPointTypes.SolidPoint, X = 1, Y = 2, Width = 1, Height = 1, Value = new { Y = 11.5 } },
                    new() { PoiId = failDetails ? 4 : 9, Name = "second", PointType = POIPointTypes.SolidPoint, X = 3, Y = 4, Width = 1, Height = 1, Value = new { Y = 14.75 } }
                ]
            };
            int Save(MySqlConnectedEndpoint endpoint) => LocalFlowResultPersistence.SaveAlgorithmResultWithDetails(action,
                ViewResultAlgType.POI_Y, 62, "transaction", null, null, 2, 8, new { CieMasterId = 17 },
                id => LocalPoiCalculator.CreateDetails(id, result), endpoint: endpoint);

            Assert.Throws<InvalidOperationException>(() => Save(b));
            if (failDetails)
            {
                Exception error = Assert.ThrowsAny<Exception>(() => Save(a));
                Assert.Contains("UNIQUE constraint failed", error.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            else Assert.Equal(2, Save(a));

            using var dbA = a.CreateDbClient();
            using var dbB = b.CreateDbClient();
            Assert.Equal("original B", Assert.Single(dbB.Queryable<AlgResultMasterModel>().ToList()).TName);
            Assert.Equal("original B", Assert.Single(dbB.Queryable<PoiPointResultModel>().ToList()).PoiName);
            var rows = dbA.Queryable<AlgResultMasterModel>().OrderBy(row => row.Id).ToList();
            var details = dbA.Queryable<PoiPointResultModel>().OrderBy(row => row.Id).ToList();
            Assert.Equal("original A", rows[0].TName);
            Assert.Equal("original A", details[0].PoiName);
            if (failDetails)
            {
                Assert.Single(rows);
                Assert.Single(details);
            }
            else
            {
                Assert.Equal(2, rows.Count);
                Assert.Equal(batchA.Id, rows[1].BatchId);
                Assert.Equal("transaction", rows[1].TName);
                Assert.Equal(17, (int)JObject.Parse(rows[1].Params!)["CieMasterId"]!);
                Assert.Equal(3, details.Count);
                Assert.All(details.Skip(1), row => Assert.Equal(rows[1].Id, row.Pid));
                Assert.Equal(new int?[] { 4, 9 }, details.Skip(1).Select(row => row.PoiId));
            }
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
            Directory.Delete(folder);
        }

        static SqlSugarClient Open(string path) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite,
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        static void MakeSchema(MySqlConnectedEndpoint endpoint)
        {
            using var db = endpoint.CreateDbClient();
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(AlgResultMasterModel), typeof(PoiPointResultModel));
            db.Ado.ExecuteCommand("CREATE UNIQUE INDEX unique_poi ON t_scgd_algorithm_result_detail_poi_mtf (pid, poi_id)");
        }

        static int Seed(MySqlConnectedEndpoint endpoint, int batchId, string label)
        {
            using var db = endpoint.CreateDbClient();
            int id = db.Insertable(new AlgResultMasterModel { BatchId = batchId, TName = label, CreateDate = DateTime.Now }).ExecuteReturnIdentity();
            db.Insertable(new PoiPointResultModel { Pid = id, PoiId = 4, PoiName = label, PoiType = POIPointTypes.SolidPoint,
                PoiX = 1, PoiY = 2, PoiWidth = 1, PoiHeight = 1, Value = "{}" }).ExecuteCommand();
            return id;
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CalibrationPoiReusesTheCaptureRowOnlyForTheSameRawFileAndBatch(bool sameFile, bool sameBatch) => StaTest.Run(() =>
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("cv-calibration-poi-history-");
        try
        {
            string databasePath = Path.Combine(folder.FullName, "results.db");
            var endpoint = new MySqlConnectedEndpoint(_ => true, () => new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath};Pooling=False", DbType = DbType.Sqlite,
                IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
            }));
            using var db = endpoint.CreateDbClient();
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel),
                typeof(AlgResultMasterModel), typeof(PoiPointResultModel));

            var action = new CVStartCFC("calibration-poi-history") { PersistResults = true };
            using var resources = action.RuntimeResources;
            MeasureBatchModel batch = new() { Name = action.SerialNumber, Code = action.SerialNumber };
            FlowMeasurementBatch.Insert(batch, endpoint).Bind(resources);
            int captureBatchId = batch.Id;
            if (!sameBatch)
            {
                MeasureBatchModel otherBatch = new() { Name = "earlier-capture", Code = "earlier-capture" };
                _ = FlowMeasurementBatch.Insert(otherBatch, endpoint);
                captureBatchId = otherBatch.Id;
            }
            const string template = "test-color";
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 1, Height = 1, SourceBpp = 16, Channels = 1, Exposure = [1],
                CalibrationTemplate = template, IsMirrorReady = true,
            }, sizeof(ushort), 0);
            using (LocalFlowFrameLease lease = frame.Acquire()) Marshal.WriteInt16(lease.RawPointer, 100);
            frame.ColorCalibration = new ColorCalibrationSnapshot
            {
                Width = 1, Height = 1, RawBpp = 16, Channels = 1, Template = template, TransformKind = 2,
                Coefficients = [1, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1]
            };
            string rawPath = Path.Combine(folder.FullName, "capture.cvraw");
            LocalFrameFileService.SaveCapture(frame, rawPath);
            action.SetCurrentFrame(frame);

            int captureId = db.Insertable(new MeasureResultImgModel
            {
                BatchId = captureBatchId, FileUrl = sameFile ? rawPath : Path.Combine(folder.FullName, "other.cvraw"),
                Result = "capture",
            }).ExecuteReturnIdentity();
            frame.MasterId = captureId;
            action.MasterValue(null, captureId, (int)CVCommCore.CVResultType.Camera_Img);
            int layoutId = db.Insertable(new AlgResultMasterModel { BatchId = batch.Id, TName = "layout", CreateDate = DateTime.Now })
                .ExecuteReturnIdentity();
            db.Insertable(new PoiPointResultModel
            {
                Pid = layoutId, PoiId = 7, PoiName = "center", PoiType = POIPointTypes.SolidPoint,
                PoiX = 0, PoiY = 0, PoiWidth = 1, PoiHeight = 1, Value = "{}",
            }).ExecuteCommand();
            CVStartCFC layout = new(action);
            layout.MasterValue(null, layoutId, (int)ViewResultAlgType.BuildPOI);

            var node = new LocalCalibrationRealPoiNode { CalibTempName = template };
            using var execution = node.CreateCompatibilityExecution(new CVMQTTRequest { SerialNumber = action.SerialNumber }, null, action, layout);
            execution.Bind(action);
            execution.Execute();
            var result = Assert.IsType<LocalCalibrationRealPoiNodeResultData>(execution.Complete(action));

            bool reuse = sameFile && sameBatch;
            var imageRows = db.Queryable<MeasureResultImgModel>().OrderBy(row => row.Id).ToList();
            Assert.Equal(reuse ? 1 : 2, imageRows.Count);
            Assert.Equal("capture", imageRows[0].Result);
            Assert.Equal(reuse ? -1 : imageRows[1].Id, result.CalibrationMasterId);
            Assert.Equal(reuse ? captureId : imageRows[1].Id, frame.MasterId);
            AlgResultMasterModel poi = Assert.Single(db.Queryable<AlgResultMasterModel>().Where(row => row.Id != layoutId).ToList());
            Assert.Equal(result.PoiMasterId, poi.Id);
            Assert.Equal(batch.Id, poi.BatchId);
            Assert.Equal(rawPath, poi.ImgFile);
            Assert.Equal(frame.MasterId, JObject.Parse(poi.Params!).Value<int>("CieMasterId"));
            Assert.Equal(7, Assert.Single(db.Queryable<PoiPointResultModel>().Where(row => row.Pid == poi.Id).ToList()).PoiId);
        }
        finally
        {
            foreach (FileInfo file in folder.EnumerateFiles()) file.Delete();
            folder.Delete();
        }
    });

    [Theory]
    [InlineData("commit")]
    [InlineData("short")]
    [InlineData("cancel")]
    public void PoiMasterAndOrderedDetailsCommitOrRollBackTogether(string outcome)
    {
        using CancellationTokenSource stop = new();
        var transaction = new RecordingTransaction(outcome, stop);
        var result = new LocalPoiResultSet
        {
            Points =
            [
                new() { PoiId = 42, Name = "first", PointType = POIPointTypes.Circle, X = 6, Y = 5, Width = 3, Height = 2, Value = new { Y = 7.25 } },
                new() { PoiId = 7, Name = "second", PointType = POIPointTypes.SolidPoint, X = 1, Y = 2, Width = 1, Height = 1, Value = new { Y = 12.5 } }
            ]
        };
        int Save() => LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(new AlgResultMasterModel { BatchId = 9 },
            ViewResultAlgType.POI_Y, id => LocalPoiCalculator.CreateDetails(id, result), () => transaction, stop.Token);

        if (outcome == "commit") Assert.Equal(123, Save());
        else if (outcome == "cancel") Assert.ThrowsAny<OperationCanceledException>(() => Save());
        else Assert.Contains("实际写入 1 条", Assert.Throws<InvalidOperationException>(() => Save()).Message);

        Assert.Equal(new[] { "begin", "master", "details", outcome == "commit" ? "commit" : "rollback", "dispose" }, transaction.Calls);
        Assert.Collection(transaction.Details,
            first =>
            {
                Assert.Equal((123, 42, "first", POIPointTypes.Circle, 6, 5, 3, 2),
                    (first.Pid, first.PoiId, first.PoiName, first.PoiType, first.PoiX, first.PoiY, first.PoiWidth, first.PoiHeight));
                Assert.Equal(7.25, (double)JObject.Parse(first.Value!)["Y"]!);
            },
            second =>
            {
                Assert.Equal(123, second.Pid);
                Assert.Equal(7, second.PoiId);
                Assert.Equal(12.5, (double)JObject.Parse(second.Value!)["Y"]!);
            });
    }

    [Theory]
    [InlineData(typeof(LocalPoiNode), false)]
    [InlineData(typeof(LocalRealPoiNode), true)]
    [InlineData(typeof(LocalCalibrationRealPoiNode), true)]
    public void RuntimePointsAndLayoutsFlowWithoutDatabases(Type nodeType, bool useLayout) => StaTest.Run(() =>
    {
        var action = new CVStartCFC("offline-poi") { PersistResults = !useLayout };
        using var resources = action.RuntimeResources;
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 1, Height = 1, SourceBpp = 16, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvCie
        }, 0, sizeof(float));
        using (LocalFlowFrameLease lease = frame.Acquire()) Marshal.Copy(new[] { 7.25f }, 0, lease.CiePointer, 1);
        action.SetCurrentFrame(frame);
        if (useLayout)
        {
            string layout = LocalPoiRemappingCalculator.StoreRuntimeLayout(action, "layout",
                [new() { PoiId = 7, Name = "center", PointType = POIPointTypes.SolidPoint, Width = 1, Height = 1 }]);
            action.MasterValue(layout, 0, (int)ViewResultAlgType.BuildPOI);
        }
        else
        {
            var points = new LocalFlowPoiPointSet([new LightBeadCenter(0, 0)], frame.Metadata, PoiShape.Point, 1, 1);
            points.Publish(action);
        }

        object node = Activator.CreateInstance(nodeType)!;
        object execution = nodeType.GetMethod("ExecuteLocal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(node, [action])!;
        JObject output = JObject.FromObject(execution.GetType().GetProperty("Data")!.GetValue(execution)!);
        Assert.False((bool)output["DatabasePersisted"]!);
        Assert.True((bool)output["MemoryOnly"]!);
        Assert.Equal(0, (int)output["MasterId"]!);
        Assert.Equal((int)ViewResultAlgType.POI_Y, (int)output["MasterResultType"]!);
        Assert.Equal(1, (int)output["PointCount"]!);
        Assert.True(action.RuntimeResources.TryGet((string)output["ResultHandle"]!, out LocalPoiResultSet? result));
        Assert.Equal(7.25, (double)JObject.FromObject(Assert.Single(result!.Points).Value)["Y"]!);
    });

    private sealed class RecordingTransaction(string outcome, CancellationTokenSource stop) : ILocalFlowResultTransaction<PoiPointResultModel>
    {
        internal List<string> Calls { get; } = [];
        internal IReadOnlyCollection<PoiPointResultModel> Details { get; private set; } = [];

        public void Begin() => Calls.Add("begin");
        public int InsertMaster(AlgResultMasterModel model)
        {
            Calls.Add("master");
            Assert.Equal(9, model.BatchId);
            return 123;
        }

        public int InsertDetails(IReadOnlyCollection<PoiPointResultModel> details)
        {
            Calls.Add("details");
            Details = details;
            if (outcome == "cancel") stop.Cancel();
            return outcome == "short" ? 1 : details.Count;
        }

        public void Commit() => Calls.Add("commit");
        public void Rollback() => Calls.Add("rollback");
        public void Dispose() => Calls.Add("dispose");
    }
}
