using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FrequencySpectrum
{
    /// <summary>ImageView adapter for the catalog-backed frequency-spectrum analysis.</summary>
    public sealed class FrequencySpectrumEditorTool
    {
        private readonly ImageProcessingContext _image;
        private readonly Guid _ownerId = Guid.NewGuid();

        public FrequencySpectrumEditorTool(ImageProcessingContext image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public async Task ExecuteAsync()
        {
            FrequencySpectrumParameters parameters = new();
            bool submitted = false;
            PropertyEditorWindow editor = new(parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = Application.Current.GetActiveWindow(),
                Title = "FFT / 频域分析参数",
            };
            editor.Submitted += (_, _) => submitted = true;
            editor.ShowDialog();
            if (!submitted) return;
            await ExecuteAsync(parameters);
        }

        internal async Task ExecuteAsync(FrequencySpectrumParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, parameters);
            Guid documentId = _image.DocumentInstanceId;
            AlgorithmInput input;
            try
            {
                input = ImageAlgorithmInputFactory.Acquire(_image);
            }
            catch (Exception exception)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "FFT / 频域分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sourceRevision))
            {
                input.Image.Dispose();
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法确定当前图像 revision。", "FFT / 频域分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(
                _image, documentId, sourceRevision, _ownerId, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("FFT / 频域分析", cancellation)
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "FFT / 频域分析", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AlgorithmResult result;
            try
            {
                Progress<AlgorithmProgress> progress = new(value => progressWindow.Report(value));
                result = await _image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [input],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                    Progress = progress,
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                if (!progressWindow.WasCancelled)
                    MessageBox.Show(progressWindow.Owner, exception.Message, "FFT / 频域分析", MessageBoxButton.OK, MessageBoxImage.Error);
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
                string message = string.Join(Environment.NewLine, result.Failures.Select(value => $"[{value.Code}] {value.Message}"));
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(progressWindow.Owner, message, "FFT / 频域分析失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(_image, documentId, sourceRevision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }

            previous?.Close();
            FrequencySpectrumResultWindow resultWindow;
            try
            {
                resultWindow = new FrequencySpectrumResultWindow(result)
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
            }
            catch (Exception exception)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                MessageBox.Show(Application.Current.GetActiveWindow(), exception.Message, "FFT / 频域分析结果", MessageBoxButton.OK, MessageBoxImage.Error);
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
