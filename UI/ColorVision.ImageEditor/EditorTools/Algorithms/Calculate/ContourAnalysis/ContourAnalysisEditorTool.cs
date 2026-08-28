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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ContourAnalysis
{
    /// <summary>ImageView adapter for catalog-backed contour analysis.</summary>
    public sealed class ContourAnalysisEditorTool(ImageProcessingContext image, DrawEditorContext draw)
    {
        private readonly Guid _ownerId = Guid.NewGuid();

        public async void Execute()
        {
            ContourAnalysisParameters? parameters = EditParameters();
            if (parameters != null) await ExecuteAsync(null, parameters);
        }

        public async void Execute(SelectShapeType shapeType)
        {
            ContourAnalysisParameters? parameters = EditParameters();
            if (parameters == null) return;
            SelectResult? selection = await new TransientRoiSelectionSession(draw, shapeType).Start();
            if (selection == null) return;
            AlgorithmRoi roi = RoiStatistics.RoiStatisticsEditorTool.FromSelection(selection, image.ViewBitmapSource as System.Windows.Media.Imaging.BitmapSource);
            await ExecuteAsync(roi, parameters, selection.SourceScope);
        }

        internal async Task ExecuteAsync(AlgorithmRoi? roi, ContourAnalysisParameters parameters, ImageSelectionScope? expectedScope = null)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, parameters, roi);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput input;
            try
            {
                input = ImageAlgorithmInputFactory.Acquire(image, expectedScope);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "轮廓提取", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法确定当前图像 revision。", "轮廓提取", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(
                image,
                documentId,
                sourceRevision,
                _ownerId,
                invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("轮廓提取", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "轮廓提取", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    RequiredCapabilities = AlgorithmInvocationCapabilities.Derive(
                        AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                        inputCount: 1,
                        hasRoi: roi != null),
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "轮廓提取", MessageBoxButton.OK, MessageBoxImage.Error);
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
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.IsCurrent(image, documentId, sourceRevision, invocation.InvocationId))
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
                MessageBox.Show(progressWindow.Owner, message, "轮廓提取失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }

            previous?.Close();
            ContourAnalysisResultWindow resultWindow;
            try
            {
                resultWindow = new ContourAnalysisResultWindow(result, image, draw) { Owner = Application.Current.GetActiveWindow() };
            }
            catch (Exception exception)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "轮廓提取结果", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, resultWindow))
            {
                resultWindow.Close();
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
                return;
            }
            resultWindow.Show();
        }

        private static ContourAnalysisParameters? EditParameters()
        {
            ContourAnalysisParameters parameters = new();
            PropertyEditorWindow window = new(parameters)
            {
                Title = "轮廓提取参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            bool submitted = false;
            window.Submitted += (_, _) => submitted = true;
            window.ShowDialog();
            return submitted ? parameters : null;
        }
    }

    public sealed class ContourAnalysisContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu, IAlgorithmCatalogBoundMenu
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.Contours;

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            ContourAnalysisEditorTool tool = new(image, draw);
            return
            [
                new MenuItemMetadata { OwnerGuid = "AlgorithmsCall", GuidId = "ContourAnalysis", Order = 4, Header = "轮廓提取" },
                Item("ContourAnalysisWholeImage", "整图...", 0, tool.Execute, hasRoi: false),
                Item("ContourAnalysisRectangle", "矩形 ROI...", 1, () => tool.Execute(SelectShapeType.Rectangle), hasRoi: true),
                Item("ContourAnalysisCircle", "圆形 ROI...", 2, () => tool.Execute(SelectShapeType.Circle), hasRoi: true),
                Item("ContourAnalysisPolygon", "多边形 ROI...", 3, () => tool.Execute(SelectShapeType.Polygon), hasRoi: true),
            ];
        }

        private MenuItemMetadata Item(string id, string header, int order, Action execute, bool hasRoi) => new()
        {
            OwnerGuid = "ContourAnalysis",
            GuidId = id,
            Order = order,
            Header = header,
            Command = new RelayCommand(_ => execute(), _ => CanExecute(hasRoi)),
        };

        private bool CanExecute(bool hasRoi)
            => image.AlgorithmRuntime.Catalog.TryResolve(AlgorithmId, out AlgorithmDescriptor? descriptor)
                && descriptor != null
                && StandardAlgorithmAdapterContract.IsCompatible(descriptor)
                && StandardAlgorithmAdapterContract.TryGetInteractiveRequiredCapabilities(
                    descriptor,
                    inputCount: 1,
                    hasRoi,
                    AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                    out AlgorithmHostCapabilities required)
                && image.AlgorithmRuntime.CanExecuteDescriptor(descriptor, required);
    }
}
