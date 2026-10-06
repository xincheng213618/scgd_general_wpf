using LocalizedText = global::ColorVision.ImageEditor.DisplayText;
using ColorVision.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.Mtf;

internal static class StripeMtfImageViewRunner
{
    internal const string OverlayName = "ImageView.StripeMtf.Result";
    private sealed class State
    {
        public StripeMtfParameters Parameters { get; set; } = new();
        public IDisposable? Overlay { get; set; }
    }
    private static readonly ConditionalWeakTable<ImageProcessingContext, State> States = new();

    internal static MtfRoi[] Capture(IEnumerable<IRectangle> rectangles, double dpiX, double dpiY)
    {
        double sx = LuminousAreaDetector.GetDipToPixelScale(dpiX), sy = LuminousAreaDetector.GetDipToPixelScale(dpiY);
        return rectangles.Select((rectangle, index) =>
        {
            if (rectangle is DrawingVisualBase drawing && drawing.BaseAttribute is RegionProperties region && region.Rotation != 0)
                throw new ArgumentException("MTF 测量框不支持旋转，请使用与图像坐标轴平行的矩形。");
            Rect r = rectangle.Rect;
            if (r.IsEmpty || !new[] { r.Left, r.Top, r.Right, r.Bottom }.All(double.IsFinite)) throw new ArgumentException("MTF 测量框无效。");
            string name = rectangle is DVRectangleText text && !string.IsNullOrWhiteSpace(text.Attribute.Text) ? text.Attribute.Text : $"P_{index}";
            int x = checked((int)Math.Floor(r.Left * sx)), y = checked((int)Math.Floor(r.Top * sy));
            return new MtfRoi(name, x, y, checked((int)Math.Ceiling(r.Right * sx) - x), checked((int)Math.Ceiling(r.Bottom * sy) - y));
        }).ToArray();
    }

