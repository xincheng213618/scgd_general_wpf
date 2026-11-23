using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossNodeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersistedRgbCrossReadsAndWritesTheOriginalDatabaseAfterReconnect(bool rejectDetail)
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo fixture = Directory.CreateTempSubdirectory("cv-rgb-cross-database-");
            string outputDirectory = Path.Combine(fixture.FullName, "results");
            var action = new CVStartCFC("shared-rgb-cross-batch");
            MySqlConnectedEndpoint? active = null;
            bool switchOnOpen = false;
            var replacement = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(active, endpoint), () => Open("replacement.db"));
            var original = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(active, endpoint), () =>
            {
                if (switchOnOpen)
                {
                    switchOnOpen = false;
                    active = replacement;
                }
                return Open("original.db");
            });
            try
            {
                active = original;
                var first = Seed(original, "first.png", x: 40, bind: true);
                var other = Seed(replacement, "other.png", x: 70, bind: false);
                Assert.Equal((first.BatchId, first.ImageId, first.TemplateId), (other.BatchId, other.ImageId, other.TemplateId));
                action.MasterValue(null!, first.ImageId, (int)CVCommCore.CVResultType.Camera_Img);
                if (rejectDetail)
                {
                    using var db = original.CreateDbClient();
                    db.Ado.ExecuteCommand("CREATE TRIGGER reject_rgb_detail BEFORE INSERT ON t_scgd_algorithm_result_detail_common "
                        + "BEGIN SELECT RAISE(ABORT, 'fixture rejected RGB detail'); END;");
                    active = replacement;
                }
                else switchOnOpen = true;

                ResultMessage? published = null;
                using var subscription = ResultMessageBus.Default.Subscribe(message =>
                {
                    if (message.SerialNumber == action.SerialNumber) published = message;
                });
                var node = new LocalRgbCrossNode { SearchRegionPoiTemplate = "shared-roi", ResultDirectory = outputDirectory };
                var saved = rejectDetail ? null : node.ExecuteSynchronously(action);
                if (rejectDetail)
                {
                    Exception error = Assert.ThrowsAny<Exception>(() => node.ExecuteSynchronously(action));
                    Assert.Contains("fixture rejected RGB detail", error.ToString());
                    Assert.Null(published);
                    Assert.False(action.Data.ContainsKey("LocalRgbCrossResultFile"));
                    Assert.Empty(Directory.EnumerateFiles(outputDirectory));
                }

                Assert.False(original.IsCurrent);
                using var dbOriginal = original.CreateDbClient();
                using var dbReplacement = replacement.CreateDbClient();
                Assert.Empty(dbReplacement.Queryable<AlgResultMasterModel>().ToList());
                Assert.Empty(dbReplacement.Queryable<DetailCommonModel>().ToList());
                if (rejectDetail)
                {
                    Assert.Empty(dbOriginal.Queryable<AlgResultMasterModel>().ToList());
                    Assert.Empty(dbOriginal.Queryable<DetailCommonModel>().ToList());
                    return;
                }

                var master = Assert.Single(dbOriginal.Queryable<AlgResultMasterModel>().ToList());
                Assert.Equal(first.BatchId, master.BatchId);
                Assert.Equal(first.ImagePath, master.ImgFile);
                Assert.Equal(ViewResultAlgType.FindCross, master.ImgFileType);
                JObject audit = JObject.Parse(master.Params!);
                Assert.Equal(first.ImageId, audit.Value<int>("SourceMasterId"));
                Assert.Equal(40, audit["SearchRegion"]!.Value<int>("X"));
                Assert.Equal("2.0", master.version);
                var detail = Assert.Single(dbOriginal.Queryable<DetailCommonModel>().ToList());
                Assert.Equal(master.Id, detail.PId);
                Assert.Equal(saved!.ResultFilePath, JObject.Parse(detail.ResultJson).Value<string>("ResultFileName"));
                Assert.Equal("colorvision.rgb-cross-measurement", JObject.Parse(File.ReadAllText(saved.ResultFilePath)).Value<string>("schemaId"));
                var message = Assert.IsType<ResultMessage>(published);
                Assert.Same(original, message.DatabaseEndpoint);
                Assert.Equal(master.Id, message.Data.MasterId);
            }
            finally
            {
                action.RuntimeResources.Dispose();
                if (Directory.Exists(outputDirectory))
                {
                    foreach (string file in Directory.EnumerateFiles(outputDirectory)) File.Delete(file);
                    Directory.Delete(outputDirectory);
                }
                foreach (FileInfo file in fixture.EnumerateFiles()) file.Delete();
                fixture.Delete();
            }

            SqlSugarClient Open(string name) => new(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Path.Combine(fixture.FullName, name)};Pooling=False",
                DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
            });

            (int BatchId, int ImageId, int TemplateId, string ImagePath) Seed(MySqlConnectedEndpoint endpoint, string fileName, int x, bool bind)
            {
                string imagePath = Path.Combine(fixture.FullName, fileName);
                var bitmap = BitmapSource.Create(400, 400, 96, 96, PixelFormats.Bgr24, null, new byte[400 * 400 * 3], 400 * 3);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var image = File.Create(imagePath)) encoder.Save(image);

                using var db = endpoint.CreateDbClient();
                db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel), typeof(PoiMasterModel),
                    typeof(PoiDetailModel), typeof(AlgResultMasterModel), typeof(DetailCommonModel));
                var batch = new MeasureBatchModel { Code = action.SerialNumber };
                var receipt = FlowMeasurementBatch.Insert(batch, endpoint);
                if (bind) receipt.Bind(action.RuntimeResources);
                int imageId = db.Insertable(new MeasureResultImgModel { BatchId = batch.Id, FileUrl = imagePath }).ExecuteReturnIdentity();
                int templateId = db.Insertable(new PoiMasterModel
                {
                    Name = "shared-roi", Type = 0, Width = 400, Height = 400, CfgJson = "{}", Remark = string.Empty,
                    LeftTopX = 0, LeftTopY = 0, RightTopX = 399, RightTopY = 0,
                    RightBottomX = 399, RightBottomY = 399, LeftBottomX = 0, LeftBottomY = 399
                }).ExecuteReturnIdentity();
                db.Insertable(new PoiDetailModel
                {
                    Pid = templateId, Name = "region", Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType,
                    PixX = x, PixY = 50, PixWidth = 300, PixHeight = 300
                }).ExecuteCommand();
                return (batch.Id, imageId, templateId, imagePath);
            }
        });
    }

    [Fact]
    public void ReusesFindCrossStorageAndReloadsPoiOnEachExecution()
    {
        var services = new Services { ExpectedThreshold = 0.6 }; var node = new LocalRgbCrossNode(services) { ParameterJson = "{\"TargetThreshold\":0.6}", SearchRegionPoiTemplate = "发光区", SearchRegion = new(900,900,40,40) };
        var action = new CVStartCFC("nine-test");
        var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 400, Height = 400, SourceBpp = 16, Channels = 3, PrimaryBufferKind = LocalFrameBufferKind.CvRaw }, 400*400*6, 0);
        using (var lease = frame.Acquire()) System.Runtime.InteropServices.Marshal.Copy(new byte[lease.RawLength], 0, lease.RawPointer, lease.RawLength);
        action.SetCurrentFrame(frame);
        try
        {
            node.ExecuteSynchronously(action); node.ExecuteSynchronously(action);
            Assert.Equal(2, services.Reads); Assert.Equal(2, services.Published);
            Assert.Equal(63, action.Data["MasterResultType"]);
            Assert.Equal(123, action.Data["MasterId"]);
            var saved = services.Request!;
            var master = LocalRgbCrossResultPersistence.CreateMaster(saved, 17);
            Assert.Equal(ViewResultAlgType.FindCross, master.ImgFileType); Assert.Equal("2.0", master.version);
            Assert.Null(master.TId); Assert.Equal("LocalRgbCross", master.TName); Assert.Equal(17, master.BatchId);
            Assert.Equal("INVALID", master.Result); Assert.Equal(0, master.ResultCode);
            var detail = LocalFindCrossResultPersistence.CreateDetail(123, @"C:\Results\nine.json");
            Assert.Equal(123, detail.PId); Assert.Equal(@"C:\Results\nine.json", JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson)!.ResultFileName);
            Assert.Equal("colorvision.rgb-cross-measurement", saved.Measurement.GetProperty("schemaId").GetString());
            Assert.Equal(9, saved.Measurement.GetProperty("points").GetArrayLength());
            Assert.DoesNotContain("limit", saved.Measurement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void LegacyTemplateSelectionIsIgnoredAndRemainingSettingsRoundTrip()
    {
        var node = new LocalRgbCrossNode { SearchRegionPoiTemplate = "发光区", SearchRegion = new(10,20,300,300),
            ParameterJson = "{\"MinimumArmCoverage\":0.75}", ResultDirectory = @"C:\results\FindCross" };
        node.Create();
        node.Title = "九点十字 RGB 分离";
        var state = ParseState(node.GetSaveData());
        Assert.False(state.ContainsKey("TemplateName"));
        state["TemplateName"] = Encoding.UTF8.GetBytes("已删除的旧模板");
        var restored = new LocalRgbCrossNode(); restored.Create(); restored.OnLoadNode(state);
        Assert.Equal(node.SearchRegionPoiTemplate, restored.SearchRegionPoiTemplate);
        Assert.Equal(node.SearchRegion, restored.SearchRegion);
        Assert.Equal(node.ResultDirectory, restored.ResultDirectory);
        Assert.Equal(node.ParameterJson, restored.ParameterJson);
        state.Remove("ParameterJson");
        var legacy = new LocalRgbCrossNode(); legacy.Create(); legacy.OnLoadNode(state);
        Assert.Equal("{}", legacy.ParameterJson);
        Assert.False(ParseState(restored.GetSaveData()).ContainsKey("TemplateName"));
        Assert.Equal("LocalRgbCross", restored.NodeType);
        Assert.Equal("十字 RGB 分离", restored.Title);
        Assert.Single(restored.GetAllInputOptions()); Assert.Single(restored.GetAllOutputOptions());
    }

    private sealed class Services : ILocalRgbCrossNodeServices
    {
        public double ExpectedThreshold = 0.5;
        public int Reads, Published; public LocalRgbCrossSaveRequest? Request;
        public string ResolveResultDirectory(string configuredDirectory, MySqlConnectedEndpoint? endpoint) => @"C:\Results";
        public Int32Rect LoadSearchRegionTemplate(string name, MySqlConnectedEndpoint? endpoint) => new(50 + Reads++,50,300,300);
        public LocalFlowFrame LoadFrame(string path) => throw new InvalidOperationException();
        public MeasureResultImgModel? GetImageResult(int id, MySqlConnectedEndpoint? endpoint) => throw new InvalidOperationException();
        public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation)
        {
            Assert.Equal(49+Reads, ((RectangleAlgorithmRoi)invocation.Roi!).X);
            Assert.Equal(ExpectedThreshold, invocation.Parameters.GetProperty("targetThreshold").GetDouble());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, invocation.Parameters.GetProperty("maximumEdgeSeparationPixels").ValueKind);
            return LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
        }
        public LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request) { Request=request;return new(123,@"C:\Results\nine.json"); }
        public void Publish(string serialNumber, string nodeId, int zIndex, int masterId, MySqlConnectedEndpoint? endpoint) => Published++;
    }
    private static Dictionary<string, byte[]> ParseState(byte[] data)
    {
        int position = 0;
        position += data[position] + 1;
        position += data[position] + 1;
        Dictionary<string, byte[]> state = new();
        while (position < data.Length)
        {
            int keyLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            string key = Encoding.UTF8.GetString(data, position, keyLength);
            position += keyLength;
            int valueLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            byte[] value = new byte[valueLength];
            Array.Copy(data, position, value, 0, valueLength);
            position += valueLength;
            state.Add(key, value);
        }
        return state;
    }
}
