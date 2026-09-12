using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed record LocalGridDistortionNodeResultData
{
    public int MasterId { get; init; }
    public int MasterResultType { get; init; } = (int)ViewResultAlgType.Distortion;
    public int SourceMasterId { get; init; }
    public string FrameId { get; init; } = string.Empty;
    public string? ImageFilePath { get; init; }
    public string ResultFilePath { get; init; } = string.Empty;
    public required GridDistortionResult Result { get; init; }
    public required GridDistortionAnalysis Analysis { get; init; }
    public int TotalTime { get; init; }
}

internal sealed record LocalGridDistortionPersistenceRequest
{
    public required CVStartCFC Action { get; init; }
    public required string DeviceCode { get; init; }
    public string? ImageFilePath { get; init; }
    public int ZIndex { get; init; }
    public int TotalTime { get; init; }
    public int ResultCode { get; init; }
    public string ResultDescription { get; init; } = "ok";
    public required object Parameters { get; init; }
    public GridDistortionResult? Result { get; init; }
    public GridDistortionAnalysis? Analysis { get; init; }
    public GridTvFormula TvFormula { get; init; }
    public GridPoint9Formula Point9Formula { get; init; }
    public bool PublishOpticalEstimate { get; init; }
    public string ResultDirectory { get; init; } = string.Empty;
}

internal sealed record LocalGridDistortionPersistenceResult
{
    public int MasterId { get; init; }
    public string ResultFilePath { get; init; } = string.Empty;
}

internal sealed record LocalGridDistortionPublishRequest
{
    public required string DeviceCode { get; init; }
    public required string SerialNumber { get; init; }
    public required string NodeId { get; init; }
    public int ZIndex { get; init; }
    public int MasterId { get; init; }
}

internal interface ILocalGridDistortionNodeServices
{
    LocalFlowFrame LoadFrame(string filePath);
    MeasureResultImgModel? GetImageResult(int masterId);
    GridDistortionResult Detect(HImage image, RoiRect roi, GridDistortionOptions options);
    LocalGridDistortionPersistenceResult Persist(LocalGridDistortionPersistenceRequest request);
    void Publish(LocalGridDistortionPublishRequest request);
}

internal sealed class LocalGridDistortionNodeServices : ILocalGridDistortionNodeServices
{
    public static LocalGridDistortionNodeServices Instance { get; } = new();
    public LocalFlowFrame LoadFrame(string filePath) => LocalFrameFileService.Load(filePath);
    public MeasureResultImgModel? GetImageResult(int masterId) => MeasureImgResultDao.Instance.GetById(masterId);
    public GridDistortionResult Detect(HImage image, RoiRect roi, GridDistortionOptions options) => GridDistortionNative.Run(image, roi, options);
    public LocalGridDistortionPersistenceResult Persist(LocalGridDistortionPersistenceRequest request) => LocalGridDistortionResultPersistence.Save(request);
    public void Publish(LocalGridDistortionPublishRequest request) => ResultMessageBus.Default.PublishPersisted(
        ResultRoutes.Algorithm, ResultKinds.Algorithm, request.DeviceCode, "Distortion", request.SerialNumber,
        request.NodeId, request.ZIndex, request.MasterId, (int)ViewResultAlgType.Distortion);
}

internal static class LocalGridDistortionResultPersistence
{
    internal const string ResultVersion = "2.0";
    internal const string MetricDefinition = "GridDistortionV2.Percent";

