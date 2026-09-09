using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public sealed class LumFourColorPoiOptions : IConfig
    {
        public bool UseRectangle { get; set; }
        public CircleManagerConfig Circle { get; set; } = new();
        public RectangleManagerConfig Rectangle { get; set; } = new();
    }

    // Owns the calibration view's single ROI; drawing and size editing belong to ImageView.
    internal sealed class LumFourColorPoiEditor : IDisposable
    {
        private readonly ImageView view;
        private readonly CircleManager circle;
        private readonly RectangleManager rectangle;
        private readonly Action<string> status;
        private LumFourColorCalibrationSample? sample;
        private LumFourColorCieCapture? frame;
        private IDrawingVisual? drawing;
        private bool updating, disposed;
        private DispatcherOperation? pending;

        public LumFourColorPoiEditor(ImageView view, LumFourColorPoiOptions options, Action<string> status)
        {
            this.view = view;
            this.status = status;
            circle = new CircleManager(view.EditorContext.DrawEditorContext) { Config = options.Circle };
            rectangle = new RectangleManager(view.EditorContext.DrawEditorContext) { Config = options.Rectangle };
            // Every color has exactly one measurement region.
            circle.Config.IsContinuous = false;
            rectangle.Config.IsContinuous = false;
            view.ImageEditMode = true;
            view.Config.IsToolBarAlVisible = false;
            view.Config.IsToolBarTopVisible = false;
            view.Config.IsToolBarLeftVisible = false;
            view.Config.IsToolBarRightVisible = false;
            view.Config.IsToolBarDrawVisible = true;
            // Keep ImageView's inline inspector available; the window supplies the two allowed shape choices.
            ((FrameworkElement)view.FindName("ToolBarDraw")).Visibility = Visibility.Collapsed;
            view.AllowDrop = false;
            view.PreviewDragOver += BlockDrop;
            view.PreviewDrop += BlockDrop;
            // Opening an unrelated bitmap here would separate the preview from the CIE being measured.
            CommandManager.AddPreviewExecutedHandler(view, BlockImageCommands);
            view.ContextMenuOpening += BlockContextMenu;
            view.EditorContext.DrawingVisualLists.CollectionChanged += DrawingsChanged;
            view.ImageShow.LostMouseCapture += DrawingFinished;
            view.ImageSourceLoaded += ImageSourceLoaded;
        }

        private static void BlockDrop(object sender, DragEventArgs e) { e.Effects = DragDropEffects.None; e.Handled = true; }
        private static void BlockImageCommands(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Open || e.Command == ApplicationCommands.Close) e.Handled = true;
        }
        private static void BlockContextMenu(object sender, System.Windows.Controls.ContextMenuEventArgs e) => e.Handled = true;

        private void ImageSourceLoaded(object? sender, ImageViewImageSourceLoadedEventArgs e)
        {
            if (!updating && sample != null)
            {
                sample.ClearCamera();
                status("显示图像已改变，请重新选择 CIE 或取图。");
            }
        }

        public void ShowSample(LumFourColorCalibrationSample? value)
        {
            if (ReferenceEquals(sample, value) && ReferenceEquals(frame, value?.Frame)) return;
            Cancel();
            updating = true;
            try
            {
                DetachDrawing();
                view.Clear();
                sample = value;
                frame = value?.Frame;
                if (value?.Preview != null)
                {
                    view.SetImageSource(value.Preview, false, false);
                    view.UpdateZoomAndScale();
                    if (value.Poi is PoiMeasurementPoint poi)
                    {
                        IDrawingVisual restored = poi.Shape == PoiMeasurementShape.Circle
                            ? new DVCircleText(new CircleTextProperties { Center = new Point(poi.X, poi.Y), Radius = poi.Width / 2d, Text = "POI" })
                            : new DVRectangleText(new RectangleTextProperties { Rect = new Rect(poi.X - poi.Width / 2, poi.Y - poi.Height / 2, poi.Width, poi.Height), Text = "POI" });
                        view.ImageShow.AddVisual((Visual)restored);
                        AttachDrawing(restored);
                        view.EditorContext.SelectionVisual.SetRender((ISelectVisual)restored);
                    }
                }
            }
            finally { updating = false; }
        }

        public void Begin(bool useRectangle)
        {
            if (frame == null) return;
            Cancel();
            view.EditorContext.SelectionVisual.ClearRender();
            if (useRectangle) rectangle.IsChecked = true;
            else circle.IsChecked = true;
        }

        public void Cancel()
        {
            pending?.Abort();
            pending = null;
            circle.IsChecked = false;
            rectangle.IsChecked = false;
        }

        private void AttachDrawing(IDrawingVisual value)
        {
            DetachDrawing();
            drawing = value;
            drawing.BaseAttribute.PropertyChanged += GeometryChanged;
        }

        private void DetachDrawing()
        {
            if (drawing != null) drawing.BaseAttribute.PropertyChanged -= GeometryChanged;
            drawing = null;
        }

        private void DrawingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (updating || disposed) return;
            updating = true;
            try
            {
                IDrawingVisual? latest = e.NewItems?.OfType<IDrawingVisual>().LastOrDefault();
                if (latest != null)
                {
                    foreach (var old in view.EditorContext.DrawingVisualLists.Where(item => item != latest).ToArray())
                        view.ImageShow.RemoveVisual((Visual)old);
                    AttachDrawing(latest);
                }
                else if (drawing != null && !view.EditorContext.DrawingVisualLists.Contains(drawing)) DetachDrawing();
            }
            finally { updating = false; }
            RequestMeasurement();
        }

        private void GeometryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Center" or "Radius" or "RadiusY" or "Rect") RequestMeasurement();
        }

        private void DrawingFinished(object sender, MouseEventArgs e) => RequestMeasurement();

        private void RequestMeasurement()
        {
            if (updating || disposed) return;
            sample?.ClearCameraMeasurement();
            pending?.Abort();
            pending = view.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                pending = null;
                if (disposed || view.ImageShow.IsMouseCaptured || sample?.Frame == null || drawing == null) return;
                try
                {
                    PoiMeasurementPoint point = GetMeasurementPoint(drawing.BaseAttribute, sample.Frame.Width, sample.Frame.Height);
                    sample.SetCameraMeasurement(point, LumFourColorCieService.Measure(sample.Frame, point));
                    status($"{sample.Name} 的 POI 已更新，可拖动或调整尺寸。");
                }
                catch (Exception ex) { status(ex.Message); }
            }));
        }

        internal static PoiMeasurementPoint GetMeasurementPoint(BaseProperties properties, int imageWidth, int imageHeight)
        {
            double cx, cy, width, height;
            PoiMeasurementShape shape;
            switch (properties)
            {
                case CircleProperties c:
                    if (c.Radius != c.RadiusY) throw new InvalidOperationException("POI 请使用正圆，两个半径须一致。");
                    (cx, cy, width, height, shape) = (c.Center.X, c.Center.Y, c.Radius * 2, c.Radius * 2, PoiMeasurementShape.Circle);
                    break;
                case RectangleProperties r:
                    (cx, cy, width, height, shape) = (r.Rect.X + r.Rect.Width / 2, r.Rect.Y + r.Rect.Height / 2, r.Rect.Width, r.Rect.Height, PoiMeasurementShape.Rect);
                    break;
                default: throw new InvalidOperationException("POI 仅支持圆形或矩形。");
            }
            if (!double.IsFinite(cx) || !double.IsFinite(cy) || !double.IsFinite(width) || !double.IsFinite(height)
                || width < 1 || height < 1 || cx - width / 2 < 0 || cy - height / 2 < 0
                || cx + width / 2 > imageWidth || cy + height / 2 > imageHeight)
                throw new InvalidOperationException("POI 必须完整位于图像内，尺寸至少为 1 像素，请调整区域。");
            int x = (int)cx, y = (int)cy, w = (int)width, h = (int)height;
            if (x - w / 2 < 0 || y - h / 2 < 0 || x - w / 2 + w > imageWidth || y - h / 2 + h > imageHeight)
                throw new InvalidOperationException("POI 超出图像边界，请调整区域。");
            return new(x, y, w, h, shape);
        }

        public void Dispose()
        {
            disposed = true;
            Cancel();
            DetachDrawing();
            circle.Dispose();
            rectangle.Dispose();
            view.EditorContext.DrawingVisualLists.CollectionChanged -= DrawingsChanged;
            view.ImageShow.LostMouseCapture -= DrawingFinished;
            view.ImageSourceLoaded -= ImageSourceLoaded;
            view.PreviewDragOver -= BlockDrop;
            view.PreviewDrop -= BlockDrop;
            CommandManager.RemovePreviewExecutedHandler(view, BlockImageCommands);
            view.ContextMenuOpening -= BlockContextMenu;
            view.Dispose();
        }
    }
}
