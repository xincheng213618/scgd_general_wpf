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
        public async void Execute() => await ExecuteCoreAsync(null);

        public async void Execute(SelectShapeType shapeType) => await ExecuteCoreAsync(shapeType);

        private async Task ExecuteCoreAsync(SelectShapeType? shapeType)
        {
            ImageComparisonParameters? parameters = EditParameters();
            if (parameters == null) return;
            OpenFileDialog dialog = new()
            {
                Title = "选择要与当前图像比较的图像",
                Filter = "图像文件|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp|所有文件|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true) return;

            AlgorithmRoi? roi = null;
            if (shapeType.HasValue)
            {
                SelectResult? selection = await new TransientRoiSelectionSession(draw, shapeType.Value).Start();
                if (selection == null) return;
                roi = RoiStatisticsEditorTool.FromSelection(selection, image.ViewBitmapSource as BitmapSource);
            }
            await ExecuteAsync(dialog.FileName, parameters, roi);
        }

        internal async Task ExecuteAsync(string candidatePath, ImageComparisonParameters parameters, AlgorithmRoi? roi)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
            ArgumentNullException.ThrowIfNull(parameters);
            Guid documentId = image.DocumentInstanceId;
            AlgorithmInput? referenceInput = null;
            AlgorithmInput? candidateInput = null;
            BitmapSource referenceSnapshot;
            BitmapSource candidateSnapshot;
            try
            {
                referenceInput = ImageAlgorithmInputFactory.Acquire(image, "reference");
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
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!long.TryParse(referenceInput.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision)
                || !image.IsCurrentImageRevision(sourceRevision))
            {
                referenceInput.Image.Dispose();
                candidateInput.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "当前图像在创建比较快照时已改变，请重试。", "图像比较", MessageBoxButton.OK, MessageBoxImage.Information);
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
            };
            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(image, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("图像比较", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                referenceInput.Image.Dispose();
                candidateInput.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult? result = null;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [referenceInput, candidateInput],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                referenceInput.Image.Dispose();
                candidateInput.Image.Dispose();
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "图像比较", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(progressWindow.Owner, message, "图像比较失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                return;
            }

            previous?.Close();
            ImageComparisonResultWindow resultWindow;
            try
            {
                resultWindow = new ImageComparisonResultWindow(result, referenceSnapshot, candidateSnapshot, Path.GetFileName(candidatePath), image, draw)
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
            }
            catch (Exception exception)
            {
                result.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像比较结果", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, resultWindow);
            resultWindow.Show();
        }

        private static ImageComparisonParameters? EditParameters()
        {
            ImageComparisonParameters parameters = new();
            PropertyEditorWindow window = new(parameters)
            {
                Title = "图像比较参数",
                Owner = Application.Current.GetActiveWindow(),
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

    public sealed class ImageComparisonContextMenu(ImageProcessingContext image, DrawEditorContext draw) : IIEditorToolContextMenu
    {
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
                Item("ImageComparisonWhole", "全图比较...", 0, tool.Execute),
                Item("ImageComparisonRectangle", "矩形 ROI...", 1, () => tool.Execute(SelectShapeType.Rectangle)),
                Item("ImageComparisonCircle", "圆形 ROI...", 2, () => tool.Execute(SelectShapeType.Circle)),
                Item("ImageComparisonPolygon", "多边形 ROI...", 3, () => tool.Execute(SelectShapeType.Polygon)),
            ];
        }

        private static MenuItemMetadata Item(string id, string header, int order, Action execute) => new()
        {
            OwnerGuid = "ImageComparison",
            GuidId = id,
            Order = order,
            Header = header,
            Command = new RelayCommand(_ => execute()),
        };
    }
}
