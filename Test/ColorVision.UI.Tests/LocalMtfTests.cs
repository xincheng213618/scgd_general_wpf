using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Algorithm.LocalMtf;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class LocalMtfTests
{
    private static JObject Parameters => JObject.Parse("""{"pattern":1,"CalcMethod":0,"PercentageDisplay":false}""");
    private static JObject Result => JObject.Parse("""{"result":[{"name":"Center","x":10,"y":10,"w":10,"h":10,"mtfValue":0.5}]}""");

    [Theory]
    [InlineData(PoiShape.Rect, 20.5, 11, 15)]
    [InlineData(PoiShape.Rect, 20, 11, 14)]
    [InlineData(PoiShape.LeftTopRect, 20.5, 11, 20)]
    public void RoiCoordinatesMatchServiceFloatAndMidpointRules(PoiShape shape, double x, double size, int left)
    {
        MtfRoi roi = Assert.Single(LocalMtfRoiAdapter.BuildRegions(new[]
        {
            new PoiPoint { Name = "中心", PointType = shape, PixX = x, PixY = x, PixWidth = size, PixHeight = size }
        }, 100, 100));
        Assert.Equal(left, roi.x);
        Assert.Equal(left, roi.y);
        Assert.Equal(11, roi.w);
    }

    [Theory]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, 0, 0, 10)]
    [InlineData(95, 0, 10, 10)]
    public void InvalidRegionsAreRejectedBeforeNativeCode(int x, int y, int w, int h) =>
        Assert.Throws<InvalidOperationException>(() => VendorMtfReference.ValidateRegions([new("Center", x, y, w, h)], 100, 100));

    [Fact]
    public void DuplicateNamesAndMissingFourPartConfigurationAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => VendorMtfReference.ValidateRegions([new("C", 0, 0, 10, 10), new("C", 20, 20, 10, 10)], 100, 100));
        Assert.Throws<InvalidOperationException>(() => VendorMtfReference.ParseParameters("""{"pattern":5,"CalcMethod":0}"""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("-1")]
    [InlineData("\"NaN\"")]
    public void MissingOrInvalidMeasurementDoesNotBecomeZero(string value)
    {
        JObject result = Result;
        result["result"]![0]!["mtfValue"] = JToken.Parse(value);
        Assert.Throws<InvalidOperationException>(() => VendorMtfReference.ValidateResult(result, 1, [new("Center", 0, 0, 32, 32)], 32, 32));
    }

    [Fact]
    public void FourPartResultCannotOmitItsHorizontalVerticalGroups()
    {
        JObject result = Result;
        result["result"] = new JArray(Enumerable.Range(0, 4).Select(id => new JObject
        {
            ["name"] = "Center", ["id"] = id, ["x"] = 10, ["y"] = 10, ["w"] = 2, ["h"] = 2, ["mtfValue"] = 0.5
        }));
        Assert.Throws<InvalidOperationException>(() => VendorMtfReference.ValidateResult(result, 5, [new("Center", 0, 0, 32, 32)], 32, 32));
    }

    [Fact]
    public void NodeUsesImageAndPoiPortsAndHasNoDeviceBinding()
    {
        LocalMtfNode node = new();
        node.Create();
        Assert.Equal(new[] { "IN_IMG", "IN_POI" }, node.GetAllInputOptions().Select(option => option.Text));
        Assert.False(typeof(IFlowDeviceNode).IsAssignableFrom(typeof(LocalMtfNode)));
    }

    [Fact]
    public void MemoryFrameAndDirectParametersNeedNoTemplates()
    {
        FakeServices services = new();
        LocalMtfNode node = new(services) { ResultName = "MTF_HV", ParameterJson = Parameters.ToString() };
        CVStartCFC action = CreateAction();
        try
        {
            LocalMtfNodeResultData first = node.ExecuteSynchronously(action);
            Assert.Equal(new MtfRoi("Center", 0, 0, 32, 32), Assert.Single(services.LastRegions!));
            node.ParameterJson = new StripeMtfParameters { Pattern = StripeMtfPattern.Vertical }.ToJson().ToString();
            node.ExecuteSynchronously(action);
            Assert.Equal(StripeMtfPattern.Vertical, services.LastParameters!.Pattern);
            Assert.Equal(0, services.PoiReads);
            Assert.Equal(2, services.Calculations);
            Assert.Equal(2, services.Published);
            Assert.Equal(91, first.MasterId);
            Assert.Equal(42, first.SourceMasterId);
            Assert.Equal((int)ViewResultAlgType.MTF, action.Data["MasterResultType"]);
            Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? frame));
            using LocalFlowFrameLease lease = frame!.Acquire();
            Assert.NotEqual(IntPtr.Zero, lease.RawPointer);
            Assert.Null(services.LastRequest!.ImageFilePath);
            Assert.Equal("MTF_HV", services.LastRequest.TemplateName);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlgorithmAndPersistenceFailureDoNotPublishSuccess(bool algorithmFailure)
    {
        FakeServices services = new() { FailCalculation = algorithmFailure, FailSave = !algorithmFailure };
        LocalMtfNode node = new(services) { ResultName = "MTF_HV", ParameterJson = Parameters.ToString() };
        CVStartCFC action = CreateAction();
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
            Assert.Equal(0, services.Published);
            Assert.False(action.Data.ContainsKey("LocalMtfResultFile"));
            Assert.NotEqual(91, action.Data.GetValueOrDefault("MasterId"));
            Assert.Equal(algorithmFailure ? 0 : 1, services.Saves);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void PersistenceCommitsMasterAndDetailTogetherAndCleansOrphans(int detailCount)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ColorVision-MtfTest-" + Guid.NewGuid().ToString("N"));
        FakeTransaction transaction = new() { DetailCount = detailCount };
        CVStartCFC action = new("MTF-transaction-test");
        AlgResultMasterModel master = LocalMtfResultPersistence.CreateMaster(new(action, "MTF_HV", null, -1, 2, new { }, Result, directory), 12);
        Assert.Equal("2.0", master.version);
        Assert.Equal(ViewResultAlgType.MTF, master.ImgFileType);
        Assert.Null(master.DeviceCode);
        try
        {
            if (detailCount == 0)
            {
                Assert.Throws<InvalidOperationException>(() => LocalMtfResultPersistence.SaveCore(master, Result, directory, () => transaction));
                Assert.True(transaction.RolledBack);
                Assert.Empty(Directory.GetFiles(directory));
            }
            else
            {
                LocalMtfPersistenceResult saved = LocalMtfResultPersistence.SaveCore(master, Result, directory, () => transaction);
                Assert.True(transaction.Committed);
                Assert.Equal(91, saved.MasterId);
                Assert.Equal(saved.ResultFilePath, JObject.Parse(transaction.Detail!.ResultJson).Value<string>("ResultFileName"));
                MTFResult legacy = JsonConvert.DeserializeObject<MTFResult>(File.ReadAllText(saved.ResultFilePath))!;
                Assert.Equal(0.5, Assert.Single(legacy.result).mtfValue);
                MTFDetailViewReslut historical = new(transaction.Detail);
                Assert.Equal(saved.ResultFilePath, historical.ResultFileName);
                Assert.Equal(0.5, Assert.Single(historical.MTFResult!.result).mtfValue);
            }
        }
        finally
        {
            foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
            action.RuntimeResources.Dispose();
        }
    }

    private static CVStartCFC CreateAction()
    {
        CVStartCFC action = new("MTF-test");
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 32, Height = 32, SourceBpp = 16, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw
        }, 32 * 32 * 2, 0);
        frame.MasterId = 42;
        action.SetCurrentFrame(frame);
        return action;
    }

    private sealed class FakeServices : ILocalMtfNodeServices
    {
        public int PoiReads, Calculations, Saves, Published;
        public bool FailCalculation, FailSave;
        public LocalMtfPersistenceRequest? LastRequest;
        public StripeMtfParameters? LastParameters;
        public MtfRoi[]? LastRegions;
        public LocalRealPoiParameters LoadPoi(int masterId, int resultType, string templateName)
        {
            PoiReads++;
            PoiParam poi = new() { Name = "ROI" };
            poi.PoiPoints.Add(new() { Name = "Center", PointType = PoiShape.LeftTopRect, PixX = 0, PixY = 0, PixWidth = 32, PixHeight = 32 });
            return new() { Poi = poi, SourceMasterId = 43 };
        }
        public LocalFlowFrame LoadFrame(string path) => throw new InvalidOperationException("The memory test must not read a file.");
        public JObject Calculate(HImage image, StripeMtfParameters parameters, MtfRoi[] regions)
        {
            Calculations++;
            LastParameters = parameters; LastRegions = regions;
            if (FailCalculation) throw new InvalidOperationException("Native failure");
            return Result;
        }
        public LocalMtfPersistenceResult Persist(LocalMtfPersistenceRequest request)
        {
            Saves++;
            if (FailSave) throw new InvalidOperationException("Database failure");
            LastRequest = request;
            return new(91, @"C:\test\mtf.json");
        }
        public void Publish(string serialNumber, string nodeId, int zIndex, int masterId) => Published++;
    }

    private sealed class FakeTransaction : ILocalFlowResultTransaction<DetailCommonModel>
    {
        public int DetailCount = 1;
        public bool Committed, RolledBack;
        public DetailCommonModel? Detail;
        public void Begin() { }
        public int InsertMaster(AlgResultMasterModel model) => 91;
        public int InsertDetails(IReadOnlyCollection<DetailCommonModel> details) { Detail = Assert.Single(details); return DetailCount; }
        public void Commit() => Committed = true;
        public void Rollback() => RolledBack = true;
        public void Dispose() { }
    }
}

