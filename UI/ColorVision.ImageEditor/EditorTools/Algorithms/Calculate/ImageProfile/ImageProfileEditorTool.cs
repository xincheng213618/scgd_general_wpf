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
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile
{
    public sealed class ImageProfileEditorTool(ImageProcessingContext image, DrawEditorContext draw)
    {
        public async void ExecuteHorizontal()
        {
            ImageProfileParameters? parameters = EditParameters(closePath: false);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            if (selection == null) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            double y = selection.Rect.Top * scaleY + selection.Rect.Height * scaleY / 2;
            await ExecuteAsync(new PolylineAlgorithmRoi([new AlgorithmPoint(0, y), new AlgorithmPoint(bitmap.PixelWidth - 1, y)]), parameters);
        }

        public async void ExecuteVertical()
        {
            ImageProfileParameters? parameters = EditParameters(closePath: false);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            if (selection == null) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            double x = selection.Rect.Left * scaleX + selection.Rect.Width * scaleX / 2;
            await ExecuteAsync(new PolylineAlgorithmRoi([new AlgorithmPoint(x, 0), new AlgorithmPoint(x, bitmap.PixelHeight - 1)]), parameters);
        }

        public async void ExecutePolyline()
        {
            ImageProfileParameters? parameters = EditParameters(closePath: false);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Polygon).Start();
            if (selection == null || selection.Points.Count < 2) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            PolylineAlgorithmRoi roi = new(selection.Points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray());
            await ExecuteAsync(roi, parameters);
        }

        public async void Execute(IReadOnlyList<Point> points, bool closePath)
        {
            if (points.Count < 2 || image.ViewBitmapSource is not BitmapSource bitmap) return;
            ImageProfileParameters? parameters = EditParameters(closePath);
            if (parameters == null) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            PolylineAlgorithmRoi roi = new(points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray());
            await ExecuteAsync(roi, parameters);
        }

        internal async Task ExecuteAsync(PolylineAlgorithmRoi roi, ImageProfileParameters parameters)
        {
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, roi);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput input;
            try { input = ImageAlgorithmInputFactory.Acquire(image); }
            catch (Exception exception)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(image, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("剖面分析", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(progressWindow.Owner, exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                progressWindow.Complete();
                ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation);
            }

            if (result.Status == AlgorithmResultStatus.Cancelled || progressWindow.WasCancelled
                || !ImageAlgorithmAnalysisSession.IsCurrent(image, documentId, sourceRevision, invocation.InvocationId))
            {
                result.Dispose();
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(failure => $"[{failure.Code}] {failure.Message}"));
                result.Dispose();
                MessageBox.Show(progressWindow.Owner, message, "剖面分析失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                return;
            }
            previous?.Close();
            try
            {
                ImageProfileResultWindow window = new(result, image, draw) { Owner = Application.Current.GetActiveWindow() };
                ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, window);
                window.Show();
            }
            catch (Exception exception)
            {
                result.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "剖面分析结果", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ImageProfileParameters? EditParameters(bool closePath)
        {
            ImageProfileParameters parameters = new() { ClosePath = closePath };
            PropertyEditorWindow window = new(parameters)
            {
                Title = "剖面采样参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            bool submitted = false;
            window.Submitted += (_, _) => submitted = true;
            window.ShowDialog();
            return submitted ? parameters : null;
        }

        internal static (double X, double Y) PixelScale(BitmapSource bitmap)
            => (SafeDpi(bitmap.DpiX) / 96, SafeDpi(bitmap.DpiY) / 96);

        private static double SafeDpi(double dpi) => double.IsFinite(dpi) && dpi > 0 ? dpi : 96;
    }

    public sealed class ImageProfileContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            ImageProfileEditorTool tool = new(image, draw);
            return
            [
                new MenuItemMetadata { OwnerGuid = "AlgorithmsCall", GuidId = "ImageProfile", Order = 1, Header = "灰度与颜色剖面" },
                Item("ImageProfileHorizontal", "水平剖面...", 0, tool.ExecuteHorizontal),
                Item("ImageProfileVertical", "垂直剖面...", 1, tool.ExecuteVertical),
                Item("ImageProfilePolyline", "任意折线剖面...", 2, tool.ExecutePolyline),
            ];
        }

        private static MenuItemMetadata Item(string id, string header, int order, Action execute) => new()
        {
            OwnerGuid = "ImageProfile",
            GuidId = id,
            Order = order,
            Header = header,
            Command = new RelayCommand(_ => execute()),
        };
    }
}
