using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.UI;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.MoireAnalysis
{
    public sealed class MoireAnalysisEditorTool
    {
        private readonly ImageProcessingContext _image;
        private readonly Guid _ownerId = Guid.NewGuid();

        public MoireAnalysisEditorTool(ImageProcessingContext image) => _image = image ?? throw new ArgumentNullException(nameof(image));

        public async Task ExecuteAsync()
        {
            MoireAnalysisParameters parameters = new();
            bool submitted = false;
            PropertyEditorWindow editor = new(parameters, PropertyEditorEditMode.Transactional)
            {
                Owner = Application.Current.GetActiveWindow(), Title = "摩尔纹分析参数",
            };
            editor.Submitted += (_, _) => submitted = true;
            editor.ShowDialog();
            if (submitted) await ExecuteAsync(parameters);
        }

        internal async Task ExecuteAsync(MoireAnalysisParameters parameters)
        {
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, parameters);
            Guid documentId = _image.DocumentInstanceId;
            AlgorithmInput input;
            try { input = ImageAlgorithmInputFactory.Acquire(_image); }
            catch (Exception exception) { Error(exception.Message); return; }
            if (!long.TryParse(input.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long revision))
            {
                input.Image.Dispose();
                Error("无法确定当前图像 revision。");
                return;
            }

            using CancellationTokenSource cancellation = ImageAlgorithmAnalysisSession.Begin(_image, documentId, revision, _ownerId, invocation.InvocationId);
            ImageAlgorithmProgressWindow progressWindow;
            try
            {
                progressWindow = new ImageAlgorithmProgressWindow("摩尔纹分析", cancellation) { Owner = Application.Current.GetActiveWindow() };
                progressWindow.Show();
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                Error(exception.Message);
                return;
            }

            AlgorithmResult result;
            try
            {
                result = await _image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = [input],
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                    Progress = new Progress<AlgorithmProgress>(progressWindow.Report),
                }, cancellation.Token);
            }
            catch (Exception exception)
            {
                input.Image.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                if (!progressWindow.WasCancelled) Error(exception.Message);
                return;
            }
            finally
            {
                progressWindow.Complete();
                ImageAlgorithmAnalysisSession.CompleteRun(_image, invocation.InvocationId, cancellation);
            }

            if (result.Status == AlgorithmResultStatus.Cancelled || progressWindow.WasCancelled
                || !ImageAlgorithmAnalysisSession.IsCurrent(_image, documentId, revision, invocation.InvocationId))
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
                Error(message);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.CanPresent(_image, documentId, revision, invocation.InvocationId, out Window? previous))
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }
            previous?.Close();
            MoireAnalysisResultWindow window;
            try { window = new MoireAnalysisResultWindow(result) { Owner = Application.Current.GetActiveWindow() }; }
            catch (Exception exception)
            {
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                Error(exception.Message);
                return;
            }
            if (!ImageAlgorithmAnalysisSession.Present(_image, invocation.InvocationId, window))
            {
                window.Close();
                result.Dispose();
                ImageAlgorithmAnalysisSession.Release(_image, invocation.InvocationId);
                return;
            }
            window.Show();
        }

        private static void Error(string message) => MessageBox.Show(Application.Current.GetActiveWindow(), message, "摩尔纹分析", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
