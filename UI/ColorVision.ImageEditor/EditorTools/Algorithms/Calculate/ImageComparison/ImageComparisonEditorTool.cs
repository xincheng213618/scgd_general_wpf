using ColorVision.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.RoiStatistics;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageComparison
{
    /// <summary>ImageView adapter for strict two-input image comparison.</summary>
    public sealed class ImageComparisonEditorTool(ImageProcessingContext image, DrawEditorContext draw)
    {
        private readonly Guid _ownerId = Guid.NewGuid();

        public async void Execute() => await ExecuteCoreAsync(null);

        public async void Execute(SelectShapeType shapeType) => await ExecuteCoreAsync(shapeType);

        private async Task ExecuteCoreAsync(SelectShapeType? shapeType)
        {
            AlgorithmAnalysisWindowOwner windowOwner = AlgorithmAnalysisWindowOwner.Capture();
            ImageComparisonParameters? parameters = EditParameters(windowOwner.Current);
            if (parameters == null) return;
            OpenFileDialog dialog = new()
            {
                Title = "选择要与当前图像比较的图像",
                Filter = "图像文件|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp|所有文件|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(windowOwner.Current) != true) return;

            AlgorithmRoi? roi = null;
            ImageSelectionScope? expectedScope = null;
            if (shapeType.HasValue)
            {
                SelectResult? selection = await new TransientRoiSelectionSession(draw, shapeType.Value).Start();
                if (selection == null) return;
                roi = RoiStatisticsEditorTool.FromSelection(selection, image.ViewBitmapSource as BitmapSource);
                expectedScope = selection.SourceScope;
            }
            await ExecuteAsync(dialog.FileName, parameters, roi, expectedScope, windowOwner);
        }

        internal async Task ExecuteAsync(
            string candidatePath,
            ImageComparisonParameters parameters,
            AlgorithmRoi? roi,
            ImageSelectionScope? expectedScope = null,
            AlgorithmAnalysisWindowOwner? windowOwner = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
            ArgumentNullException.ThrowIfNull(parameters);
            windowOwner ??= AlgorithmAnalysisWindowOwner.Capture();
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput? referenceInput = null;
            AlgorithmInput? candidateInput = null;
            BitmapSource referenceSnapshot;
            BitmapSource candidateSnapshot;
            try
            {
                referenceInput = ImageAlgorithmInputFactory.Acquire(image, expectedScope, "reference");
                referenceSnapshot = Snapshot(image.ViewBitmapSource as BitmapSource
                    ?? throw new InvalidOperationException("当前 ImageView 没有可比较的位图。"));
                candidateSnapshot = await Task.Run(() => Load(candidatePath));
                candidateInput = new AlgorithmInput
                {
                    Name = "candidate",
                    Image = ImageAlgorithmInputFactory.Copy(candidateSnapshot),
                    Ownership = AlgorithmInputOwnership.Transferred,
                    SourceUri = Path.GetFullPath(candidatePath),
                    ColorSpace = "encoded-device-values",
                };
            }
            catch (Exception exception)
            {
                referenceInput?.Image.Dispose();
                candidateInput?.Image.Dispose();
                AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!long.TryParse(referenceInput.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision)
                || !image.IsCurrentImageRevision(sourceRevision))
            {
                referenceInput.Image.Dispose();
                candidateInput.Image.Dispose();
                AlgorithmAnalysisMessageBox.Show(windowOwner, "当前图像在创建比较快照时已改变，请重试。", "图像比较", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AlgorithmInvocation invocation = new()
            {
                AlgorithmId = StandardAlgorithmIds.ImageComparison,
                ParameterSchemaVersion = parameters.SchemaVersion,
                Parameters = AlgorithmJson.ToElement(parameters),
                Inputs =
                [
                    new AlgorithmInputReference("reference", Revision: referenceInput.SourceRevision),
                    new AlgorithmInputReference("candidate", Uri: candidateInput.SourceUri),
                ],
                Roi = roi,
                Metadata = ImageComparisonOutputPlan.CreateMetadata(ImageComparisonArtifactOutputs.InteractiveVisualizations),
            };
            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(
                image,
                documentId,
                sourceRevision,
                _ownerId,
                invocation.InvocationId);
            ImageAlgorithmProgressWindow? progressWindow = null;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("图像比较", cancellation);
                if (!windowOwner.TryAssign(progressWindow))
                    throw new InvalidOperationException("发起图像比较的窗口已关闭，请重试。");
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => progressWindow?.Complete(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(referenceInput.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(candidateInput.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId), ref ignored);
                AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult? result = null;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [referenceInput, candidateInput],
                    RequiredCapabilities = AlgorithmInvocationCapabilities.Derive(
                        AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                        inputCount: 2,
                        hasRoi: roi != null),
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(referenceInput.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(candidateInput.Image.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId), ref ignored);
                if (!progressWindow.WasCancelled)
                    AlgorithmAnalysisMessageBox.Show(windowOwner, exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(progressWindow.Complete, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation), ref ignored);
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
                AlgorithmAnalysisMessageBox.Show(windowOwner, message, "图像比较失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                value => new ImageComparisonResultWindow(value, referenceSnapshot, candidateSnapshot, Path.GetFileName(candidatePath), image, draw),
                window => ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, window),
                () => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId),
                previous,
                out Exception? presentationFailure);
            if (!shown && presentationFailure != null)
                AlgorithmAnalysisMessageBox.Show(windowOwner, presentationFailure.Message, "图像比较结果", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static ImageComparisonParameters? EditParameters(Window? owner)
        {
            ImageComparisonParameters parameters = new();
            PropertyEditorWindow window = new(parameters)
            {
                Title = "图像比较参数",
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            bool submitted = false;
            window.Submitted += (_, _) => submitted = true;
            window.ShowDialog();
            return submitted ? parameters : null;
        }

        internal static BitmapSource Load(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("图像文件不存在。", filePath);
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) throw new InvalidDataException("图像文件没有可读取的帧。");
            BitmapSource frame = decoder.Frames[0];
            _ = ImageAlgorithmInputFactory.FromPixelFormat(frame.Format);
            return Snapshot(frame);
        }

        private static WriteableBitmap Snapshot(BitmapSource source)
        {
            WriteableBitmap snapshot = new(source);
            if (snapshot.CanFreeze) snapshot.Freeze();
            return snapshot;
        }
    }

    public sealed class ImageComparisonContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu, IAlgorithmCatalogBoundMenu
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.ImageComparison;

        public int PlannedInputCount => 2;

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            ImageComparisonEditorTool tool = new(image, draw);
            return
            [
                new MenuItemMetadata
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "ImageComparison",
                    Order = 2,
                    Header = "图像比较",
                },
                Item("ImageComparisonWhole", "全图比较...", 0, tool.Execute, hasRoi: false),
                Item("ImageComparisonRectangle", "矩形 ROI...", 1, () => tool.Execute(SelectShapeType.Rectangle), hasRoi: true),
                Item("ImageComparisonCircle", "圆形 ROI...", 2, () => tool.Execute(SelectShapeType.Circle), hasRoi: true),
                Item("ImageComparisonPolygon", "多边形 ROI...", 3, () => tool.Execute(SelectShapeType.Polygon), hasRoi: true),
            ];
        }

        private MenuItemMetadata Item(string id, string header, int order, Action execute, bool hasRoi) => new()
        {
            OwnerGuid = "ImageComparison",
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
                    inputCount: 2,
                    hasRoi,
                    AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                    out AlgorithmHostCapabilities required)
                && image.AlgorithmRuntime.CanExecuteDescriptor(descriptor, required);
    }
}
