using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public enum LocalLightBeadsPoiShape { Point, Circle, Rectangle }

[STNode("Flow_LocalAlgorithms", "灯珠定位", CategoryOrder = 9910, NodeOrder = 15)]
[FlowEngineLib.FlowNodeDocumentation("本地定位灯珠，输出本次流程内存中的 POI 点集；不创建模板或逐点写入数据库。",
    Notes = "可直接连接本地 POI，或把 OUT 接到本地实时 POI 的 IN_POI；同方向的校正图像接 IN_CIE。测量结果随运行释放，不进入历史结果表。")]
public sealed class LocalLightBeadsNode : LocalFlowNodeBase
{
    private int threshold = 20, minSize = 2, maxSize;
    private double poiWidth = 3, poiHeight = 3;
    private Int32Rect searchRegion;
    private LocalLightBeadsPoiShape pointShape = LocalLightBeadsPoiShape.Circle;
    private bool inspectDefects = true;
    private int expectedRows, expectedColumns;
    private double dimRatio = 0.5;
    private string resultDirectory = string.Empty;

    [Category("不良检测"), STNodeProperty("不良检测", "按规则网格检查不亮或明显偏暗的灯珠；关闭时仅定位亮点。", true)]
    public bool InspectDefects { get => inspectDefects; set { inspectDefects = value; OnPropertyChanged(); } }
    [Category("不良检测"), STNodeProperty("标准行数", "0 自动识别；正数校验识别结果，避免漏掉整行不亮的灯珠。", true)]
    public int ExpectedRows { get => expectedRows; set { expectedRows = value; OnPropertyChanged(); } }
    [Category("不良检测"), STNodeProperty("标准列数", "0 自动识别；正数校验识别结果，避免漏掉整列不亮的灯珠。", true)]
    public int ExpectedColumns { get => expectedColumns; set { expectedColumns = value; OnPropertyChanged(); } }
    [Category("不良检测"), STNodeProperty("偏暗比例", "相对同图亮点中位亮度的比例，默认 0.5；低于此比例判为偏暗。", true)]
    public double DimRatio { get => dimRatio; set { dimRatio = value; OnPropertyChanged(); } }
    [Category("结果"), STNodeProperty("本地结果目录", "可选绝对目录，导出全点和不良点 CSV（行列、坐标、状态、亮度）；留空仅保留本次运行内存。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("定位"), STNodeProperty("阈值", "转换为 Gray8 后的二值化阈值，0–255。", true)]
    public int Threshold { get => threshold; set { threshold = value; OnPropertyChanged(); } }
    [Category("定位"), STNodeProperty("最小尺寸", "亮点包围框的宽高须严格大于此像素数。", true)]
    public int MinSize { get => minSize; set { minSize = value; OnPropertyChanged(); } }
    [Category("定位"), STNodeProperty("最大尺寸", "亮点包围框的宽高须严格小于此像素数；0 表示不限。", true)]
    public int MaxSize { get => maxSize; set { maxSize = value; OnPropertyChanged(); } }
    [Category("定位"), STNodeProperty("搜索区域", "0,0,0,0 表示整图；非空区域须完整位于图像内。", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
    public Int32Rect SearchRegion { get => searchRegion; set { searchRegion = value; OnPropertyChanged(); } }
    [Category("输出 POI"), STNodeProperty("点类型", "后续本地 POI 测量使用的形状；不良检测模式保留完整网格，包括不亮和偏暗点。", true)]
    public LocalLightBeadsPoiShape PointShape { get => pointShape; set { pointShape = value; OnPropertyChanged(); } }
    [Category("输出 POI"), STNodeProperty("POI 宽度", "圆形的直径或矩形宽度，单位像素。", true)]
    public double PoiWidth { get => poiWidth; set { poiWidth = value; OnPropertyChanged(); } }
    [Category("输出 POI"), STNodeProperty("POI 高度", "圆形的直径或矩形高度，单位像素。", true)]
    public double PoiHeight { get => poiHeight; set { poiHeight = value; OnPropertyChanged(); } }

    private sealed record Parameters(int Threshold, int MinSize, int MaxSize, Int32Rect Region, PoiShape Shape, double Width, double Height,
        bool Inspect, int Rows, int Columns, double DimRatio, string ResultDirectory);

    public LocalLightBeadsNode() : base("灯珠定位", "LocalLightBeads", "FindLightBeads") { }
    protected override string GetCompactSummaryValue() => FormatCompactRegion(SearchRegion);
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber, Parameters = GetExecutionParameters<Parameters>(action), DatabasePersisted = false
    });
    protected override object CaptureExecutionParameters(CVStartCFC action)
    {
        if (Threshold is < 0 or > 255 || MinSize < 0 || MaxSize < 0 || MaxSize > 0 && MaxSize <= MinSize)
            throw new InvalidOperationException("灯珠阈值须为 0–255，尺寸须非负；最大尺寸为 0 或大于最小尺寸。");
        if (!double.IsFinite(PoiWidth) || !double.IsFinite(PoiHeight) || PoiWidth < 1 || PoiHeight < 1
            || PoiWidth > int.MaxValue || PoiHeight > int.MaxValue)
            throw new InvalidOperationException("POI 宽高须为至少 1 像素的有限数值。");
        if (ExpectedRows < 0 || ExpectedColumns < 0 || !double.IsFinite(DimRatio) || DimRatio <= 0 || DimRatio >= 1)
            throw new InvalidOperationException("标准行列数须非负；偏暗比例须大于 0 且小于 1。");
        if (!string.IsNullOrWhiteSpace(ResultDirectory) && !Path.IsPathFullyQualified(ResultDirectory))
            throw new InvalidOperationException("本地结果目录必须是绝对路径。");
        PoiShape shape = PointShape switch
        {
            LocalLightBeadsPoiShape.Point => PoiShape.Point,
            LocalLightBeadsPoiShape.Circle => PoiShape.Circle,
            LocalLightBeadsPoiShape.Rectangle => PoiShape.Rect,
            _ => throw new InvalidOperationException("不支持的灯珠 POI 形状。")
        };
        return new Parameters(Threshold, MinSize, MaxSize, SearchRegion, shape, PoiWidth, PoiHeight,
            InspectDefects, ExpectedRows, ExpectedColumns, DimRatio, ResultDirectory);
    }

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        Parameters parameters = GetExecutionParameters<Parameters>(action);
        bool ownsFrame = !action.TryGetCurrentFrame(out LocalFlowFrame? frame) || frame == null;
        if (ownsFrame)
        {
            string path = ResolveInputImageFilePath(action, 0);
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请连接相机、校正或加载图片节点。 ");
            frame = LocalFrameFileService.Load(path);
        }
        try
        {
            using LocalFlowFrameLease lease = frame!.Acquire();
            if (!lease.IsFlipApplied) throw new InvalidOperationException("图像方向变换尚未完成，无法生成灯珠点集。");
            var watch = Stopwatch.StartNew();
            Int32Rect region = parameters.Region;
            HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
            LightBeadsDetectionResult detection = LightBeadsDetector.Detect(image,
                new RoiRect(region.X, region.Y, region.Width, region.Height), parameters.Threshold, parameters.MinSize, parameters.MaxSize,
                cancellationToken: action.RuntimeResources.StopToken);
            LightBeadsInspectionResult? inspection = parameters.Inspect ? LightBeadsInspector.Inspect(image, detection.Centers,
                parameters.Rows, parameters.Columns, parameters.DimRatio, parameters.Threshold, action.RuntimeResources.StopToken) : null;
            var centers = inspection == null ? detection.Centers : inspection.Points.Where(point => point.Status != LightBeadStatus.Unknown)
                .Select(point => new LightBeadCenter(checked((int)Math.Round(point.X)), checked((int)Math.Round(point.Y)))).ToArray();
            var points = new LocalFlowPoiPointSet(centers, frame.Metadata, parameters.Shape, parameters.Width, parameters.Height, inspection);
            action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
            (string? allFile, string? badFile) = WriteInspection(action, inspection, parameters.ResultDirectory);
            string handle = points.Publish(action);
            if (ownsFrame) { action.SetCurrentFrame(frame); ownsFrame = false; }
            watch.Stop();
            return new LocalNodeExecutionResult { Data = new
            {
                PointSet = handle, PointCount = points.Count, DarkCandidateCount = detection.DarkCandidates.Count,
                TotalCount = inspection?.TotalCount ?? points.Count, BadCount = inspection?.BadCount, NormalCount = inspection?.NormalCount,
                DetectedCenterCount = detection.Centers.Count, UnmatchedCenterCount = inspection?.UnmatchedCenters.Count,
                ReferenceBrightness = inspection?.ReferenceBrightness, DimThreshold = inspection?.DimThreshold,
                Rows = inspection?.Grid.Rows, Columns = inspection?.Grid.Columns, InferredSize = inspection?.InferredSize,
                TotalTime = watch.ElapsedMilliseconds, MemoryOnly = allFile == null, DatabasePersisted = false,
                AllPointsCsv = allFile, BadPointsCsv = badFile, ImageWidth = points.ImageWidth, ImageHeight = points.ImageHeight
            } };
        }
        finally { if (ownsFrame) frame?.Dispose(); }
    }

    private static (string?, string?) WriteInspection(CVStartCFC action, LightBeadsInspectionResult? inspection, string directory)
    {
        if (inspection == null || string.IsNullOrWhiteSpace(directory)) return (null, null);
        Directory.CreateDirectory(directory);
        string stem = Path.Combine(directory, $"LED_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
        string all = stem + "_all.csv", bad = stem + "_defects.csv";
        try
        {
            using (var writer = new StreamWriter(all, false, new UTF8Encoding(true))) inspection.WriteCsv(writer, false, action.RuntimeResources.StopToken);
            using (var writer = new StreamWriter(bad, false, new UTF8Encoding(true))) inspection.WriteCsv(writer, true, action.RuntimeResources.StopToken);
            return (all, bad);
        }
        catch { File.Delete(all); File.Delete(bad); throw; }
    }
}
