using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Algorithms.Mtf;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Collections;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class LocalMtfDatabaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersistedInputsAndOutputStayWithTheRunDatabaseAfterReconnect(bool failDetailWrite)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-mtf-batch-db-");
        string results = Path.Combine(directory.FullName, "results");
        try
        {
            StaTest.Run(() =>
            {
                MySqlConnectedEndpoint? current = null;
                MySqlConnectedEndpoint Endpoint(string file) => new(candidate => ReferenceEquals(candidate, current),
                    () => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={Path.Combine(directory.FullName, file)};Pooling=False",
                        DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
                    }));
                var first = Endpoint("a.db");
                var second = Endpoint("b.db");
                current = first;
                using var a = first.CreateDbClient();
                using var b = second.CreateDbClient();
                var action = new CVStartCFC("mtf-flow") { PersistResults = true };
                using var resources = action.RuntimeResources;
                string pathA = Path.Combine(directory.FullName, "camera-a.cvraw");
                string pathB = Path.Combine(directory.FullName, "camera-b.cvraw");
                int imageId = Seed(a, pathA, 3, "POI A");
                Assert.Equal(imageId, Seed(b, pathB, 5, "POI B"));
                var batchA = new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber };
                FlowMeasurementBatch.Insert(batchA, first).Bind(resources);
                var batchB = new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber, Result = "untouched B" };
                _ = FlowMeasurementBatch.Insert(batchB, second);
                Assert.Equal(batchA.Id, batchB.Id);
                if (failDetailWrite)
                    a.Ado.ExecuteCommand("CREATE TRIGGER reject_detail BEFORE INSERT ON t_scgd_algorithm_result_detail_common BEGIN SELECT RAISE(ABORT, 'synthetic detail failure'); END");

                var services = new TestMtfServices();
                var node = new LocalMtfNode(services) { ResultDirectory = results };
                node.Create();
                var image = new CVStartCFC(action);
                image.MasterValue(null!, imageId, (int)CVCommCore.CVResultType.Camera_Img);
                var poi = new CVStartCFC(action);
                poi.MasterValue(null!, 42, (int)ViewResultAlgType.POI_Y);
                SetInputs(node, action, image, poi);
                List<ResultMessage> notifications = [];
                using var subscription = ResultMessageBus.Default.Subscribe(message =>
                {
                    if (message.EventName == "MTF" && message.SerialNumber == action.SerialNumber) notifications.Add(message);
                });

                current = second;
                if (failDetailWrite)
                {
                    Exception error = Assert.ThrowsAny<Exception>(() => node.ExecuteSynchronously(action));
                    Assert.Contains("synthetic detail failure", error.ToString());
                    Assert.Empty(a.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Empty(a.Queryable<DetailCommonModel>().ToList());
                    Assert.Empty(Directory.EnumerateFiles(results));
                    Assert.Empty(notifications);
                }
                else
                {
                    LocalMtfNodeResultData result = node.ExecuteSynchronously(action);
                    Assert.Equal((imageId, 42, pathA), (result.SourceMasterId, result.PoiSourceMasterId, result.ImageFilePath));
                    AlgResultMasterModel row = Assert.Single(a.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Equal(result.MasterId, row.Id);
                    Assert.Equal(batchA.Id, row.BatchId);
                    Assert.Equal(pathA, row.ImgFile);
                    Assert.Equal("2.0", row.version);
                    DetailCommonModel detail = Assert.Single(a.Queryable<DetailCommonModel>().ToList());
                    Assert.Equal(row.Id, detail.PId);
                    Assert.Equal(result.ResultFilePath, (string?)JObject.Parse(detail.ResultJson)["ResultFileName"]);
                    Assert.Equal("synthetic", (string?)JObject.Parse(File.ReadAllText(result.ResultFilePath))["source"]);
                    ResultMessage notification = Assert.Single(notifications);
                    Assert.Equal(row.Id, notification.Data.MasterId);
                    Assert.Same(first, notification.DatabaseEndpoint);
                }
                Assert.False(first.IsCurrent);
                Assert.Equal(pathA, services.LoadedPath);
                Assert.Equal(new MtfRoi("POI A", 2, 2, 2, 2), Assert.Single(services.Rois));
                Assert.Empty(b.Queryable<AlgResultMasterModel>().ToList());
                Assert.Empty(b.Queryable<DetailCommonModel>().ToList());
                Assert.Equal("untouched B", Assert.Single(b.Queryable<MeasureBatchModel>().ToList()).Result);
            });
        }
        finally
        {
            if (Directory.Exists(results))
            {
                foreach (string file in Directory.EnumerateFiles(results)) File.Delete(file);
                Directory.Delete(results);
            }
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static int Seed(SqlSugarClient db, string filePath, int x, string name)
    {
        db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(PoiPointResultModel),
            typeof(AlgResultMasterModel), typeof(DetailCommonModel));
        db.Insertable(new PoiPointResultModel { Pid = 42, PoiId = 1, PoiName = name, PoiType = POIPointTypes.Rect,
            PoiX = x, PoiY = 3, PoiWidth = 2, PoiHeight = 2, Value = "{}" }).ExecuteCommand();
        return db.Insertable(new MeasureResultImgModel { FileUrl = filePath }).ExecuteReturnIdentity();
    }

    private static void SetInputs(LocalMtfNode node, CVStartCFC action, params CVStartCFC[] inputActions)
    {
        Type baseType = typeof(LocalFlowNodeBase);
        Type snapshotType = baseType.GetNestedType("LocalFlowInputSnapshot", BindingFlags.NonPublic)!;
        MethodInfo create = snapshotType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;
        Array inputs = Array.CreateInstance(snapshotType, inputActions.Length);
        for (int index = 0; index < inputActions.Length; index++)
            inputs.SetValue(create.Invoke(null, [inputActions[index], null]), index);
        var active = (IDictionary)baseType.GetField("activeInputSets", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(node)!;
        active.Add(action.SerialNumber, inputs);
    }

    private sealed class TestMtfServices : ILocalMtfNodeServices
    {
        public string? LoadedPath { get; private set; }
        public MtfRoi[] Rois { get; private set; } = [];

        public LocalRealPoiParameters LoadPoi(int masterId, int resultType, MySqlConnectedEndpoint endpoint) =>
            LocalMtfNodeServices.Instance.LoadPoi(masterId, resultType, endpoint);

        public LocalFlowFrame LoadFrame(string path)
        {
            LoadedPath = path;
            var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 8, Height = 8, SourceBpp = 8,
                Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw }, 64, 0);
            frame.CvRawFilePath = path;
            return frame;
        }

        public JObject Calculate(HImage image, StripeMtfParameters parameters, MtfRoi[] regions)
        {
            Rois = regions;
            return new JObject { ["source"] = "synthetic" };
        }

        public LocalMtfPersistenceResult Persist(LocalMtfPersistenceRequest request) => LocalMtfNodeServices.Instance.Persist(request);

        public void Publish(string serialNumber, string nodeId, int zIndex, int masterId, MySqlConnectedEndpoint endpoint) =>
            LocalMtfNodeServices.Instance.Publish(serialNumber, nodeId, zIndex, masterId, endpoint);
    }
}
