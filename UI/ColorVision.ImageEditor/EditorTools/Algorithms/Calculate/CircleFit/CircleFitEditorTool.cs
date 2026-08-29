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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.CircleFit
{
    /// <summary>ImageView adapter for fitting a circle to an explicitly selected point set.</summary>
    public sealed class CircleFitEditorTool(ImageProcessingContext image, DrawEditorContext draw)
    {
        private readonly Guid _ownerId = Guid.NewGuid();

        public async void Execute()
        {
            CircleFitParameters? parameters = EditParameters();
            if (parameters == null || image.ViewBitmapSource is not BitmapSource bitmap) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Polygon).Start();
            if (selection == null || selection.Points.Count < 3) return;
            (double scaleX, double scaleY) = ImageProfile.ImageProfileEditorTool.PixelScale(bitmap);
            await ExecuteAsync(new PolylineAlgorithmRoi(
                selection.Points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray()),
                parameters,
                selection.SourceScope);
        }

        internal async Task ExecuteAsync(PolylineAlgorithmRoi roi, CircleFitParameters parameters, ImageSelectionScope? expectedScope = null)
        {
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, parameters, roi);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput input;
            try { input = ImageAlgorithmInputFactory.Acquire(image, expectedScope); }
            catch (Exception exception)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "圆拟合", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法确定当前图像 revision。", "圆拟合", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(image, documentId, sourceRevision, _ownerId, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("圆拟合", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "圆拟合", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult result;
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
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "圆拟合", MessageBoxButton.OK, MessageBoxImage.Error);
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
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(failure => $"[{failure.Code}] {failure.Message}"));
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                MessageBox.Show(progressWindow.Owner, message, "圆拟合失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            previous?.Close();
            try
            {
                CircleFitResultWindow window = new(result, image, draw) { Owner = Application.Current.GetActiveWindow() };
                if (!ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, window))
                {
                    window.Close();
                    result.Dispose();
                    ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                    return;
                }
                window.Show();
            }
            catch (Exception exception)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "圆拟合结果", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static CircleFitParameters? EditParameters()
        {
            CircleFitParameters parameters = new();
            PropertyEditorWindow window = new(parameters)
            {
                Title = "圆拟合参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            bool submitted = false;
            window.Submitted += (_, _) => submitted = true;
            window.ShowDialog();
            return submitted ? parameters : null;
        }
    }

    public sealed class CircleFitContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu, IAlgorithmCatalogBoundMenu
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.CircleFit;

        public bool RequiresRoi => true;

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            CircleFitEditorTool tool = new(image, draw);
            return
            [
                new MenuItemMetadata
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "CircleFit",
                    Order = 7,
                    Header = "圆拟合...",
                    Command = new RelayCommand(_ => tool.Execute()),
                },
            ];
        }
    }
}
