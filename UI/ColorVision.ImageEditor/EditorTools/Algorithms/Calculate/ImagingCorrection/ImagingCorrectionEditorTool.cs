using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImagingCorrection
{
    /// <summary>ImageView adapter for the named-reference imaging-correction pipeline.</summary>
    public sealed class ImagingCorrectionEditorTool
    {
        private readonly ImageProcessingContext _image;

        public ImagingCorrectionEditorTool(ImageProcessingContext image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public async Task ExecuteAsync()
        {
            ImagingCorrectionParametersWindow parametersWindow = new(_image.AlgorithmRuntime.Catalog) { Owner = Application.Current.GetActiveWindow() };
            if (parametersWindow.ShowDialog() != true) return;
            IReadOnlyList<AlgorithmInput> references;
            try
            {
                references = await Task.Run(() => AlgorithmReferenceImageLoader.LoadEnabledReferences(parametersWindow.Parameters));
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "成像校正", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ImagingCorrectionParameters parameters = parametersWindow.Parameters;
            AlgorithmInvocation baseInvocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, parameters);
            AlgorithmInvocation invocation = new()
            {
                InvocationId = baseInvocation.InvocationId,
                AlgorithmId = baseInvocation.AlgorithmId,
                ParameterSchemaVersion = baseInvocation.ParameterSchemaVersion,
                Parameters = baseInvocation.Parameters,
                PresetId = parametersWindow.PresetId,
                Inputs = new[] { new AlgorithmInputReference("source") }
                    .Concat(references.Select(value => new AlgorithmInputReference(value.Name, value.SourceUri, value.SourceRevision, value.Checksum)))
                    .ToArray(),
            };
            AlgorithmResult result;
            try
            {
                result = await ImageAlgorithmApplier.ApplyAsync(_image, invocation, references);
            }
            catch (Exception exception)
            {
                foreach (AlgorithmInput input in references.Where(value => value.Ownership == AlgorithmInputOwnership.Transferred && !value.Image.IsDisposed)) input.Image.Dispose();
                MessageBox.Show(exception.Message, "成像校正", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                MessageBox.Show(string.Join(Environment.NewLine, result.Failures.Select(value => $"[{value.Code}] {value.Message}")), "成像校正", MessageBoxButton.OK, MessageBoxImage.Warning);
                result.Dispose();
                return;
            }
            try
            {
                ImagingCorrectionResultWindow window = new(result) { Owner = Application.Current.GetActiveWindow() };
                window.ShowDialog();
            }
            catch (Exception exception)
            {
                result.Dispose();
                MessageBox.Show(exception.Message, "成像校正", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
