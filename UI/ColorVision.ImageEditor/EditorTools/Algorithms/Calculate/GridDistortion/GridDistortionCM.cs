using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GridDistortion
{
    internal static class GridDistortionImageViewRunner
    {
        private static readonly ConditionalWeakTable<ImageProcessingContext, GridDistortionOptions> Options = new();

        public static void ShowOptions(ImageProcessingContext imageContext, DrawEditorContext drawContext, RoiRect roi)
        {
            // Opening a new request also invalidates an older in-flight result, including when cancelled.
            AlgorithmResultOverlay.InvalidateRequest(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            GridDistortionOptions options = Options.GetValue(imageContext, static _ => new());
            PropertyEditorWindow window = new(options, PropertyEditorEditMode.Transactional)
            {
                Title = "点阵畸变分析 (V2) 参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submitted += (_, _) => Run(imageContext, drawContext, roi, options);
            window.ShowDialog();
        }

        internal static void Run(ImageProcessingContext imageContext, DrawEditorContext drawContext, RoiRect roi, GridDistortionOptions options)
        {
            long request = AlgorithmResultOverlay.BeginRequest(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            if (!options.TryValidate(out string error))
            {
                MessageBox.Show(error, "点阵畸变参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            GridDistortionOptions snapshot = new()
            {
                ExpectedRows = options.ExpectedRows, ExpectedCols = options.ExpectedCols, BrightTarget = options.BrightTarget,
                MaxProcessingSize = options.MaxProcessingSize, MinimumContrast = options.MinimumContrast,
                MaximumGridResidualFraction = options.MaximumGridResidualFraction
            };
            ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null)
            {
                MessageBox.Show("请先打开待分析图像。", "点阵畸变", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                GridDistortionResult result;
                GridDistortionAnalysis? analysis = null;
                try
                {
                    using (lease) result = GridDistortionNative.Run(lease.Image, roi, snapshot);
                    if (result.Success)
                    {
                        try { analysis = GridDistortionAnalysis.Calculate(result); }
                        catch (ArgumentException ex)
                        { result = GridDistortionResult.CreateFailure("InvalidGridGeometry", ex.Message, result.NativeReturnCode, result.RawJson); }
                    }
                }
                catch (Exception ex)
                {
                    result = GridDistortionResult.CreateFailure("AnalysisFailed", ex.Message);
                }
                imageContext.Dispatcher.BeginInvoke(() =>
                {
                    if (!imageContext.IsCurrentImageRevision(revision) ||
                        !AlgorithmResultOverlay.IsCurrentRequest(drawContext, AlgorithmResultOverlay.GridDistortionTag, request)) return;
                    if (result.Success) RenderResult(imageContext, drawContext, result);
                    GridDistortionResultWindow window = new(result, analysis) { Owner = Application.Current.GetActiveWindow() };
                    window.Show();
                });
            });
        }

        internal static void RenderResult(ImageProcessingContext imageContext, DrawEditorContext drawContext, GridDistortionResult result)
        {
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            if (!result.Success || result.Metrics == null) return;
            double scaleX = LuminousAreaDetector.GetPixelToDipScale(imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double scaleY = LuminousAreaDetector.GetPixelToDipScale(imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            Point Position(GridDistortionPoint point) => new(point.X * scaleX, point.Y * scaleY);
            double zoom = AlgorithmResultOverlay.GetZoom(drawContext);
            Pen gridPen = new(Brushes.DeepSkyBlue, 1 / zoom);
            Pen referencePen = new(Brushes.OrangeRed, 2 / zoom);
            Dictionary<(int Row, int Col), GridDistortionPoint> grid = result.Points.ToDictionary(p => (p.Row, p.Col));
            foreach (GridDistortionPoint point in result.Points)
            {
                if (grid.TryGetValue((point.Row, point.Col + 1), out GridDistortionPoint? right))
                    AlgorithmResultOverlay.AddLine(drawContext, Position(point), Position(right), gridPen, AlgorithmResultOverlay.GridDistortionTag);
                if (grid.TryGetValue((point.Row + 1, point.Col), out GridDistortionPoint? bottom))
                    AlgorithmResultOverlay.AddLine(drawContext, Position(point), Position(bottom), gridPen, AlgorithmResultOverlay.GridDistortionTag);
                bool reference = result.ReferencePointIds.Contains(point.Id);
                AlgorithmResultOverlay.AddLabel(drawContext, Position(point), $"{(reference ? "参考 " : string.Empty)}({point.Row + 1},{point.Col + 1})",
                    reference ? Brushes.OrangeRed : Brushes.DeepSkyBlue, AlgorithmResultOverlay.GridDistortionTag);
            }
            GridDistortionPoint[] referencePoints = result.ReferencePointIds.Select(id => result.Points.Single(p => p.Id == id)).ToArray();
            foreach ((int start, int end) in new[] { (0, 2), (2, 8), (8, 6), (6, 0) })
                AlgorithmResultOverlay.AddLine(drawContext, Position(referencePoints[start]), Position(referencePoints[end]), referencePen, AlgorithmResultOverlay.GridDistortionTag);
        }
    }

    public sealed class CMGridDistortion : IIEditorToolContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        public CMGridDistortion(ImageProcessingContext imageContext, DrawEditorContext drawContext)
        { _imageContext = imageContext; _drawContext = drawContext; }

        public List<MenuItemMetadata> GetContextMenuItems() => new()
        {
            new()
            {
                OwnerGuid = "AlgorithmsCall", GuidId = "GridDistortionV2", Order = 3, Header = "点阵畸变分析 (V2)...",
                Command = new RelayCommand(_ => GridDistortionImageViewRunner.ShowOptions(_imageContext, _drawContext, new RoiRect()))
            }
        };
    }

    public sealed class DVCMGridDistortion : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;
        public DVCMGridDistortion(ImageProcessingContext imageContext, DrawEditorContext drawContext, ImageViewConfig config)
        { _imageContext = imageContext; _drawContext = drawContext; _config = config; }
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not IRectangle rectangle) return Array.Empty<MenuItem>();
            double scaleX = LuminousAreaDetector.GetDipToPixelScale(_config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double scaleY = LuminousAreaDetector.GetDipToPixelScale(_config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            double left = Math.Floor(rectangle.Rect.Left * scaleX), top = Math.Floor(rectangle.Rect.Top * scaleY);
            double right = Math.Ceiling(rectangle.Rect.Right * scaleX), bottom = Math.Ceiling(rectangle.Rect.Bottom * scaleY);
            if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom) ||
                left < 0 || top < 0 || right > int.MaxValue || bottom > int.MaxValue || right <= left || bottom <= top) return Array.Empty<MenuItem>();
            RoiRect roi = new((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            MenuItem item = new() { Header = "点阵畸变分析 (V2)..." };
            item.Click += (_, _) => GridDistortionImageViewRunner.ShowOptions(_imageContext, _drawContext, roi);
            return new[] { item };
        }
    }
}