public sealed class MtfFieldFactAttribute : FactAttribute
{
    public MtfFieldFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_MTF_FIELD_CASE")))
            Skip = "Set COLORVISION_MTF_FIELD_CASE to an offline case JSON containing ImagePath, AlgorithmDirectory, Parameters, RoiRects and Expected.";
    }
}

[CollectionDefinition("MTF offline comparison", DisableParallelization = true)]
public sealed class MtfOfflineComparisonCollection { }

[Collection("MTF offline comparison")]
public sealed class LocalMtfFieldTests(ITestOutputHelper output)
{
    [MtfFieldFact]
    public void NativeAdapterMatchesSavedFieldRectanglesAndHorizontalVerticalValues()
    {
        JObject testCase = JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("COLORVISION_MTF_FIELD_CASE")!));
        using LocalFlowFrame frame = LocalFrameFileService.Load(testCase.Value<string>("ImagePath")!);
        using LocalFlowFrameLease lease = frame.Acquire();
        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
        MtfRoi[] regions = testCase["RoiRects"]!.ToObject<MtfRoi[]>()!;
        JObject expected = (JObject)testCase["Expected"]!;
        List<double> elapsed = [];
        JObject? actual = null;
        for (int run = 0; run < 3; run++)
        {
            Stopwatch timer = Stopwatch.StartNew();
            actual = VendorMtfReference.Calculate(image, testCase.Value<string>("AlgorithmDirectory")!, (JObject)testCase["Parameters"]!, regions);
            elapsed.Add(timer.Elapsed.TotalMilliseconds);
            foreach (JToken rectangle in expected["result"]!)
            {
                JToken match = actual["result"]!.Single(item => item.Value<string>("name") == rectangle.Value<string>("name") && item.Value<int>("id") == rectangle.Value<int>("id"));
                foreach (string key in new[] { "x", "y", "w", "h", "mtfValue" }) Assert.Equal(rectangle.Value<double>(key), match.Value<double>(key));
            }
            foreach (JToken group in expected["resultChild"]!)
            {
                JToken match = actual["resultChild"]!.Single(item => item.Value<string>("name") == group.Value<string>("name"));
                foreach (string key in new[] { "Average", "horizontalAverage", "verticalAverage" }) Assert.Equal(group.Value<double>(key), match.Value<double>(key));
            }
        }
        output.WriteLine($"Regions={regions.Length}; rectangles={actual!["result"]!.Count()}; native adapter ms={string.Join(", ", elapsed)}; exact numeric parity across three runs.");
        if (Environment.GetEnvironmentVariable("COLORVISION_MTF_FIELD_REPORT") is string report)
            File.WriteAllText(report, JsonConvert.SerializeObject(new { ElapsedMilliseconds = elapsed, Result = actual }, Formatting.Indented));
    }
}
