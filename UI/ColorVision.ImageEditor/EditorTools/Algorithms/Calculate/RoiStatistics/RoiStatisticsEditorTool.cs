using ColorVision.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.RoiStatistics
{
    /// <summary>ImageView adapter for the catalog-backed ROI statistics algorithm.</summary>
    public sealed class RoiStatisticsEditorTool(ImageProcessingContext image, DrawEditorContext draw)
    {
        public async void Execute(SelectShapeType shapeType)
        {
            RoiStatisticsParameters? parameters = EditParameters();
            if (parameters == null) return;

            SelectResult? selection = await new TransientRoiSelectionSession(draw, shapeType).Start();
            if (selection == null) return;
            AlgorithmRoi roi = FromSelection(selection, image.ViewBitmapSource as BitmapSource);
            await ExecuteAsync(roi, parameters);
        }

        public async void Execute(AlgorithmRoi roi)
        {
            RoiStatisticsParameters? parameters = EditParameters();
            if (parameters == null) return;
            await ExecuteAsync(roi, parameters);
        }

        internal async Task ExecuteAsync(AlgorithmRoi roi, RoiStatisticsParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(roi);
            ArgumentNullException.ThrowIfNull(parameters);
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics, parameters, roi);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput input;
            try
            {
                input = ImageAlgorithmInputFactory.Acquire(image);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "ROI 统计", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法确定当前图像 revision。", "ROI 统计", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(image, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("ROI 统计", cancellation)
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "ROI 统计", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            AlgorithmResult? result = null;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [input],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "ROI 统计", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                progressWindow.Complete();
                ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation);
            }

            if (result.Status == AlgorithmResultStatus.Cancelled || progressWindow.WasCancelled)
            {
                result.Dispose();
                return;
            }
            if (!ImageAlgorithmAnalysisSession.IsCurrent(image, documentId, sourceRevision, invocation.InvocationId))
            {
                result.Dispose();
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(failure => $"[{failure.Code}] {failure.Message}"));
                result.Dispose();
                MessageBox.Show(progressWindow.Owner, message, "ROI 统计失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                return;
            }

            previous?.Close();
            RoiStatisticsResultWindow resultWindow;
            try
            {
                resultWindow = new RoiStatisticsResultWindow(result, image, draw)
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
            }
            catch (Exception exception)
            {
                result.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "ROI 统计结果", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, resultWindow);
            resultWindow.Show();
        }

        private static RoiStatisticsParameters? EditParameters()
        {
            RoiStatisticsParameters parameters = new();
            PropertyEditorWindow window = new(parameters)
            {
                Title = "ROI 统计参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            bool submitted = false;
            window.Submitted += (_, _) => submitted = true;
            window.ShowDialog();
            return submitted ? parameters : null;
        }

        internal static AlgorithmRoi FromSelection(SelectResult selection, BitmapSource? source)
        {
            (double scaleX, double scaleY) = PixelScale(source);
            return selection.ShapeType switch
            {
                SelectShapeType.Rectangle => Rectangle(selection.Rect, scaleX, scaleY),
                SelectShapeType.Circle => Circle(selection.Center, selection.Radius, scaleX, scaleY),
                SelectShapeType.Polygon => Polygon(selection.Points, scaleX, scaleY),
                _ => throw new NotSupportedException($"ROI selection type {selection.ShapeType} is unsupported."),
            };
        }

        internal static RectangleAlgorithmRoi Rectangle(Rect rect, double scaleX, double scaleY)
            => new(rect.X * scaleX, rect.Y * scaleY, rect.Width * scaleX, rect.Height * scaleY);

        internal static AlgorithmRoi Circle(Point center, double radius, double scaleX, double scaleY)
        {
            if (Math.Abs(scaleX - scaleY) <= 1e-10 * Math.Max(scaleX, scaleY))
                return new CircleAlgorithmRoi(new AlgorithmPoint(center.X * scaleX, center.Y * scaleY), radius * scaleX);

            const int segments = 64;
            AlgorithmPoint[] points = new AlgorithmPoint[segments];
            for (int index = 0; index < segments; index++)
            {
                double angle = index * Math.PI * 2 / segments;
                points[index] = new AlgorithmPoint(
                    (center.X + radius * Math.Cos(angle)) * scaleX,
                    (center.Y + radius * Math.Sin(angle)) * scaleY);
            }
            return new PolygonAlgorithmRoi(points);
        }

        internal static PolygonAlgorithmRoi Polygon(IEnumerable<Point> points, double scaleX, double scaleY)
            => new(points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray());

        internal static (double X, double Y) PixelScale(BitmapSource? source)
            => source == null ? (1, 1) : (SafeDpi(source.DpiX) / 96, SafeDpi(source.DpiY) / 96);

        private static double SafeDpi(double dpi) => double.IsFinite(dpi) && dpi > 0 ? dpi : 96;
    }

    public sealed class RoiStatisticsContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RoiStatisticsEditorTool tool = new(image, draw);
            return
            [
                new MenuItemMetadata { OwnerGuid = "AlgorithmsCall", GuidId = "RoiStatistics", Order = 0, Header = "ROI 统计" },
                Item("RoiStatisticsRectangle", "矩形 ROI...", 0, () => tool.Execute(SelectShapeType.Rectangle)),
                Item("RoiStatisticsCircle", "圆形 ROI...", 1, () => tool.Execute(SelectShapeType.Circle)),
                Item("RoiStatisticsPolygon", "多边形 ROI...", 2, () => tool.Execute(SelectShapeType.Polygon)),
            ];
        }

        private static MenuItemMetadata Item(string id, string header, int order, Action execute) => new()
        {
            OwnerGuid = "RoiStatistics",
            GuidId = id,
            Order = order,
            Header = header,
            Command = new RelayCommand(_ => execute()),
        };
    }

    public sealed class RoiStatisticsRectangleDrawingContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not IRectangle rectangle) return [];
            (double x, double y) = RoiStatisticsEditorTool.PixelScale(image.ViewBitmapSource as BitmapSource);
            MenuItem item = new() { Header = "ROI 统计..." };
            item.Click += (_, _) => new RoiStatisticsEditorTool(image, draw).Execute(RoiStatisticsEditorTool.Rectangle(rectangle.Rect, x, y));
            return [item];
        }
    }

    public sealed class RoiStatisticsCircleDrawingContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IDVContextMenu
    {
        public Type ContextType => typeof(ICircle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not ICircle circle) return [];
            (double x, double y) = RoiStatisticsEditorTool.PixelScale(image.ViewBitmapSource as BitmapSource);
            MenuItem item = new() { Header = "ROI 统计..." };
            item.Click += (_, _) => new RoiStatisticsEditorTool(image, draw).Execute(RoiStatisticsEditorTool.Circle(circle.Center, circle.Radius, x, y));
            return [item];
        }
    }

    public sealed class RoiStatisticsPolygonDrawingContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IDVContextMenu
    {
        public Type ContextType => typeof(DVPolygon);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not DVPolygon polygon || polygon.Points.Count < 3) return [];
            (double x, double y) = RoiStatisticsEditorTool.PixelScale(image.ViewBitmapSource as BitmapSource);
            MenuItem item = new() { Header = "ROI 统计..." };
            item.Click += (_, _) => new RoiStatisticsEditorTool(image, draw).Execute(RoiStatisticsEditorTool.Polygon(polygon.Points, x, y));
            return [item];
        }
    }

}
