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
        private readonly Guid _ownerId = Guid.NewGuid();

        public async void ExecuteHorizontal()
        {
            AlgorithmAnalysisWindowOwner windowOwner = AlgorithmAnalysisWindowOwner.Capture();
            ImageProfileParameters? parameters = EditParameters(closePath: false, windowOwner.Current);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            if (selection == null) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            double y = selection.Rect.Top * scaleY + selection.Rect.Height * scaleY / 2;
            await ExecuteAsync(new PolylineAlgorithmRoi([new AlgorithmPoint(0, y), new AlgorithmPoint(bitmap.PixelWidth - 1, y)]), parameters, selection.SourceScope, windowOwner);
        }

        public async void ExecuteVertical()
        {
            AlgorithmAnalysisWindowOwner windowOwner = AlgorithmAnalysisWindowOwner.Capture();
            ImageProfileParameters? parameters = EditParameters(closePath: false, windowOwner.Current);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            if (selection == null) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            double x = selection.Rect.Left * scaleX + selection.Rect.Width * scaleX / 2;
            await ExecuteAsync(new PolylineAlgorithmRoi([new AlgorithmPoint(x, 0), new AlgorithmPoint(x, bitmap.PixelHeight - 1)]), parameters, selection.SourceScope, windowOwner);
        }

        public async void ExecutePolyline()
        {
            AlgorithmAnalysisWindowOwner windowOwner = AlgorithmAnalysisWindowOwner.Capture();
            ImageProfileParameters? parameters = EditParameters(closePath: false, windowOwner.Current);
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Polygon).Start();
            if (selection == null || selection.Points.Count < 2) return;
            (double scaleX, double scaleY) = PixelScale(bitmap);
            PolylineAlgorithmRoi roi = new(selection.Points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray());
            await ExecuteAsync(roi, parameters, selection.SourceScope, windowOwner);
        }

        public async void Execute(IReadOnlyList<Point> points, bool closePath)
        {
            AlgorithmAnalysisWindowOwner windowOwner = AlgorithmAnalysisWindowOwner.Capture();
            ImageSelectionScope? sourceScope = TransientRoiSelectionSession.CaptureSourceScope(image);
            if (points.Count < 2 || sourceScope == null) return;
            ImageProfileParameters? parameters = EditParameters(closePath, windowOwner.Current);
            if (parameters == null) return;
            double scaleX = SafeDpi(sourceScope.DpiX) / 96;
            double scaleY = SafeDpi(sourceScope.DpiY) / 96;
            PolylineAlgorithmRoi roi = new(points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray());
            await ExecuteAsync(roi, parameters, sourceScope, windowOwner);
        }

        internal async Task ExecuteAsync(
            PolylineAlgorithmRoi roi,
            ImageProfileParameters parameters,
            ImageSelectionScope? expectedScope = null,
            AlgorithmAnalysisWindowOwner? windowOwner = null)
        {
            windowOwner ??= AlgorithmAnalysisWindowOwner.Capture();
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, roi);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput input;
            try { input = ImageAlgorithmInputFactory.Acquire(image, expectedScope); }
            catch (Exception exception)
            {
                AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(
                image,
                documentId,
                sourceRevision,
                _ownerId,
                invocation.InvocationId);
            ImageAlgorithmProgressWindow? progressWindow = null;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("剖面分析", cancellation);
                if (!windowOwner.TryAssign(progressWindow))
                    throw new InvalidOperationException("发起剖面分析的窗口已关闭，请重试。");
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => progressWindow?.Complete(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(input.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId), ref ignored);
                AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult? result = null;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [input],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(input.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId), ref ignored);
                if (!progressWindow.WasCancelled)
                    AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "剖面分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(progressWindow.Complete, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation), ref ignored);
            }

            if (result.Status == AlgorithmResultStatus.Cancelled || progressWindow.WasCancelled
                || !ImageAlgorithmAnalysisSession.IsCurrent(image, documentId, sourceRevision, invocation.InvocationId))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(failure => $"[{failure.Code}] {failure.Message}"));
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                AlgorithmAnalysisMessageBox.Show(windowOwner, message, "剖面分析失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            bool shown = AlgorithmAnalysisResultWindowTransaction.TryShow(
                result,
                windowOwner,
                value => new ImageProfileResultWindow(value, image, draw),
                window => ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, window),
                () => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId),
                previous,
                out Exception? presentationFailure);
            if (!shown && presentationFailure != null)
                AlgorithmAnalysisMessageBox.Show(windowOwner, presentationFailure.Message, "剖面分析结果", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static ImageProfileParameters? EditParameters(bool closePath, Window? owner)
        {
            ImageProfileParameters parameters = new() { ClosePath = closePath };
            PropertyEditorWindow window = new(parameters)
            {
                Title = "剖面采样参数",
                Owner = owner,
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

    public sealed class ImageProfileContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu, IAlgorithmCatalogBoundMenu
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.ImageProfile;

        public bool RequiresRoi => true;

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