    public static LocalGridDistortionPersistenceResult Save(LocalGridDistortionPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(request.Action.SerialNumber)
            ?? throw new InvalidOperationException($"找不到流程批次：{request.Action.SerialNumber}");
        AlgResultMasterModel master = CreateMasterModel(request, batch.Id);
        if (request.ResultCode != 0)
            return new() { MasterId = SaveMasterOnlyCore(master, AlgResultMasterDao.Instance.SaveAndReturnId) };

        GridDistortionResult result = request.Result ?? throw new InvalidOperationException("成功的本地点阵畸变结果缺少明细。");
        GridDistortionAnalysis analysis = request.Analysis ?? throw new InvalidOperationException("成功的本地点阵畸变结果缺少指标分析。");
        string resultFilePath = WriteResultFile(request.ResultDirectory, BuildLegacyResultJson(result, analysis, request.TvFormula, request.Point9Formula, request.PublishOpticalEstimate));
        try
        {
            int masterId = SaveDatabaseCore(master, resultFilePath, static () => new SqlSugarLocalFlowResultTransaction<DetailCommonModel>());
            return new() { MasterId = masterId, ResultFilePath = resultFilePath };
        }
        catch (Exception saveException)
        {
            try { File.Delete(resultFilePath); }
            catch (Exception cleanupException)
            {
                throw new AggregateException("保存点阵畸变结果失败，且清理未引用的结果文件失败。", saveException, cleanupException);
            }
            throw;
        }
    }

