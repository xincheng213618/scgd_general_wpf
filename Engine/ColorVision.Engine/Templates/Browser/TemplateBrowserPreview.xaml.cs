using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Browser;

/// <summary>Read-only preview of the selected saved template; never opens an editor or hydrates shared POI parameters.</summary>
public partial class TemplateBrowserPreview : UserControl
{
    private static readonly ILog log = LogManager.GetLogger(typeof(TemplateBrowserPreview));
    private CancellationTokenSource? pending;
    internal Func<FlowIdentity, string?, FlowTemplateUsage?> ReadUsage { get; set; } = (identity, hash) =>
        FlowTemplateUsageReader.Read(ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath, identity, hash);

    public TemplateBrowserPreview()
    {
        InitializeComponent();
        Grid.SetIsSharedSizeScope(PreviewDetails, true);
        Unloaded += (_, _) => Cancel();
    }

    internal void SetTitle(string? title) => PreviewTitle.Text = title ?? string.Empty;

    internal void Cancel()
    {
        pending?.Cancel();
        pending = null;
        PreviewImage.Source = null;
        ImageBorder.Visibility = Visibility.Collapsed;
        PreviewDetails.ItemsSource = null;
        SetTitle(null);
        PreviewStatus.Text = EngineLocalization.Get("TemplateBrowserSelectPreview");
    }

