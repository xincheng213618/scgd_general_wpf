using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output
{
    /// <summary>Captures WPF pixels and drawings on their owning dispatcher without retaining the view.</summary>
    internal sealed class ImageSnapshotCapture
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ImageSnapshotCapture));
        private readonly SnapshotImageBufferPool snapshotBufferPool = new();

        internal void ReleaseBuffer() => snapshotBufferPool.Release();

        internal BitmapSource? CaptureRendered(
            DrawCanvas surface,
            double dpiX,
            double dpiY,
            Action commitDrawingEdits)
        {
            surface.Dispatcher.VerifyAccess();
            commitDrawingEdits();
            surface.UpdateLayout();
            int pixelWidth = GetRenderPixelLength(surface.ActualWidth, surface.RenderSize.Width);
            int pixelHeight = GetRenderPixelLength(surface.ActualHeight, surface.RenderSize.Height);
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                log.WarnFormat(
                    "Skip capturing ImageView because render size is invalid. Actual={0}x{1}, RenderSize={2}x{3}, Source={4}",
                    surface.ActualWidth,
                    surface.ActualHeight,
                    surface.RenderSize.Width,
                    surface.RenderSize.Height,
                    surface.Source?.GetType().FullName ?? "<null>");
                return null;
            }

            dpiX = GetPositiveDpi(dpiX);
            dpiY = GetPositiveDpi(dpiY);

            RenderTargetBitmap renderTargetBitmap = new(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(surface);
            renderTargetBitmap.Freeze();
            return renderTargetBitmap;
        }

        internal ImageViewSnapshot? CaptureForBackgroundSave(
            DrawCanvas surface,
            ImageSource? loadedSource,
            double dpiX,
            double dpiY,
            bool includeOverlays,
            Action commitDrawingEdits)
        {
            surface.Dispatcher.VerifyAccess();
            BitmapSource? source = loadedSource as BitmapSource
                ?? surface.Source as BitmapSource;
            if (source == null)
                return null;

            if (includeOverlays)
            {
                foreach (Visual visual in surface.Visuals)
                {
                    if (visual is not DrawingVisual drawingVisual
                        || drawingVisual.Effect != null
                        || drawingVisual.CacheMode != null)
                    {
                        log.WarnFormat(
                            "ImageView background snapshot does not support visual type {0}.",
                            visual.GetType().FullName);
                        return null;
                    }
                }

                commitDrawingEdits();
            }

            SnapshotImageBufferLease? imageBuffer = null;
            BitmapSource? frozenSource = null;
            try
            {
                DrawingGroup scene = new();
                if (source is WriteableBitmap writeableBitmap && !writeableBitmap.IsFrozen)
                {
                    imageBuffer = snapshotBufferPool.Capture(writeableBitmap);
                }
                else
                {
                    frozenSource = source.IsFrozen ? source : source.CloneCurrentValue();
                    if (!frozenSource.IsFrozen)
                        frozenSource.Freeze();
                }

                if (includeOverlays)
                {
                    foreach (Visual visual in surface.Visuals)
                    {
                        DrawingGroup? drawing = CloneVisualDrawing(visual);
                        if (drawing == null)
                        {
                            log.WarnFormat(
                                "ImageView background snapshot does not support visual type {0}.",
                                visual.GetType().FullName);
                            return null;
                        }
                        scene.Children.Add(drawing);
                    }
                }

                dpiX = GetPositiveDpi(dpiX);
                dpiY = GetPositiveDpi(dpiY);
                if (imageBuffer != null)
                {
                    ImageViewSnapshot snapshot = ImageViewSnapshot.Create(
                        scene,
                        imageBuffer,
                        source.PixelWidth,
                        source.PixelHeight,
                        dpiX,
                        dpiY);
                    imageBuffer = null;
                    return snapshot;
                }

                return ImageViewSnapshot.Create(
                    scene,
                    frozenSource!,
                    source.PixelWidth,
                    source.PixelHeight,
                    dpiX,
                    dpiY);
            }
            catch (Exception ex)
            {
                log.Warn("Unable to prepare an ImageView background snapshot.", ex);
                return null;
            }
            finally
            {
                imageBuffer?.Dispose();
            }
        }

        private static DrawingGroup? CloneVisualDrawing(Visual visual)
        {
            if (visual is not DrawingVisual drawingVisual
                || drawingVisual.Effect != null
                || drawingVisual.CacheMode != null)
            {
                return null;
            }

            DrawingGroup drawing = new();
            DrawingGroup? visualDrawing = drawingVisual.Drawing;
            if (visualDrawing != null)
                drawing.Children.Add(visualDrawing.CloneCurrentValue());

            TransformGroup transforms = new();
            if (drawingVisual.Transform != null && !drawingVisual.Transform.Value.IsIdentity)
                transforms.Children.Add(drawingVisual.Transform.CloneCurrentValue());
            Vector offset = drawingVisual.Offset;
            if (offset.X != 0 || offset.Y != 0)
                transforms.Children.Add(new TranslateTransform(offset.X, offset.Y));
            if (transforms.Children.Count > 0)
                drawing.Transform = transforms;

            if (drawingVisual.Clip != null)
                drawing.ClipGeometry = drawingVisual.Clip.CloneCurrentValue();
            drawing.Opacity = drawingVisual.Opacity;
            if (drawingVisual.OpacityMask != null)
                drawing.OpacityMask = drawingVisual.OpacityMask.CloneCurrentValue();
            return drawing;
        }

        private static int GetRenderPixelLength(params double[] values)
        {
            foreach (double value in values)
            {
                if (IsPositiveFinite(value))
                {
                    return Math.Max(1, (int)Math.Ceiling(value));
                }
            }

            return 0;
        }

        private static double GetPositiveDpi(double value)
        {
            return IsPositiveFinite(value) ? value : 96d;
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
