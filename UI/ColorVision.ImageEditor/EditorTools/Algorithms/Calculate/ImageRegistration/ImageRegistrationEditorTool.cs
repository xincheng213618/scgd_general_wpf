using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageComparison;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageRegistration
{
    /// <summary>ImageView adapter for strict two-input local registration.</summary>
    public sealed class ImageRegistrationEditorTool
    {
        private readonly ImageProcessingContext _image;
        private readonly DrawEditorContext? _draw;
        private readonly Guid _ownerId = Guid.NewGuid();

        public ImageRegistrationEditorTool(ImageProcessingContext image, DrawEditorContext? draw = null)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _draw = draw;
        }

        public async Task ExecuteAsync()
        {
            ImageRegistrationParametersWindow parametersWindow = new(_image.AlgorithmRuntime.Catalog) { Owner = Application.Current.GetActiveWindow() };
            if (parametersWindow.ShowDialog() != true) return;
            OpenFileDialog dialog = new()
            {
                Title = "选择 moving 图像",
                Filter = "图像文件|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp|所有文件|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true) return;
            await ExecuteAsync(dialog.FileName, parametersWindow.Parameters, parametersWindow.PresetId);
        }

        internal async Task ExecuteAsync(string movingPath, ImageRegistrationParameters parameters, string? presetId = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(movingPath);
            ArgumentNullException.ThrowIfNull(parameters);
            Guid documentId = _image.DocumentInstanceId;
            AlgorithmInput? referenceInput = null;
            AlgorithmInput? movingInput = null;
            try
            {
                referenceInput = ImageAlgorithmInputFactory.Acquire(_image, "reference");
                BitmapSource movingSnapshot = await Task.Run(() => ImageComparisonEditorTool.Load(movingPath));
                movingInput = new AlgorithmInput
                {
                    Name = "moving",
                    Image = ImageAlgorithmInputFactory.Copy(movingSnapshot),
                    Ownership = AlgorithmInputOwnership.Transferred,
                    SourceUri = Path.GetFullPath(movingPath),
                    ColorSpace = "encoded-device-values",
                };
            }
            catch (Exception exception)
            {
                referenceInput?.Image.Dispose();
                movingInput?.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像配准", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!long.TryParse(referenceInput.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision)
                || !_image.IsCurrentImageRevision(sourceRevision))
            {
                referenceInput.Image.Dispose();
                movingInput.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "当前图像在创建配准快照时已改变，请重试。", "图像配准", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AlgorithmInvocation baseInvocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration, parameters);
            AlgorithmInvocation invocation = new()
            {
                InvocationId = baseInvocation.InvocationId,
                AlgorithmId = baseInvocation.AlgorithmId,
                ParameterSchemaVersion = baseInvocation.ParameterSchemaVersion,
                Parameters = baseInvocation.Parameters,
                PresetId = presetId,
                Inputs =
                [
                    new AlgorithmInputReference("reference", Revision: referenceInput.SourceRevision),
                    new AlgorithmInputReference("moving", Uri: movingInput.SourceUri),
                ],
            };
            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(_image, documentId, sourceRevision, _ownerId, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("图像配准", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                referenceInput.Image.Dispose();
                movingInput.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像配准", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult result;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await _image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [referenceInput, movingInput],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                referenceInput.Image.Dispose();
                movingInput.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "图像配准", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                progressWindow.Complete();
                ImageAlgorithmAnalysisSession.CompleteRun(_image, invocation.InvocationId, cancellation);
            }

            if (result.Status == AlgorithmResultStatus.Cancelled || progressWindow.WasCancelled)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.IsCurrent(_image, documentId, sourceRevision, invocation.InvocationId))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(failure => $"[{failure.Code}] {failure.Message}"));
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(progressWindow.Owner, message, "图像配准失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(_image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }

            previous?.Close();
            ImageRegistrationResultWindow resultWindow;
            try
            {
                resultWindow = new ImageRegistrationResultWindow(result, Path.GetFileName(movingPath), _image, _draw) { Owner = Application.Current.GetActiveWindow() };
            }
            catch (Exception exception)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "图像配准结果", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.Present(_image, invocation.InvocationId, resultWindow))
            {
                resultWindow.Close();
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }
            resultWindow.Show();
        }
    }
}
