using ColorVision.Algorithms;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.RingGhost;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Ghost;
using ProjectARVRPro.Recipe;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectARVRPro.Tests;

public sealed class GhostFlowAndProcessTests
{
    // Stable contract: valid non-detection is zero; execution/data failures never become passing zeros.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RealRawNodePersistsAndProjectExportsExactlyTwoMetrics(int ghosts)
    {
        using var services = new NodeServices();
        var action = new CVStartCFC("ghost-test");
        action.SetCurrentFrame(PatternFrame(ghosts));
        try
        {
            var node = new LocalRingGhostNode(services) { ImageFilePath = @"C:\must-not-open\fallback.tiff" };
            var completed = node.ExecuteSynchronously(action);
            Assert.Equal(ghosts, completed.Measurement.CandidateCount);
            Assert.Equal(0, services.Opened); Assert.Equal(1, services.Published);
            Assert.Equal(completed.Measurement.GMR1, action.Data["GMR1"]);
            Assert.Equal(completed.Measurement.GMR2, action.Data["GMR2"]);
            Assert.Equal((int)ViewResultAlgType.Ghost, action.Data["MasterResultType"]);
            var roundtrip = LocalRingGhostResult.Read(completed.Saved.ResultFilePath);
            Assert.Equal(completed.Measurement.GMR1, roundtrip.GMR1);
            if (ghosts == 0) Assert.Equal(0, roundtrip.GMR1);
            else Assert.InRange(roundtrip.GMR1, .7, .92);
            if (ghosts < 2) Assert.Equal(0, roundtrip.GMR2);
            else Assert.InRange(roundtrip.GMR2, .24, .39);
            var master = LocalRingGhostResultPersistence.CreateMaster(services.Request!, 5);
            Assert.Equal(LocalRingGhostResult.ResultVersion, master.version); Assert.Equal(ViewResultAlgType.Ghost, master.ImgFileType);
            Assert.Equal(5, master.BatchId); Assert.Equal(0, master.ResultCode);
            var detail = LocalRingGhostResultPersistence.CreateDetail(42, completed.Saved.ResultFilePath);
            Assert.Equal(42, detail.PId);
            Assert.Equal(completed.Saved.ResultFilePath, JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson)!.ResultFileName);

            var process = new RingGhostProcess(new BatchResults(completed.Saved.ResultFilePath));
            var context = Context();
            Assert.True(await process.Execute(context));
            Assert.True(context.Result.Result); Assert.True(context.ObjectiveTestResult.TotalResult);
            Assert.Equal(640, context.Result.ImageWidth);
            var rows = process.GetObjectiveCsvRows(context.Result);
            Assert.Equal(new[] { "GMR1", "GMR2" }, rows.Select(r => r.TestItem));
            Assert.All(rows, row => { Assert.Equal("%", row.Unit); Assert.Equal("PASS", row.TestResult); });
            if (ghosts < 2) Assert.Equal("0", rows[1].MetricValue);
            var customer = JObject.Parse(JsonConvert.SerializeObject(context.ObjectiveTestResult))["RingGhostTestResults"]!["RingGhost"]!;
            Assert.Equal(new[] { "GMR1", "GMR2" }, ((JObject)customer).Properties().Select(p => p.Name));
            var metrics = ObjectiveTestResultMetricCollector.Collect(context.ObjectiveTestResult);
            Assert.Equal(2, metrics.Count); Assert.Equal(new[] { "RingGhost_GMR1", "RingGhost_GMR2" }, metrics.Select(m => m.Header));
            Assert.NotNull(new ObjectiveTestResultValueResolver(context.ObjectiveTestResult).Find("ringghost", "GMR2"));
            string csv = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
            try { ObjectiveTestResultCsvExporter.ExportToCsv(context.ObjectiveTestResult, csv); Assert.Equal(3, File.ReadAllLines(csv).Length); }
            finally { File.Delete(csv); }
            process.Config.OutputKey = "Changed";
            Assert.All(process.GetObjectiveCsvRows(context.Result), row => Assert.Equal("RingGhost", row.TestScreen));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData("blank")]
    [InlineData("saturated")]
    public void FailedDetectionClearsOldValuesAndCannotSaveOrPublishZero(string mode)
    {
        using var services = new NodeServices();
        var action = new CVStartCFC("bad-ghost");
        action.Data["GMR1"] = .3; action.Data["GMR2"] = 0d;
        action.SetCurrentFrame(PatternFrame(2, mode));
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalRingGhostNode(services).ExecuteSynchronously(action));
            Assert.False(action.Data.ContainsKey("GMR1")); Assert.False(action.Data.ContainsKey("GMR2"));
            Assert.Null(services.Request); Assert.Equal(0, services.Published);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void CiePrimaryCannotSilentlyUseDifferentRawPhotometry()
    {
        using var services = new NodeServices();
        var action = new CVStartCFC("cie-boundary");
        action.SetCurrentFrame(LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 10, Height = 10,
            SourceBpp = 16, CieBpp = 32, Channels = 3, PrimaryBufferKind = LocalFrameBufferKind.CvCie }, 10 * 10 * 6, 10 * 10 * 12));
        try
        {
            Assert.Throws<NotSupportedException>(() => new LocalRingGhostNode(services).ExecuteSynchronously(action));
            Assert.Null(services.Request); Assert.Equal(0, services.Published); Assert.False(action.Data.ContainsKey("GMR1"));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void PersistenceFailureDoesNotPublishOrReplaceUpstreamFrame()
    {
        using var services = new NodeServices { FailSave = true };
        var action = new CVStartCFC("bad-save"); var frame = PatternFrame(1); action.SetCurrentFrame(frame);
        try
        {
            Assert.Throws<IOException>(() => new LocalRingGhostNode(services).ExecuteSynchronously(action));
            Assert.False(action.Data.ContainsKey("GMR1")); Assert.Equal(0, services.Published);
            Assert.True(action.TryGetCurrentFrame(out var current)); Assert.Same(frame, current);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void InputImageResultFallbackRetainsMeasuredSourceAssociation()
    {
        using var services = new NodeServices();
        string image = Path.GetTempFileName(); services.ImageFile = image;
        var action = new CVStartCFC("image-fallback"); action.MasterValue(null, 17, (int)CVCommCore.CVResultType.Camera_Img);
        try
        {
            new LocalRingGhostNode(services).ExecuteSynchronously(action);
            Assert.Equal(1, services.Opened); Assert.Equal(image, services.Request!.ImageFile);
            Assert.True(action.TryGetCurrentFrame(out var frame)); Assert.Equal(17, frame!.MasterId);
        }
        finally { action.RuntimeResources.Dispose(); File.Delete(image); }
    }

    [Fact]
    public void BatchSelectionRequiresUniqueSuccessfulLocalResultInCurrentBatch()
    {
        var old = new AlgResultMasterModel { Id = 1, BatchId = 5, ImgFileType = ViewResultAlgType.Ghost, version = "2.0", ResultCode = 0 };
        var local = new AlgResultMasterModel { Id = 42, BatchId = 5, ImgFileType = ViewResultAlgType.Ghost, version = LocalRingGhostResult.ResultVersion, ResultCode = 0 };
        List<DetailCommonModel> Details(int id) => [LocalRingGhostResultPersistence.CreateDetail(id, @"C:\results\ghost.json")];
        Assert.Equal(42, RingGhostBatchResults.Select([old, local], 5, Details).MasterId);
        Assert.Throws<InvalidDataException>(() => RingGhostBatchResults.Select([local], 6, Details));
        Assert.Throws<InvalidDataException>(() => RingGhostBatchResults.Select([local, local], 5, Details));
        local.ResultCode = null; Assert.Throws<InvalidDataException>(() => RingGhostBatchResults.Select([local], 5, Details));
        local.ResultCode = 0; Assert.Throws<InvalidDataException>(() => RingGhostBatchResults.Select([local], 5, _ => []));
    }

    [Fact]
    public async Task MissingMetricIsDataErrorAndOverwritesPreviousSuccess()
    {
        using var services = new NodeServices();
        var action = new CVStartCFC("corrupt"); action.SetCurrentFrame(PatternFrame(1));
        try
        {
            var done = new LocalRingGhostNode(services).ExecuteSynchronously(action);
            var process = new RingGhostProcess(new BatchResults(done.Saved.ResultFilePath)); var ctx = Context();
            Assert.True(await process.Execute(ctx));
            var json = JObject.Parse(File.ReadAllText(done.Saved.ResultFilePath)); json.Remove("GMR2");
            File.WriteAllText(done.Saved.ResultFilePath, json.ToString());
            Assert.False(await process.Execute(ctx)); Assert.False(ctx.Result.Result); Assert.False(ctx.ObjectiveTestResult.TotalResult);
            Assert.Null(ctx.ObjectiveTestResult.RingGhostTestResults["RingGhost"].GMR1);
            Assert.Null(ctx.ObjectiveTestResult.RingGhostTestResults["RingGhost"].GMR2);
            Assert.All(process.GetObjectiveCsvRows(ctx.Result), row => { Assert.Equal("", row.MetricValue); Assert.Equal("DATA_ERROR", row.TestResult); });
            Assert.Null(ctx.Result.FileName);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public async Task InclusiveLimitsAndZeroPolicyUseIndependentRecipeSnapshots()
    {
        using var services = new NodeServices(); var action = new CVStartCFC("limits"); action.SetCurrentFrame(PatternFrame(2));
        try
        {
            var done = new LocalRingGhostNode(services).ExecuteSynchronously(action);
            done.Measurement.GMR1 = 1; done.Measurement.GMR2 = .5;
            Write(done.Saved.ResultFilePath, done.Measurement);
            var process = new RingGhostProcess(new BatchResults(done.Saved.ResultFilePath)); var ctx = Context();
            Assert.True(await process.Execute(ctx)); Assert.True(ctx.Result.Result);
            done.Measurement.GMR2 = .5001; Write(done.Saved.ResultFilePath, done.Measurement);
            ctx = Context(); Assert.True(await process.Execute(ctx)); Assert.False(ctx.Result.Result);
            done.Measurement.CandidateCount = 1; done.Measurement.SingleCandidateAssignedToFirst = true;
            done.Measurement.MeasurementStatus = "SECOND_NOT_DETECTED"; done.Measurement.GMR2 = 0;
            done.Measurement.Regions = done.Measurement.Regions.Take(4).ToArray(); Write(done.Saved.ResultFilePath, done.Measurement);
            process.Config.RecipeConfig.GMR2 = new RecipeBase(0, .5, 1, 4);
            ctx = Context(); Assert.True(await process.Execute(ctx)); Assert.Equal(0, ctx.ObjectiveTestResult.RingGhostTestResults["RingGhost"].GMR2!.Value);
            var restored = new RingGhostProcess(); restored.SetProcessConfig(JsonConvert.SerializeObject(process.Config));
            Assert.Equal(4, restored.Config.RecipeConfig.GMR2.B); Assert.Equal(1, restored.Config.RecipeConfig.GMR1.Max);
            Assert.Equal(ProcessTypeCatalog.DefectSubcategory, ProcessTypeCatalog.GetSubcategory(typeof(RingGhostProcess)));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void NodeConfigurationAndHistoricalHandlerKeepVersionBoundary()
    {
        var node = new LocalRingGhostNode { OrderMapping = RingGhostOrderMapping.InnerIsFirst, ImageFilePath = @"C:\input.tiff", ResultDirectory = @"C:\results" };
        node.Create();
        byte[] bytes = node.GetSaveData(); int position = 0; position += bytes[position] + 1; position += bytes[position] + 1;
        var state = new Dictionary<string, byte[]>();
        while (position < bytes.Length)
        {
            int keyLength = BitConverter.ToInt32(bytes, position); position += 4;
            string key = Encoding.UTF8.GetString(bytes, position, keyLength); position += keyLength;
            int length = BitConverter.ToInt32(bytes, position); position += 4;
            state[key] = bytes.AsSpan(position, length).ToArray(); position += length;
        }
        var restored = new LocalRingGhostNode(); restored.Create(); restored.OnLoadNode(state);
        Assert.Equal(node.OrderMapping, restored.OrderMapping); Assert.Equal(node.ImageFilePath, restored.ImageFilePath);
        Assert.Equal(node.ResultDirectory, restored.ResultDirectory); Assert.Single(restored.GetAllInputOptions()); Assert.Single(restored.GetAllOutputOptions());
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            var handler = new ViewHandleRingGhost();
            Assert.True(handler.CanHandle1(new() { ResultType = ViewResultAlgType.Ghost, Version = LocalRingGhostResult.ResultVersion }));
            Assert.False(handler.CanHandle1(new() { ResultType = ViewResultAlgType.Ghost, Version = "2.0" }));
        });
    }

    [Fact]
    public void SavedRegionsRenderReplaceAndClearWithoutRunningDetection()
    {
        using var services = new NodeServices(); var action = new CVStartCFC("render"); action.SetCurrentFrame(PatternFrame(2));
        try
        {
            var measurement = new LocalRingGhostNode(services).ExecuteSynchronously(action).Measurement;
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                using var view = new ImageView();
                view.ViewBitmapSource = BitmapSource.Create(640, 640, 96, 96, PixelFormats.Gray16, null, new byte[640 * 640 * 2], 640 * 2);
                int baseline = view.ImageShow.Visuals.Count;
                var ctx = Context(); ctx.ImageView = view; ctx.Result.ViewResultJson = JsonConvert.SerializeObject(new RingGhostViewTestResult
                { Measurement = measurement, Status = "PASS" });
                var process = new RingGhostProcess(); process.Render(ctx);
                Assert.Equal(baseline + 1, view.ImageShow.Visuals.Count);
                process.Render(ctx); Assert.Equal(baseline + 1, view.ImageShow.Visuals.Count);
                process.Config.DrawRegions = false; process.Render(ctx); Assert.Equal(baseline, view.ImageShow.Visuals.Count);
            });
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    private static IProcessExecutionContext Context() => new() { Batch = new() { Id = 5 }, Result = new() { Result = true }, ObjectiveTestResult = new() };
    private static void Write(string path, LocalRingGhostResult measurement) => File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(measurement));
    private sealed class BatchResults(string path) : IRingGhostBatchResults
    { public RingGhostBatchInput Resolve(int batchId) => new(path, "", 42); }

    private sealed class NodeServices : ILocalRingGhostNodeServices, IDisposable
    {
        public int Opened, Published; public bool FailSave; public string? ImageFile;
        public LocalRingGhostSaveRequest? Request;
        private readonly List<string> files = [];
        public LocalFlowFrame LoadFrame(string path) { Opened++; return PatternFrame(1); }
        public ColorVision.Engine.MeasureResultImgModel? GetImageResult(int id) => new() { Id = id, FileUrl = ImageFile };
        public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation) =>
            LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
        public LocalRingGhostSavedResult Save(LocalRingGhostSaveRequest request)
        {
            if (FailSave) throw new IOException("simulated save failure");
            Request = request; string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); files.Add(path);
            Write(path, request.Measurement); return new(42, path);
        }
        public void Publish(string serialNumber, string nodeId, int zIndex, int masterId) => Published++;
        public void Dispose() { foreach (string file in files) File.Delete(file); }
    }

    private static LocalFlowFrame PatternFrame(int ghosts, string mode = "rings")
    {
        const int width = 640; const double radius = 160;
        var data = new byte[width * width * 2];
        for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
        {
            double r = Math.Sqrt(Math.Pow(x - 320, 2) + Math.Pow(y - 320, 2));
            double Gaussian(double target) => Math.Exp(-Math.Pow((r - target) / 4, 2) / 2);
            double signal = .6 * Gaussian(radius) + (ghosts > 0 ? .0048 * Gaussian(radius * .65) : 0)
                + (ghosts > 1 ? .0018 * Gaussian(radius * .38) : 0) + .00003;
            if (mode == "blank") signal = 0;
            if (mode == "saturated" && Math.Abs(r - radius) < 2) signal = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 2, 2), (ushort)Math.Clamp(Math.Round(signal * 65535), 0, 65535));
        }
        var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = width, Height = width, SourceBpp = 16,
            Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw }, data.Length, 0);
        using var lease = frame.Acquire(); Marshal.Copy(data, 0, lease.RawPointer, data.Length);
        return frame;
    }
}