    internal async Task ShowAsync(TemplateBase? item, FlowTemplateCoverService covers)
    {
        Cancel();
        if (item == null) return;
        using var request = new CancellationTokenSource();
        pending = request;
        SetTitle(item.Key);
        PreviewStatus.Text = EngineLocalization.Get("正在加载预览…");
        var details = new List<KeyValuePair<string, string>>();
        void Add(string label, object? value) => details.Add(new(EngineLocalization.Get(label), value?.ToString() ?? "—"));
        Add("Id", item.Id);
        Add("TemplateBrowserStorage", item.Id < -1 ? EngineLocalization.Get("TemplateBrowserLocal") : "MySQL");
        PreviewDetails.ItemsSource = details.ToArray();
        try
        {
            ImageSource? image = null;
            string status = string.Empty;
            switch (item)
            {
                case TemplateModel<FlowParam> { Value: var flow }:
                {
                    string data = flow.DataBase64;
                    int? nodeCount = null, edgeCount = null;
                    try
                    {
                        var style = FlowTemplateCoverStyle.Current;
                        var canvasColor = style.Theme.Canvas;
                        ImageBorder.Background = new SolidColorBrush(Color.FromArgb(canvasColor.A, canvasColor.R, canvasColor.G, canvasColor.B));
                        image = await covers.LoadAsync(data, request.Token, style);
                        if (!string.IsNullOrEmpty(data))
                        {
                            var summary = await Task.Run(() => new FlowCanvasCatalogBuilder().Build(Convert.FromBase64String(data)), request.Token);
                            nodeCount = summary.SemanticDocument.Nodes.Count;
                            edgeCount = summary.SemanticDocument.Edges.Count;
                        }
                        else status = EngineLocalization.Get("空流程");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        log.Warn("Reading flow preview structure failed; usage information remains available.", ex);
                        status = EngineLocalization.Get("TemplateBrowserPreviewFailed");
                    }
                    var identity = new FlowIdentity(flow.Id, flow.FlowKey, item.Key);
                    string? hash = flow.LoadedContentHash ?? flow.TemplateContentHash;
                    FlowTemplateUsage? usage = null;
                    try { usage = await Task.Run(() => ReadUsage(identity, hash), request.Token); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { log.Warn("Reading flow preview usage failed; structural preview remains available.", ex); }
                    Add("TemplateBrowserExecutionCount", usage?.ExecutionCount.ToString("N0") ?? EngineLocalization.Get("TemplateBrowserNoData"));
                    Add("TemplateBrowserExpectedTime", usage?.ExpectedMs is double milliseconds
                        ? $"{milliseconds / 1000:N2} s" : EngineLocalization.Get("TemplateBrowserNoData"));
                    if (nodeCount.HasValue) Add("TemplateBrowserNodeCount", nodeCount);
                    if (edgeCount.HasValue) Add("TemplateBrowserEdgeCount", edgeCount);
                    if (flow.TemplateRevision.HasValue) Add("TemplateBrowserRevision", flow.TemplateRevision);
                    string basis = usage?.SampleCount > 0
                        ? string.Format(EngineLocalization.Get("TemplateBrowserEstimateBasis"), usage.SampleCount)
                        : EngineLocalization.Get("TemplateBrowserUsageScope");
                    status = string.IsNullOrEmpty(status) ? basis : status + Environment.NewLine + basis;
                    break;
                }
                case TemplateModel<PoiParam> { Value: var poi }:
                {
                    int id = poi.Id, width = poi.Width, height = poi.Height;
                    bool dynamicPoints = poi.PoiConfig.IsPoiCIEFile;
                    Add("TemplateBrowserCanvasSize", $"{width} × {height}");
                    PreviewDetails.ItemsSource = details.ToArray();
                    // Copy already loaded points, or read only this record through its owning storage.
                    // Do not mark shared masters as loaded or replace their editable point collection.
                    var points = poi.DetailsLoaded ? poi.PoiPoints.ToList()
                        : await Task.Run(() => (poi.Storage ?? PoiTemplateStorage.Default).ReadPoints(id), request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    Add("TemplateBrowserPointCount", points.Count);
                    Add("TemplateBrowserCircleCount", points.Count(point => point.PointType == PoiShape.Circle));
                    Add("TemplateBrowserRectangleCount", points.Count(point => point.PointType is PoiShape.Rect or PoiShape.LeftTopRect));
                    image = DrawPoi(width, height, points);
                    status = EngineLocalization.Get(dynamicPoints ? "TemplateBrowserDynamicPoi" : "TemplateBrowserPoiLayout");
                    break;
                }
            }
            if (!ReferenceEquals(pending, request) || request.IsCancellationRequested) return;
            PreviewImage.Source = image;
            ImageBorder.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
            PreviewDetails.ItemsSource = details.ToArray();
            PreviewStatus.Text = status;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ReferenceEquals(pending, request)) return;
            log.Warn("Reading template preview failed; editing remains available.", ex);
            PreviewStatus.Text = EngineLocalization.Get("TemplateBrowserPreviewFailed");
        }
        finally
        {
            if (ReferenceEquals(pending, request)) pending = null;
        }
    }

    internal static DrawingImage DrawPoi(int width, int height, IReadOnlyList<PoiPoint> points)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(29, 32, 37)), null, new Rect(0, 0, 640, 360));
            var valid = points.Where(point => double.IsFinite(point.PixX) && double.IsFinite(point.PixY)
                && double.IsFinite(point.PixWidth) && double.IsFinite(point.PixHeight)).ToArray();
            var bounds = new Rect(0, 0, Math.Max(1, width), Math.Max(1, height));
            foreach (var point in valid) bounds.Union(PointBounds(point));
            double scale = Math.Min(600 / bounds.Width, 320 / bounds.Height);
            var transform = new Matrix(scale, 0, 0, scale, (640 - bounds.Width * scale) / 2 - bounds.X * scale, (360 - bounds.Height * scale) / 2 - bounds.Y * scale);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(97, 174, 255)), 1.5);
            var fill = new SolidColorBrush(Color.FromArgb(32, 97, 174, 255));
            Rect canvas = Rect.Transform(new Rect(0, 0, Math.Max(1, width), Math.Max(1, height)), transform);
            context.DrawRectangle(null, new Pen(Brushes.SlateGray, 1), canvas);
            foreach (var point in valid)
            {
                Rect rect = Rect.Transform(PointBounds(point), transform);
                // Keep tiny points visible while preserving saved coordinates and shape proportions.
                if (rect.Width < 3 || rect.Height < 3)
                    context.DrawEllipse(pen.Brush, null, transform.Transform(new Point(point.PixX, point.PixY)), 2, 2);
                else if (point.PointType is PoiShape.Rect or PoiShape.LeftTopRect)
                    context.DrawRectangle(fill, pen, rect);
                else if (point.PointType == PoiShape.Circle)
                    context.DrawEllipse(fill, pen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
                else context.DrawEllipse(pen.Brush, null, transform.Transform(new Point(point.PixX, point.PixY)), 2, 2);
            }
        }
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private static Rect PointBounds(PoiPoint point)
    {
        double width = Math.Max(0, point.PixWidth), height = point.PointType == PoiShape.Circle ? width : Math.Max(0, point.PixHeight);
        return point.PointType == PoiShape.LeftTopRect ? new Rect(point.PixX, point.PixY, width, height)
            : new Rect(point.PixX - width / 2, point.PixY - height / 2, width, height);
    }
}
