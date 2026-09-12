using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Distortion;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalGridDistortionNodeTests
{
    [Fact]
    public void TypedConfigurationRoundTripsAndNodeHasOneImageInput()
    {
        LocalGridDistortionNode node = new();
        node.Create();
        Assert.Equal("LocalGridDistortion", node.NodeType);
        Assert.Equal("本地点阵畸变(V2)", node.Title);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(3, node.ExpectedRows);
        Assert.Equal(3, node.ExpectedCols);
        Assert.Equal(0.02, node.MinimumContrast);
        Assert.Equal(GridTvFormula.Standard, node.TvFormula);
        Assert.Equal(GridPoint9Formula.OppositeEdgeMean, node.Point9Formula);
        Assert.False(node.PublishOpticalEstimate);
        Assert.Equal(Int32Rect.Empty, node.SearchRegion);
        node.ExpectedRows = 7;
        node.ExpectedCols = 9;
        node.MinimumContrast = 0.04;
        node.ImageFilePath = @"C:\images\dots.cvraw";
        node.SearchRegion = new Int32Rect(10, 20, 400, 300);
        node.ResultDirectory = @"C:\results\distortion";
        node.TvFormula = GridTvFormula.Half;
        node.Point9Formula = GridPoint9Formula.LegacyThreeSpanMean;
        node.PublishOpticalEstimate = true;
        LocalGridDistortionNode restored = new();
        restored.Create();
        restored.OnLoadNode(ParseState(node.GetSaveData()));
        Assert.Equal(node.ExpectedRows, restored.ExpectedRows);
        Assert.Equal(node.ExpectedCols, restored.ExpectedCols);
        Assert.Equal(node.MinimumContrast, restored.MinimumContrast);
        Assert.Equal(node.ImageFilePath, restored.ImageFilePath);
        Assert.Equal(node.SearchRegion, restored.SearchRegion);
        Assert.Equal(node.ResultDirectory, restored.ResultDirectory);
        Assert.Equal(node.TvFormula, restored.TvFormula);
        Assert.Equal(node.Point9Formula, restored.Point9Formula);
        Assert.Equal(node.PublishOpticalEstimate, restored.PublishOpticalEstimate);
    }

    [Fact]
    public void MemoryExecutionPreservesPixelsAndSnapshotsConfigurationBeforePersistenceAndPublish()
    {
        CVStartCFC action = CreateAction();
        Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? frame));
        frame!.MasterId = 73;
        using LocalFlowFrameLease lease = frame.Acquire();
        byte[] pixels = Enumerable.Repeat((byte)37, 64 * 48).ToArray();
        Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
        GridDistortionResult detection = CreateDetection(7, 7);
        LocalGridDistortionPersistenceRequest? persisted = null;
        List<string> events = [];
        LocalGridDistortionNode? node = null;
        FakeServices services = new()
        {
            DetectHandler = (image, roi, options) =>
            {
                events.Add("Detect");
                Assert.Equal(lease.RawPointer, image.pData);
                Assert.Equal(64, image.cols);
                Assert.Equal(48, image.rows);
                Assert.Equal(7, options.ExpectedRows);
                Assert.Equal(7, options.ExpectedCols);
                Assert.Equal(0, roi.Width);
                node!.ExpectedRows = 9;
                node.MinimumContrast = 0.9;
                node.ResultDirectory = @"C:\changed";
                node.TvFormula = GridTvFormula.Standard;
                node.Point9Formula = GridPoint9Formula.OppositeEdgeMean;
                node.PublishOpticalEstimate = false;
                return detection;
            },
            PersistHandler = request =>
            {
                events.Add("Persist");
                persisted = request;
                Assert.False(action.Data.ContainsKey("LocalGridDistortionResult"));
                return new() { MasterId = 311, ResultFilePath = @"C:\results\grid.json" };
            },
            PublishHandler = request =>
            {
                events.Add("Publish");
                Assert.Equal(311, request.MasterId);
                Assert.Equal(311, Convert.ToInt32(action.Data["MasterId"]));
                Assert.Same(detection, action.Data["LocalGridDistortionResult"]);
            }
        };
        node = new(services)
        {
            ExpectedRows = 7, ExpectedCols = 7, ImageFilePath = @"C:\missing-file.cvraw", ResultDirectory = @"C:\initial",
            TvFormula = GridTvFormula.Half, Point9Formula = GridPoint9Formula.LegacyThreeSpanMean, PublishOpticalEstimate = true
        };
        try
        {
            LocalGridDistortionNodeResultData result = node.ExecuteSynchronously(action);
            Assert.Equal(["Detect", "Persist", "Publish"], events);
            Assert.Equal(73, result.SourceMasterId);
            Assert.Equal(9, result.MasterResultType);
            Assert.Equal(frame.FrameId.ToString("N"), result.FrameId);
            Assert.Equal(@"C:\initial", persisted!.ResultDirectory);
            JObject parameters = JObject.FromObject(persisted.Parameters);
            Assert.Equal(7, parameters["Options"]!.Value<int>("ExpectedRows"));
            Assert.Equal(0.02, parameters["Options"]!.Value<double>("MinimumContrast"));
            Assert.True(parameters.Value<bool>("MemoryOnly"));
            Assert.False(parameters.Value<bool>("ImageRead"));
            Assert.Equal("percent", parameters.Value<string>("Units"));
            Assert.Same(result.Analysis, action.Data["LocalGridDistortionAnalysis"]);
            Assert.Same(result.Analysis, persisted.Analysis);
            Assert.Equal(GridTvFormula.Half, persisted.TvFormula);
            Assert.Equal(GridPoint9Formula.LegacyThreeSpanMean, persisted.Point9Formula);
            Assert.True(persisted.PublishOpticalEstimate);
            Assert.Equal(result.Analysis.HalfTv.HorizontalPercent, action.Data["LocalGridDistortionHorizontalTvPercent"]);
            Assert.NotNull(parameters["Analysis"]?["Optical"]);
            Assert.False(action.Data.ContainsKey("DIFF_H"));
            byte[] after = new byte[pixels.Length];
            Marshal.Copy(lease.RawPointer, after, 0, after.Length);
            Assert.Equal(pixels, after);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void CieFrameBorrowsYPlaneAndKeepsSourceMaster()
    {
        CVStartCFC action = CreateAction(cie: true);
        action.TryGetCurrentFrame(out LocalFlowFrame? frame);
        frame!.MasterId = 84;
        using LocalFlowFrameLease lease = frame.Acquire();
        FakeServices services = new()
        {
            DetectHandler = (image, _, _) =>
            {
                Assert.Equal(IntPtr.Add(lease.CiePointer, 64 * 48 * sizeof(float)), image.pData);
                Assert.Equal(32, image.depth);
                Assert.Equal(1, image.channels);
                Assert.Equal(64 * sizeof(float), image.stride);
                return CreateDetection();
            }
        };
        try
        {
            LocalGridDistortionNodeResultData result = new LocalGridDistortionNode(services).ExecuteSynchronously(action);
            Assert.Equal(84, result.SourceMasterId);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileSourcesTransferLoadedFrameOnlyAfterSuccessfulPersistence(bool useImageResult)
    {
        string path = Path.GetTempFileName();
        CVStartCFC action = new("grid-file");
        LocalFlowFrame loaded = CreateFrame();
        FakeServices services = new()
        {
            GetImageResultHandler = masterId => { Assert.Equal(41, masterId); return new MeasureResultImgModel { FileUrl = path }; },
            LoadFrameHandler = file => { Assert.Equal(path, file); return loaded; },
            PersistHandler = request =>
            {
                Assert.False(action.TryGetCurrentFrame(out _));
                Assert.Equal(path, request.ImageFilePath);
                Assert.True(JObject.FromObject(request.Parameters).Value<bool>("ImageRead"));
                return new() { MasterId = 303, ResultFilePath = @"C:\grid.json" };
            }
        };
        if (useImageResult)
        {
            action.Data["MasterId"] = 41;
            action.Data["MasterResultType"] = (int)CVCommCore.CVResultType.Camera_Img;
        }
        LocalGridDistortionNode node = new(services) { ImageFilePath = useImageResult ? string.Empty : path };
        try
        {
            LocalGridDistortionNodeResultData result = node.ExecuteSynchronously(action);
            Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? current));
            Assert.Same(loaded, current);
            Assert.Equal(useImageResult ? 41 : -1, result.SourceMasterId);
        }
        finally { action.RuntimeResources.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void InvalidConfiguredFileDoesNotFallBackToInputResult()
    {
        CVStartCFC action = new("grid-missing");
        action.Data["MasterId"] = 41;
        action.Data["MasterResultType"] = (int)CVCommCore.CVResultType.Camera_Img;
        FakeServices services = new();
        LocalGridDistortionNode node = new(services) { ImageFilePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.cvraw") };
        Assert.Throws<FileNotFoundException>(() => node.ExecuteSynchronously(action));
        Assert.Equal(0, services.DetectCount);
        Assert.Equal(0, services.PersistCount);
    }

    [Fact]
    public void UnappliedDirectionFailsBeforeDetection()
    {
        CVStartCFC action = CreateAction(flip: CVImageFlipMode.X);
        FakeServices services = new();
        try
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new LocalGridDistortionNode(services).ExecuteSynchronously(action));
            Assert.Contains("方向变换尚未完成", error.Message);
            Assert.Equal(0, services.DetectCount);
            Assert.Equal(0, services.PersistCount);
            Assert.Equal(0, services.PublishCount);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(4, 7, 0.02)]
    [InlineData(7, 17, 0.02)]
    [InlineData(7, 7, -1)]
    [InlineData(7, 7, double.NaN)]
    public void InvalidProductConfigurationFailsBeforeLoading(int rows, int cols, double contrast)
    {
        FakeServices services = new();
        LocalGridDistortionNode node = new(services) { ExpectedRows = rows, ExpectedCols = cols, MinimumContrast = contrast };
        Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(new CVStartCFC("invalid-config")));
        Assert.Equal(0, services.DetectCount);
        Assert.Equal(0, services.PersistCount);
    }

    [Theory]
    [InlineData("IncompleteGrid")]
    [InlineData("ResultParseFailed")]
    [InlineData("NativeLibraryUnavailable")]
    public void RejectionPublishesFailedMasterWithoutOverwritingExistingMainResult(string failureStatus)
    {
        CVStartCFC action = CreateAction();
        action.Data["MasterId"] = 81;
        action.Data["MasterResultType"] = 22;
        action.Data["MasterValue"] = "existing-result";
        FakeServices services = new()
        {
            DetectHandler = (_, _, _) => GridDistortionResult.CreateFailure(failureStatus, "Detection cannot produce a usable grid"),
            PersistHandler = request =>
            {
                Assert.Equal(-1, request.ResultCode);
                Assert.Null(request.Result);
                Assert.Contains(failureStatus, request.ResultDescription);
                Assert.False(JObject.FromObject(request.Parameters)["Detection"]!.Value<bool>("Success"));
                return new() { MasterId = 401 };
            },
            PublishHandler = request => Assert.Equal(401, request.MasterId)
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalGridDistortionNode(services).ExecuteSynchronously(action));
            Assert.Equal(81, action.Data["MasterId"]);
            Assert.Equal(22, action.Data["MasterResultType"]);
            Assert.Equal("existing-result", action.Data["MasterValue"]);
            Assert.False(action.Data.ContainsKey("LocalGridDistortionResult"));
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void AnalysisRejectsDegenerateCoordinatesThroughFailurePersistence()
    {
        CVStartCFC action = CreateAction();
        action.Data["MasterValue"] = "original";
        GridDistortionResult detection = CreateDetection();
        detection = detection with { Points = detection.Points.Select(point => point with { X = 29, Y = 23 }).ToArray() };
        FakeServices services = new()
        {
            DetectHandler = (_, _, _) => detection,
            PersistHandler = request =>
            {
                Assert.Equal(-1, request.ResultCode);
                Assert.Null(request.Result);
                Assert.Null(request.Analysis);
                Assert.Contains("参考跨度退化", request.ResultDescription);
                Assert.NotNull(JObject.FromObject(request.Parameters)["Detection"]);
                return new() { MasterId = 402 };
            }
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalGridDistortionNode(services).ExecuteSynchronously(action));
            Assert.Equal("original", action.Data["MasterValue"]);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersistenceAndPublishingFailuresKeepDistinctCommitStates(bool failPublish)
    {
        CVStartCFC action = CreateAction();
        action.Data["MasterId"] = 81;
        FakeServices services = new()
        {
            PersistHandler = _ => failPublish ? new() { MasterId = 501, ResultFilePath = @"C:\committed.json" } : throw new InvalidOperationException("save failed"),
            PublishHandler = _ => throw new InvalidOperationException("publish failed")
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalGridDistortionNode(services).ExecuteSynchronously(action));
            Assert.Equal(failPublish ? 501 : 81, action.Data["MasterId"]);
            Assert.Equal(failPublish, action.Data.ContainsKey("LocalGridDistortionResult"));
            Assert.Equal(failPublish ? 1 : 0, services.PublishCount);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void FailedFileExecutionDisposesFrameWithoutTransferringOwnership()
    {
        string path = Path.GetTempFileName();
        CVStartCFC action = new("grid-file-failed");
        LocalFlowFrame loaded = CreateFrame();
        FakeServices services = new()
        {
            LoadFrameHandler = _ => loaded,
            PersistHandler = _ => throw new InvalidOperationException("save failed")
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalGridDistortionNode(services) { ImageFilePath = path }.ExecuteSynchronously(action));
            Assert.False(action.TryGetCurrentFrame(out _));
            Assert.Throws<ObjectDisposedException>(() => loaded.Acquire());
            Assert.Equal(0, services.PublishCount);
        }
        finally { action.RuntimeResources.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void GridValidationRejectsFalseSuccessInvalidReferencesAndZeroSpanMetrics()
    {
        GridDistortionOptions options = new() { ExpectedRows = 7, ExpectedCols = 7 };
        GridDistortionResult valid = CreateDetection(7, 7);
        LocalGridDistortionNode.ValidateDetection(valid, options, 64, 48);
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { Metrics = null }, options, 64, 48));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { Metrics = new() }, options, 64, 48));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { ReferencePointIds = Enumerable.Range(0, 9).ToArray() }, options, 64, 48));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { SelectedCount = 9 }, options, 64, 48));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { Metrics = valid.Metrics! with { KeystoneHorizontalPercent = double.NaN } }, options, 64, 48));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionNode.ValidateDetection(valid with { Quality = valid.Quality with { MinimumContrast = 0.001 } }, options, 64, 48));
    }

    [Fact]
    public void LegacyVersionTwoJsonPreservesPercentAxesAndFullGridWithoutFakeOptic()
    {
        GridDistortionResult result = CreateDetection(7, 7);
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        string json = LocalGridDistortionResultPersistence.BuildLegacyResultJson(result, analysis, GridTvFormula.Standard, GridPoint9Formula.OppositeEdgeMean, false);
        DistortionReslut legacy = JsonConvert.DeserializeObject<DistortionReslut>(json)!;
        Assert.Null(legacy.OpticDistortion);
        Assert.Equal(analysis.StandardTv.HorizontalPercent, legacy.TVDistortion.HorizontalRatio);
        Assert.Equal(analysis.StandardTv.VerticalPercent, legacy.TVDistortion.VerticalRatio);
        Assert.Equal(analysis.ReferencePoint9.KeystoneHorizontalPercent, legacy.Point9Distortion.KeyStoneHoriRatio);
        Assert.Equal(analysis.ReferencePoint9.KeystoneVerticalPercent, legacy.Point9Distortion.KeyStoneVercRatio);
        Assert.Equal(49, legacy.TVDistortion.FinalPoints.Count);
        Assert.Equal(new[] { 0, 3, 6, 21, 24, 27, 42, 45, 48 }, legacy.Point9Distortion.FinalPoints.Select(point => point.Id));
        JObject root = JObject.Parse(json);
        Assert.Equal("percent", root.Value<string>("Units"));
        Assert.NotNull(root["GridDistortion"]?["Quality"]);
        Assert.Equal(result.RawJson, root["GridDistortion"]!.Value<string>("RawJson"));
        Assert.Null(root["DIFF_H"]);
        Assert.NotNull(root["Analysis"]?["Optical"]);
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionResultPersistence.BuildLegacyResultJson(result with { Success = false }, analysis, GridTvFormula.Standard, GridPoint9Formula.OppositeEdgeMean, false));
    }

    [Fact]
    public void OutputConventionSelectsExistingAnalysisAndOpticalEstimateRequiresExplicitPublication()
    {
        GridDistortionResult result = CreateDetection(7, 7);
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        string json = LocalGridDistortionResultPersistence.BuildLegacyResultJson(result, analysis, GridTvFormula.Half, GridPoint9Formula.LegacyThreeSpanMean, true);
        JObject root = JObject.Parse(json);
        DistortionReslut legacy = JsonConvert.DeserializeObject<DistortionReslut>(json)!;
        Assert.Equal(analysis.HalfTv.HorizontalPercent, legacy.TVDistortion.HorizontalRatio);
        Assert.Equal(analysis.LegacyPoint9.KeystoneHorizontalPercent, legacy.Point9Distortion.KeyStoneHoriRatio);
        Assert.Equal(analysis.LegacyPoint9.KeystoneVerticalPercent, legacy.Point9Distortion.KeyStoneVercRatio);
        Assert.NotNull(legacy.OpticDistortion);
        Assert.Equal(analysis.Optical.OpticRatioPercent, legacy.OpticDistortion.OpticRatio);
        Assert.Equal(49, legacy.OpticDistortion.FinalPoints.Count);
        Assert.Contains("未标定", legacy.OpticDistortion.Message);
        Assert.Null(root["Optic_Distortion"]?["t"]);
        Assert.Equal(string.Empty, ViewHandleDistortion2.GetOpticTText(new Distortion2View { Result = json, DistortionReslut = legacy }));
        Assert.False(root["Optic_Distortion"]!.Value<bool>("isCalibrated"));
        Assert.Equal("Half", root["OutputSelection"]!.Value<string>("TvFormula"));
        Assert.NotNull(root["Analysis"]?["ReferencePoint9"]);
        GridDistortionAnalysis unavailable = analysis with { Optical = analysis.Optical with { IsAvailable = false, OpticRatioPercent = null } };
        JObject absent = JObject.Parse(LocalGridDistortionResultPersistence.BuildLegacyResultJson(result, unavailable, GridTvFormula.Standard, GridPoint9Formula.OppositeEdgeMean, true));
        Assert.Equal(JTokenType.Null, absent["Optic_Distortion"]!.Type);
    }

    [Fact]
    public void PersistenceUsesVersionTwoTypeNineAndSingleResultFileDetail()
    {
        LocalGridDistortionPersistenceRequest request = new()
        {
            Action = new CVStartCFC("contract"), DeviceCode = "ALG-1", Parameters = new { Algorithm = "GridDistortionV2" },
            ImageFilePath = @"C:\dots.cvraw", ZIndex = 7, TotalTime = 18, Result = CreateDetection()
        };
        AlgResultMasterModel master = LocalGridDistortionResultPersistence.CreateMasterModel(request, 17);
        Assert.Equal(ViewResultAlgType.Distortion, master.ImgFileType);
        Assert.Equal("2.0", master.version);
        Assert.Equal("LocalGridDistortion", master.TName);
        Assert.Equal(17, master.BatchId);
        Assert.Equal(0, master.ResultCode);
        FakeTransaction transaction = new();
        Assert.Equal(701, LocalGridDistortionResultPersistence.SaveDatabaseCore(master, @"C:\grid.json", () => transaction));
        Assert.Equal(["Begin", "Master", "Details", "Commit", "Dispose"], transaction.Events);
        DetailCommonModel detail = Assert.Single(transaction.Details);
        Assert.Equal(701, detail.PId);
        Assert.Equal(@"C:\grid.json", JObject.Parse(detail.ResultJson).Value<string>("ResultFileName"));
        AlgResultMasterModel failed = LocalGridDistortionResultPersistence.CreateMasterModel(request with { Result = null, ResultCode = -1, ResultDescription = "IncompleteGrid" }, 17);
        Assert.Equal(702, LocalGridDistortionResultPersistence.SaveMasterOnlyCore(failed, model => { Assert.Equal(-1, model.ResultCode); return 702; }));
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionResultPersistence.SaveMasterOnlyCore(master, _ => 703));
    }

    [Fact]
    public void DetailInsertFailureRollsBackAndPreservesRollbackFailure()
    {
        FakeTransaction transaction = new() { InsertedCount = 0 };
        Assert.Throws<InvalidOperationException>(() => LocalGridDistortionResultPersistence.SaveDatabaseCore(new(), @"C:\grid.json", () => transaction));
        Assert.Equal(["Begin", "Master", "Details", "Rollback", "Dispose"], transaction.Events);
        FakeTransaction failedRollback = new() { InsertedCount = 0, FailRollback = true };
        AggregateException error = Assert.Throws<AggregateException>(() => LocalGridDistortionResultPersistence.SaveDatabaseCore(new(), @"C:\grid.json", () => failedRollback));
        Assert.Equal(2, error.InnerExceptions.Count);
    }

    [Fact]
    public void VersionTwoRoutesUniquelyAndFailedMasterHasInspectableText()
    {
        ViewResultAlg result = new()
        {
            ResultType = ViewResultAlgType.Distortion, Version = "2.0", ResultCode = -1, ResultDesc = "IncompleteGrid",
            ViewResults = new ObservableCollection<IViewResult>(), AlgResultMasterModel = new() { Params = "{\"MissingCount\":1}" }
        };
        Assert.False(new ViewHandleDistortion().CanHandle1(result));
        Assert.True(new ViewHandleDistortion2().CanHandle1(result));
        Assert.Contains("IncompleteGrid", ViewHandleDistortion2.BuildResultText(result));
        Assert.Contains("MissingCount", ViewHandleDistortion2.BuildResultText(result));
        result.Version = "1.0";
        Assert.True(new ViewHandleDistortion().CanHandle1(result));
        Assert.False(new ViewHandleDistortion2().CanHandle1(result));
    }

    private static GridDistortionResult CreateDetection(int rows = 3, int cols = 3)
    {
        GridDistortionPoint[] points = Enumerable.Range(0, rows * cols).Select(id =>
        {
            double u = 2.0 * (id % cols) / (cols - 1) - 1, v = 2.0 * (id / cols) / (rows - 1) - 1;
            double radial = u * u + v * v;
            return new GridDistortionPoint
            {
                Id = id, Row = id / cols, Col = id % cols,
                X = 29 + u * 22 * (1 + 0.07 * v + 0.04 * radial),
                Y = 23 + v * 16 * (1 + 0.05 * u + 0.03 * radial),
                Area = 12, Contrast = 0.8, Name = $"P{id}"
            };
        }).ToArray();
        int[] references = new[] { 0, rows / 2, rows - 1 }.SelectMany(row => new[] { 0, cols / 2, cols - 1 }.Select(col => row * cols + col)).ToArray();
        GridDistortionResult result = new()
        {
            Success = true, StatusCode = "ok", Message = "ok", ExpectedRows = rows, ExpectedCols = cols,
            CandidateCount = rows * cols, SelectedCount = rows * cols, Points = points, ReferencePointIds = references,
            Quality = new() { Score = 0.9, MinimumContrast = 0.8, GridResidualFraction = 0.01, ProcessingScale = 1 },
            RawJson = "{\"algorithm\":\"GridDistortion\",\"version\":\"2.0\"}"
        };
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        double Span(int a, int b)
        {
            GridDistortionPoint p = points[references[a]], q = points[references[b]];
            return Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2));
        }
        return result with
        {
            Metrics = new()
            {
                HorizontalTvPercent = analysis.StandardTv.HorizontalPercent, VerticalTvPercent = analysis.StandardTv.VerticalPercent,
                TopPercent = analysis.ReferencePoint9.TopPercent, BottomPercent = analysis.ReferencePoint9.BottomPercent,
                LeftPercent = analysis.ReferencePoint9.LeftPercent, RightPercent = analysis.ReferencePoint9.RightPercent,
                KeystoneHorizontalPercent = analysis.ReferencePoint9.KeystoneHorizontalPercent,
                KeystoneVerticalPercent = analysis.ReferencePoint9.KeystoneVerticalPercent,
                TopWidth = Span(0, 2), MiddleWidth = Span(3, 5), BottomWidth = Span(6, 8),
                LeftHeight = Span(0, 6), CenterHeight = Span(1, 7), RightHeight = Span(2, 8)
            }
        };
    }

    private static LocalFlowFrame CreateFrame(bool cie = false, CVImageFlipMode flip = CVImageFlipMode.None) => LocalFlowFrame.Allocate(
        new LocalFrameMetadata { Width = 64, Height = 48, SourceBpp = 8, Channels = 1, PrimaryBufferKind = cie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw, FlipMode = flip },
        rawLength: cie ? 0 : 64 * 48, cieLength: cie ? 64 * 48 * sizeof(float) * 3 : 0);

    private static CVStartCFC CreateAction(bool cie = false, CVImageFlipMode flip = CVImageFlipMode.None)
    {
        CVStartCFC action = new($"grid-{Guid.NewGuid():N}");
        action.SetCurrentFrame(CreateFrame(cie, flip));
        return action;
    }

    private sealed class FakeServices : ILocalGridDistortionNodeServices
    {
        public Func<string, LocalFlowFrame> LoadFrameHandler { get; init; } = _ => throw new InvalidOperationException("Unexpected file load");
        public Func<int, MeasureResultImgModel?> GetImageResultHandler { get; init; } = _ => throw new InvalidOperationException("Unexpected image result lookup");
        public Func<HImage, RoiRect, GridDistortionOptions, GridDistortionResult> DetectHandler { get; init; } = (_, _, _) => CreateDetection();
        public Func<LocalGridDistortionPersistenceRequest, LocalGridDistortionPersistenceResult> PersistHandler { get; init; } = _ => new() { MasterId = 301, ResultFilePath = @"C:\grid.json" };
        public Action<LocalGridDistortionPublishRequest> PublishHandler { get; init; } = _ => { };
        public int DetectCount { get; private set; }
        public int PersistCount { get; private set; }
        public int PublishCount { get; private set; }
        public LocalFlowFrame LoadFrame(string path) => LoadFrameHandler(path);
        public MeasureResultImgModel? GetImageResult(int masterId) => GetImageResultHandler(masterId);
        public GridDistortionResult Detect(HImage image, RoiRect roi, GridDistortionOptions options) { DetectCount++; return DetectHandler(image, roi, options); }
        public LocalGridDistortionPersistenceResult Persist(LocalGridDistortionPersistenceRequest request) { PersistCount++; return PersistHandler(request); }
        public void Publish(LocalGridDistortionPublishRequest request) { PublishCount++; PublishHandler(request); }
    }

    private sealed class FakeTransaction : ILocalFlowResultTransaction<DetailCommonModel>
    {
        public List<string> Events { get; } = [];
        public IReadOnlyCollection<DetailCommonModel> Details { get; private set; } = [];
        public int InsertedCount { get; init; } = 1;
        public bool FailRollback { get; init; }
        public void Begin() => Events.Add("Begin");
        public int InsertMaster(AlgResultMasterModel model) { Events.Add("Master"); return 701; }
        public int InsertDetails(IReadOnlyCollection<DetailCommonModel> details) { Events.Add("Details"); Details = details; return InsertedCount; }
        public void Commit() => Events.Add("Commit");
        public void Rollback() { Events.Add("Rollback"); if (FailRollback) throw new InvalidOperationException("rollback failed"); }
        public void Dispose() => Events.Add("Dispose");
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
