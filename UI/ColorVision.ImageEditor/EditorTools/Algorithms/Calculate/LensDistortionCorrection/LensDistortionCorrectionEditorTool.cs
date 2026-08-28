using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LensDistortionCorrection
{
    public sealed class LensDistortionCorrectionEditorTool
    {
        private readonly ImageProcessingContext _image;

        public LensDistortionCorrectionEditorTool(ImageProcessingContext image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public async Task ExecuteAsync()
        {
            LensDistortionCorrectionParametersWindow parametersWindow = new(_image.AlgorithmRuntime.Catalog)
            {
                Owner = Application.Current.GetActiveWindow(),
            };
            if (parametersWindow.ShowDialog() != true) return;
            LensDistortionCorrectionParameters parameters = parametersWindow.Parameters;
            AlgorithmInvocation baseInvocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, parameters);
            AlgorithmInvocation invocation = new()
            {
                InvocationId = baseInvocation.InvocationId,
                AlgorithmId = baseInvocation.AlgorithmId,
                ParameterSchemaVersion = baseInvocation.ParameterSchemaVersion,
                Parameters = baseInvocation.Parameters,
                PresetId = parametersWindow.PresetId,
            };
            AlgorithmResult result;
            try
            {
                result = await ImageAlgorithmApplier.ApplyAsync(_image, invocation);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "镜头畸变校正", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                MessageBox.Show(string.Join("; ", result.Failures), "镜头畸变校正", MessageBoxButton.OK, MessageBoxImage.Warning);
                result.Dispose();
                return;
            }
            try
            {
                LensDistortionCorrectionResultWindow window = new(result) { Owner = Application.Current.GetActiveWindow() };
                window.ShowDialog();
            }
            catch (Exception exception)
            {
                result.Dispose();
                MessageBox.Show(exception.Message, "镜头畸变校正", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