    public static void ShowOptions(EditorContext editor, IEnumerable<IRectangle> rectangles)
    {
        ImageProcessingContext image = editor.ProcessingContext;
        DrawEditorContext draw = editor.DrawEditorContext;
        State state = States.GetValue(image, static _ => new());
        long request = AlgorithmResultOverlay.BeginRequest(draw, OverlayName);
        state.Overlay?.Dispose(); state.Overlay = null;
        long revision = image.ImageRevision; Guid document = image.DocumentInstanceId;
        try
        {
            MtfRoi[] selected = Capture(rectangles, image.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX), image.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            StripeMtfParameters draft = StripeMtfParameters.FromJson(state.Parameters.ToJson().ToString());
            PropertyEditorWindow dialog = new(draft, PropertyEditorEditMode.Transactional)
            { Title = LocalizedText.Get("条纹 MTF（H / V / 四部）"), Owner = editor.OwnerWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            dialog.Submitted += (_, _) =>
            {
                if (!IsCurrent(image, draw, document, revision, request)) return;
                try
                {
                    StripeMtfParameters options = StripeMtfParameters.FromJson(draft.ToJson().ToString());
                    ImageFrameLease lease = image.AcquireImageFrame() ?? throw new InvalidOperationException("请先打开原始图像。");
                    try
                    {
                        MtfRoi[] regions = selected.Length > 0 ? selected : [new("Center", 0, 0, lease.Image.cols, lease.Image.rows)];
                        StripeMtfAnalyzer.ValidateRegions(regions, lease.Image.cols, lease.Image.rows);
                        state.Parameters = options;
                        Run(editor, state, lease, options, regions, document, revision, request);
                    }
                    catch { lease.Dispose(); throw; }
                }
                catch (Exception error) { MessageBox.Show(editor.OwnerWindow, error.Message, LocalizedText.Get("MTF 参数或图像无效"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            dialog.ShowDialog();
        }
        catch (Exception error) { MessageBox.Show(editor.OwnerWindow, error.Message, LocalizedText.Get("条纹 MTF"), MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    internal static bool IsCurrent(ImageProcessingContext image, DrawEditorContext draw, Guid document, long revision, long request) =>
        image.DocumentInstanceId == document && image.IsCurrentImageRevision(revision) && AlgorithmResultOverlay.IsCurrentRequest(draw, OverlayName, request);

    private static void Run(EditorContext editor, State state, ImageFrameLease lease, StripeMtfParameters options,
        MtfRoi[] regions, Guid document, long revision, long request)
    {
        _ = Task.Run(() =>
        {
            JObject? result = null; Exception? error = null;
            Stopwatch watch = Stopwatch.StartNew();
            try { using (lease) result = StripeMtfAnalyzer.Calculate(lease.Image, options, regions); }
            catch (Exception failure) { error = failure; }
            watch.Stop();
            editor.ProcessingContext.Dispatcher.BeginInvoke(() =>
            {
                ImageProcessingContext image = editor.ProcessingContext; DrawEditorContext draw = editor.DrawEditorContext;
                if (!IsCurrent(image, draw, document, revision, request)) return;
                if (result == null)
                {
                    MessageBox.Show(editor.OwnerWindow, error?.Message ?? LocalizedText.Get("MTF 计算失败。"), LocalizedText.Get("条纹 MTF"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                using AlgorithmResult overlay = CreateOverlay(result, options.PercentageDisplay);
                state.Overlay = AlgorithmOverlayRenderer.Apply(image, draw, overlay);
                new StripeMtfResultWindow(result, options, watch.Elapsed.TotalMilliseconds) { Owner = editor.OwnerWindow }.Show();
            });
        });
    }

    internal static AlgorithmResult CreateOverlay(JObject result, bool percentage)
    {
        List<AlgorithmGeometry> geometry = new(); List<AlgorithmOverlayItem> items = new();
        int index = 0;
        foreach (JToken item in result["result"]!)
        {
            string id = $"mtf-{index++}";
            double x = item.Value<double>("x"), y = item.Value<double>("y");
            geometry.Add(new(id, AlgorithmGeometryKind.Rectangle, [new(x, y), new(x + item.Value<double>("w"), y + item.Value<double>("h"))]));
            items.Add(new(id, new(Stroke: "#FFFF5050", Label: $"{item.Value<string>("name")}  {item.Value<double>("mtfValue"):F4}{(percentage ? "%" : "")}")));
        }
        return new AlgorithmResult { Status = AlgorithmResultStatus.Succeeded, Artifacts =
            [new AlgorithmGeometryArtifact("MTF rectangles", AlgorithmCoordinateSpace.Pixel, geometry), new AlgorithmOverlayArtifact(OverlayName, AlgorithmOverlayLifetime.Transient, items)] };
    }
}

public sealed class CMStripeMtf(EditorContext editor) : IIEditorToolContextMenu
{
    public List<MenuItemMetadata> GetContextMenuItems() => [new()
    {
        OwnerGuid = AlgorithmMenuGroups.ImageQuality.Id, GuidId = "StripeMtf.Local", Order = 12,
        Header = LocalizedText.Get("条纹 MTF（H / V / 四部）…"),
        Command = new RelayCommand(_ => StripeMtfImageViewRunner.ShowOptions(editor, editor.DrawEditorContext.DrawingVisualLists.OfType<IRectangle>()))
    }];
}

public sealed class DVCMStripeMtf(EditorContext editor) : IDVContextMenu
{
    public Type ContextType => typeof(IRectangle);
    public IEnumerable<MenuItem> GetContextMenuItems(object obj)
    {
        if (obj is not IRectangle rectangle) return [];
        MenuItem item = new() { Header = LocalizedText.Get("条纹 MTF（H / V / 四部）…") };
        item.Click += (_, _) => StripeMtfImageViewRunner.ShowOptions(editor, [rectangle]);
        return [item];
    }
}