    internal static string BuildLegacyResultJson(GridDistortionResult result, GridDistortionAnalysis analysis,
        GridTvFormula tvFormula, GridPoint9Formula point9Formula, bool publishOpticalEstimate)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(analysis);
        if (!result.Success || result.Metrics == null)
            throw new InvalidOperationException("拒绝结果不能写入成功的畸变 2.0 明细。");
        if (!Enum.IsDefined(tvFormula) || !Enum.IsDefined(point9Formula)) throw new InvalidOperationException("畸变输出口径无效。");
        var tv = tvFormula == GridTvFormula.Standard ? analysis.StandardTv : analysis.HalfTv;
        var point9 = point9Formula == GridPoint9Formula.OppositeEdgeMean ? analysis.ReferencePoint9 : analysis.LegacyPoint9;
        var pointsById = result.Points.ToDictionary(point => point.Id);
        var points = result.Points.Select(point => new { id = point.Id, x = point.X, y = point.Y }).ToArray();
        var referencePoints = result.ReferencePointIds.Select(id =>
        {
            var point = pointsById[id];
            return new { id = point.Id, x = point.X, y = point.Y };
        }).ToArray();
        object? opticalResult = null;
        var optical = analysis.Optical;
        if (publishOpticalEstimate && optical.IsAvailable && optical.OpticRatioPercent is double opticRatio
            && double.IsFinite(opticRatio) && optical.MaxErrorPointId is int worstId && pointsById.TryGetValue(worstId, out var worstPoint))
        {
            opticalResult = new
            {
                opticRatio,
                finalPoints = points,
                maxErrPoint = new { id = worstPoint.Id, x = worstPoint.X, y = worstPoint.Y },
                message = $"{optical.Method}；中央节距相对畸变估计，未标定，不保证与供应商光学畸变等价；t 未定义，未输出。",
                isCalibrated = false,
                method = optical.Method
            };
        }
        return JsonConvert.SerializeObject(new
        {
            Algorithm = "GridDistortionV2",
            MetricDefinition,
            analysis.FormulaVersion,
            Units = "percent",
            OutputSelection = new { TvFormula = tvFormula.ToString(), Point9Formula = point9Formula.ToString(), PublishOpticalEstimate = publishOpticalEstimate },
            PointOrder = "row-major, top-to-bottom, left-to-right",
            ReferencePointOrder = "TL,TC,TR,ML,C,MR,BL,BC,BR",
            KeystoneAxes = point9Formula == GridPoint9Formula.OppositeEdgeMean
                ? "H=(leftHeight-rightHeight)/average(leftHeight,rightHeight); V=(topWidth-bottomWidth)/average(topWidth,bottomWidth)"
                : "Legacy H=(topWidth-bottomWidth)/average(topWidth,middleWidth,bottomWidth); Legacy V=(leftHeight-rightHeight)/average(leftHeight,centerHeight,rightHeight)",
            Optic_Distortion = opticalResult,
            Point9_distortion = new
            {
                topRatio = point9.TopPercent,
                bottomRatio = point9.BottomPercent,
                leftRatio = point9.LeftPercent,
                rightRatio = point9.RightPercent,
                keyStoneHoriRatio = point9.KeystoneHorizontalPercent,
                keyStoneVercRatio = point9.KeystoneVerticalPercent,
                finalPoints = referencePoints,
                message = result.Message
            },
            TV_distortion = new
            {
                horizontalRatio = tv.HorizontalPercent,
                verticalRatio = tv.VerticalPercent,
                finalPoints = points,
                message = result.Message
            },
            GridDistortion = result,
            Analysis = analysis
        }, Formatting.Indented);
    }

    internal static AlgResultMasterModel CreateMasterModel(LocalGridDistortionPersistenceRequest request, int batchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (batchId <= 0) throw new ArgumentOutOfRangeException(nameof(batchId));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResultDescription);
        return new AlgResultMasterModel
        {
            TId = null,
            TName = "LocalGridDistortion",
            ImgFile = string.IsNullOrWhiteSpace(request.ImageFilePath) ? null : request.ImageFilePath,
            ImgFileType = ViewResultAlgType.Distortion,
            version = ResultVersion,
            BatchId = batchId,
            Zindex = request.ZIndex,
            Params = JsonConvert.SerializeObject(request.Parameters),
            DeviceCode = string.IsNullOrWhiteSpace(request.DeviceCode) ? null : request.DeviceCode,
            ResultCode = request.ResultCode,
            Result = request.ResultDescription,
            TotalTime = request.TotalTime,
            CreateDate = DateTime.Now
        };
    }

    internal static int SaveMasterOnlyCore(AlgResultMasterModel master, Func<AlgResultMasterModel, int> saveMaster)
    {
        if (master.ResultCode.GetValueOrDefault() == 0)
            throw new InvalidOperationException("成功的畸变结果不能省略结果明细。");
        int masterId = saveMaster(master);
        if (masterId <= 0) throw new InvalidOperationException("保存点阵畸变失败主记录失败。");
        return masterId;
    }

    internal static DetailCommonModel CreateDetail(int masterId, string resultFilePath)
    {
        if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(resultFilePath);
        return new DetailCommonModel { PId = masterId, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = resultFilePath }) };
    }

    internal static int SaveDatabaseCore(AlgResultMasterModel master, string resultFilePath,
        Func<ILocalFlowResultTransaction<DetailCommonModel>> transactionFactory) =>
        LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(master, ViewResultAlgType.Distortion,
            masterId => new[] { CreateDetail(masterId, resultFilePath) }, transactionFactory);

    private static string WriteResultFile(string configuredDirectory, string resultJson)
    {
        string directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "GridDistortion")
            : Path.GetFullPath(configuredDirectory.Trim());
        Directory.CreateDirectory(directory);
        string resultFilePath = Path.Combine(directory, $"GridDistortion_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
        string temporaryFilePath = resultFilePath + ".tmp";
        try
        {
            using (FileStream stream = new(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
                writer.Write(resultJson);
            File.Move(temporaryFilePath, resultFilePath);
            return resultFilePath;
        }
        catch
        {
            File.Delete(temporaryFilePath);
            throw;
        }
    }
}

[STNode("Flow_CustomNodes", "本地点阵畸变(V2)")]
public sealed class LocalGridDistortionNode : LocalFlowNodeBase
{
    internal const int DetectionFailureResultCode = -1;
    private readonly ILocalGridDistortionNodeServices services;
    private string imageFilePath = string.Empty;
    private string resultDirectory = string.Empty;
    private Int32Rect searchRegion = Int32Rect.Empty;
    private int expectedRows = 3;
    private int expectedCols = 3;
    private double minimumContrast = 0.02;
    private GridTvFormula tvFormula;
    private GridPoint9Formula point9Formula;
    private bool publishOpticalEstimate;

    [Category("本地点阵畸变")]
    [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
    [STNodeProperty("图像文件", "可选后备输入；优先使用上游内存帧，没有内存帧时先读该文件，再回退到 IN 图像结果。历史底图需要上游保存实际帧文件。", true)]
    public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("本地点阵畸变")]
    [STNodeProperty("图卡行数", "点阵行数，支持 3 到 15 的奇数；7×7 图卡填 7。缺失点不补点。", true)]
    public int ExpectedRows { get => expectedRows; set { expectedRows = value; OnPropertyChanged(); } }

    [Category("本地点阵畸变")]
    [STNodeProperty("图卡列数", "点阵列数，支持 3 到 15 的奇数；7×7 图卡填 7。", true)]
    public int ExpectedCols { get => expectedCols; set { expectedCols = value; OnPropertyChanged(); } }

    [Category("本地点阵畸变")]
    [STNodeProperty("搜索区域", "ROI（X,Y,Width,Height）；0,0,0,0 表示整图。点坐标始终使用整图坐标。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }

    [Category("本地点阵畸变")]
    [STNodeProperty("最小对比度", "点目标相对背景的最小归一化对比度，范围 0 到 1；不足时拒绝测量。", true)]
    public double MinimumContrast { get => minimumContrast; set { minimumContrast = value; OnPropertyChanged(); } }

    [Category("畸变输出")]
    [STNodeProperty("TV 输出口径", "标准 TV 或标准值减半；同一次定位同时计算两种值，此项选择写入 ARVR 的口径。", true)]
    public GridTvFormula TvFormula { get => tvFormula; set { tvFormula = value; OnPropertyChanged(); } }

    [Category("畸变输出")]
    [STNodeProperty("九点输出口径", "对边平均参考使用附件 H/V 定义；旧 P9 保留三跨度分母及旧 H/V 命名。全部方案都会保存在分析结果中。", true)]
    public GridPoint9Formula Point9Formula { get => point9Formula; set { point9Formula = value; OnPropertyChanged(); } }

    [Category("畸变输出")]
    [STNodeProperty("输出相对光学估计", "默认关闭。启用后将中央节距相对估计写入 ARVR 光学畸变项；此值未标定，不保证与原供应商光学畸变等价。", true)]
    public bool PublishOpticalEstimate { get => publishOpticalEstimate; set { publishOpticalEstimate = value; OnPropertyChanged(); } }

    [Category("本地点阵畸变")]
    [STNodeProperty("结果目录", "可选；留空保存到当前用户 LocalAppData 下 ColorVision\\Results\\GridDistortion。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    public LocalGridDistortionNode() : this(LocalGridDistortionNodeServices.Instance) { }

    internal LocalGridDistortionNode(ILocalGridDistortionNodeServices services)
        : base("本地点阵畸变(V2)", "LocalGridDistortion", "Distortion")
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        SelectFirstAvailableDevice<DeviceAlgorithm>();
    }

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };

    internal LocalGridDistortionNodeResultData ExecuteSynchronously(CVStartCFC action)
    {
        ArgumentNullException.ThrowIfNull(action);
        // Snapshot editable settings so one run has one auditable configuration.
        GridDistortionOptions options = new() { ExpectedRows = ExpectedRows, ExpectedCols = ExpectedCols, MinimumContrast = MinimumContrast };
        if (!options.TryValidate(out string optionsError)) throw new InvalidOperationException(optionsError);
        Int32Rect configuredRegion = SearchRegion;
        string configuredImageFile = ImageFilePath;
        string configuredDirectory = ResultDirectory;
        GridTvFormula selectedTvFormula = TvFormula;
        GridPoint9Formula selectedPoint9Formula = Point9Formula;
        bool selectedPublishOptical = PublishOpticalEstimate;
        if (!Enum.IsDefined(selectedTvFormula) || !Enum.IsDefined(selectedPoint9Formula)) throw new InvalidOperationException("畸变输出口径无效。");
        int zIndex = ZIndex;
        string nodeId = NodeID;
        string algorithmDeviceCode = ResolveAvailableDeviceCode<DeviceAlgorithm>();
        LocalFlowFrame? ownedFrame = null;
        LocalFlowFrame frame = ResolveFrame(action, configuredImageFile, out ownedFrame, out string? imageFile);
        bool loadedFromFile = ownedFrame != null;
        try
        {
            using LocalFlowFrameLease lease = frame.Acquire();
            if (!lease.IsFlipApplied)
                throw new InvalidOperationException("当前图像的方向变换尚未完成，无法计算点阵畸变。");
            RoiRect roi = LocalFindLuminousAreaNode.ResolveRoi(configuredRegion, lease.Metadata.Width, lease.Metadata.Height);
            HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
            Stopwatch stopwatch = Stopwatch.StartNew();
            GridDistortionResult detection = services.Detect(image, roi, options);
            stopwatch.Stop();
            int totalTime = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            var parameters = Newtonsoft.Json.Linq.JObject.FromObject(new
            {
                Algorithm = "GridDistortionV2",
                LocalGridDistortionResultPersistence.MetricDefinition,
                Units = "percent",
                SourceMasterId = lease.MasterId,
                FrameId = lease.FrameId.ToString("N"),
                ImageFilePath = imageFile,
                SearchRegion = new { roi.X, roi.Y, roi.Width, roi.Height },
                Options = options,
                OutputSelection = new { TvFormula = selectedTvFormula.ToString(), Point9Formula = selectedPoint9Formula.ToString(), PublishOpticalEstimate = selectedPublishOptical },
                Detection = detection,
                PrimaryBufferKind = lease.Metadata.PrimaryBufferKind.ToString(),
                SourceFilePath = lease.Metadata.SourceFilePath,
                frame.CvRawFilePath,
                frame.CvCieFilePath,
                lease.Metadata.CalibrationTemplate,
                FlipMode = lease.Metadata.FlipMode.ToString(),
                FlipApplied = lease.IsFlipApplied,
                ImageRead = loadedFromFile,
                MemoryOnly = string.IsNullOrWhiteSpace(imageFile)
            });
            LocalGridDistortionPersistenceRequest request = new()
            {
                Action = action, DeviceCode = algorithmDeviceCode, ImageFilePath = imageFile, ZIndex = zIndex,
                TotalTime = totalTime, Parameters = parameters, ResultDirectory = configuredDirectory,
                TvFormula = selectedTvFormula, Point9Formula = selectedPoint9Formula, PublishOpticalEstimate = selectedPublishOptical
            };
            GridDistortionAnalysis analysis;
            try
            {
                ValidateDetection(detection, options, lease.Metadata.Width, lease.Metadata.Height);
                analysis = GridDistortionAnalysis.Calculate(detection);
            }
            catch (Exception validationException) when (validationException is InvalidOperationException or ArgumentException)
            {
                LocalGridDistortionPersistenceResult failed = services.Persist(request with
                {
                    ResultCode = DetectionFailureResultCode, ResultDescription = validationException.Message
                });
                if (failed.MasterId <= 0) throw new InvalidOperationException("点阵畸变失败持久化返回了无效主表 ID。");
                services.Publish(new() { DeviceCode = algorithmDeviceCode, SerialNumber = action.SerialNumber, NodeId = nodeId, ZIndex = zIndex, MasterId = failed.MasterId });
                throw new InvalidOperationException(validationException.Message, validationException);
            }

            parameters["Analysis"] = Newtonsoft.Json.Linq.JObject.FromObject(analysis);
            LocalGridDistortionPersistenceResult persisted = services.Persist(request with { Result = detection, Analysis = analysis });
            if (persisted.MasterId <= 0 || string.IsNullOrWhiteSpace(persisted.ResultFilePath))
                throw new InvalidOperationException("点阵畸变持久化未返回有效主表 ID 和结果文件。");
            if (ownedFrame != null)
            {
                action.SetCurrentFrame(ownedFrame);
                ownedFrame = null;
            }
            action.Data["LocalGridDistortionResult"] = detection;
            action.Data["LocalGridDistortionAnalysis"] = analysis;
            var selectedTv = selectedTvFormula == GridTvFormula.Standard ? analysis.StandardTv : analysis.HalfTv;
            var selectedPoint9 = selectedPoint9Formula == GridPoint9Formula.OppositeEdgeMean ? analysis.ReferencePoint9 : analysis.LegacyPoint9;
            action.Data["LocalGridDistortionMetrics"] = new { TV = selectedTv, Point9 = selectedPoint9 };
            action.Data["LocalGridDistortionQuality"] = detection.Quality;
            action.Data["LocalGridDistortionPoints"] = detection.Points;
            action.Data["LocalGridDistortionReferencePointIds"] = detection.ReferencePointIds;
            action.Data["LocalGridDistortionResultFile"] = persisted.ResultFilePath;
            action.Data["LocalGridDistortionHorizontalTvPercent"] = selectedTv.HorizontalPercent;
            action.Data["LocalGridDistortionVerticalTvPercent"] = selectedTv.VerticalPercent;
            action.Data["LocalGridDistortionKeystoneHorizontalPercent"] = selectedPoint9.KeystoneHorizontalPercent;
            action.Data["LocalGridDistortionKeystoneVerticalPercent"] = selectedPoint9.KeystoneVerticalPercent;
            action.MasterValue(null, persisted.MasterId, (int)ViewResultAlgType.Distortion);
            services.Publish(new() { DeviceCode = algorithmDeviceCode, SerialNumber = action.SerialNumber, NodeId = nodeId, ZIndex = zIndex, MasterId = persisted.MasterId });
            return new()
            {
                MasterId = persisted.MasterId, SourceMasterId = lease.MasterId, FrameId = lease.FrameId.ToString("N"),
                ImageFilePath = imageFile, ResultFilePath = persisted.ResultFilePath, Result = detection, Analysis = analysis, TotalTime = totalTime
            };
        }
        finally { ownedFrame?.Dispose(); }
    }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber,
        ImageFilePath, SearchRegion, ExpectedRows, ExpectedCols, MinimumContrast, ResultDirectory,
        TvFormula, Point9Formula, PublishOpticalEstimate, Algorithm = "GridDistortionV2"
    });

    internal static void ValidateDetection(GridDistortionResult result, GridDistortionOptions options, int width, int height)
    {
        if (result == null) throw new InvalidOperationException("点阵畸变未返回检测结果。");
        if (!result.Success)
            throw new InvalidOperationException($"点阵畸变失败：{result.StatusCode}；{result.Message}；{result.InteropDiagnostic}");
        int expectedCount = options.ExpectedRows * options.ExpectedCols;
        if (result.ExpectedRows != options.ExpectedRows || result.ExpectedCols != options.ExpectedCols
            || result.SelectedCount != expectedCount || result.Points.Count != expectedCount || result.CandidateCount < expectedCount)
            throw new InvalidOperationException("点阵畸变返回的图卡尺寸或有效点数与配置不一致。");
        for (int index = 0; index < result.Points.Count; index++)
        {
            GridDistortionPoint point = result.Points[index];
            if (point.Id != index || point.Row != index / options.ExpectedCols || point.Col != index % options.ExpectedCols
                || !double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)
                throw new InvalidOperationException("点阵畸变返回了无效的整图坐标或行列点序。");
        }
        int[] rows = [0, options.ExpectedRows / 2, options.ExpectedRows - 1];
        int[] cols = [0, options.ExpectedCols / 2, options.ExpectedCols - 1];
        int[] references = rows.SelectMany(row => cols.Select(col => row * options.ExpectedCols + col)).ToArray();
        if (!result.ReferencePointIds.SequenceEqual(references))
            throw new InvalidOperationException("点阵畸变九点必须来自四角、四边中点和中心，且保持明确点序。");
        if (result.Metrics == null) throw new InvalidOperationException("点阵畸变成功结果缺少指标。");
        var metrics = result.Metrics;
        double[] percentages = [metrics.HorizontalTvPercent, metrics.VerticalTvPercent, metrics.TopPercent, metrics.BottomPercent,
            metrics.LeftPercent, metrics.RightPercent, metrics.KeystoneHorizontalPercent, metrics.KeystoneVerticalPercent];
        double[] spans = [metrics.TopWidth, metrics.MiddleWidth, metrics.BottomWidth, metrics.LeftHeight, metrics.CenterHeight, metrics.RightHeight];
        if (percentages.Any(value => !double.IsFinite(value)) || spans.Any(value => !double.IsFinite(value) || value <= 0))
            throw new InvalidOperationException("点阵畸变返回了无效指标或退化跨度，不能作为零畸变保存。");
        if (result.Quality == null || !double.IsFinite(result.Quality.Score) || result.Quality.Score < 0 || result.Quality.Score > 1
            || !double.IsFinite(result.Quality.MinimumContrast) || result.Quality.MinimumContrast < options.MinimumContrast
            || !double.IsFinite(result.Quality.GridResidualFraction) || result.Quality.GridResidualFraction < 0
            || result.Quality.GridResidualFraction > options.MaximumGridResidualFraction)
            throw new InvalidOperationException("点阵畸变质量证据无效或未达到配置要求。");
    }

    private LocalFlowFrame ResolveFrame(CVStartCFC action, string configuredFile, out LocalFlowFrame? ownedFrame, out string? imageFile)
    {
        ownedFrame = null;
        imageFile = null;
        if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
        {
            string file = currentFrame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie ? currentFrame.CvCieFilePath : currentFrame.CvRawFilePath;
            imageFile = string.IsNullOrWhiteSpace(file) ? null : file;
            return currentFrame;
        }
        int sourceMasterId = -1;
        string? fallbackFile;
        if (!string.IsNullOrWhiteSpace(configuredFile)) fallbackFile = Path.GetFullPath(configuredFile.Trim());
        else
        {
            if (!TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _))
            {
                masterId = ReadActionInt(action, "MasterId");
                masterResultType = ReadActionInt(action, "MasterResultType");
            }
            if (masterId <= 0) throw new InvalidOperationException("流程中没有可用内存帧或图像结果；请连接本地取图/校正节点，或配置图像文件。");
            if (masterResultType is not (int)CVCommCore.CVResultType.Camera_Img and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
                throw new InvalidOperationException($"IN 不是图像结果：MasterId={masterId}，ResultType={masterResultType}。");
            MeasureResultImgModel input = services.GetImageResult(masterId) ?? throw new InvalidOperationException($"找不到 IN 图像结果：{masterId}。");
            sourceMasterId = masterId;
            string[] candidates = new[] { input.FileUrl, input.RawFile }.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetFullPath(value.Trim())).ToArray();
            fallbackFile = candidates.FirstOrDefault(File.Exists) ?? candidates.FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(fallbackFile) || !File.Exists(fallbackFile))
            throw new FileNotFoundException("点阵畸变图像文件不存在。", fallbackFile);
        ownedFrame = services.LoadFrame(fallbackFile);
        if (sourceMasterId > 0) ownedFrame.MasterId = sourceMasterId;
        imageFile = fallbackFile;
        return ownedFrame;
    }

    private static int ReadActionInt(CVStartCFC action, string key)
    {
        if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
        try { return Convert.ToInt32(value); }
        catch { return -1; }
    }
}
