using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.BatchProcessing
{
    /// <summary>Compatibility view model over a catalog descriptor and one mutable parameter instance.</summary>
    public sealed class BatchImageAlgorithmDefinition
    {
        private readonly Func<Mat, CancellationToken, Mat>? _legacyApply;
        private readonly bool _preserveLegacyBatchBehavior;

        public BatchImageAlgorithmDefinition(string name, string suffix, object options, Func<Mat, Mat> apply)
        {
            Name = name;
            Suffix = suffix;
            Options = options;
            _legacyApply = (source, _) => apply(source);
        }

        internal BatchImageAlgorithmDefinition(
            AlgorithmDescriptor descriptor,
            IAlgorithmParameters parameters,
            bool preserveLegacyBatchBehavior = false)
        {
            Descriptor = descriptor;
            Name = descriptor.Name;
            Suffix = descriptor.OutputSuffix;
            Options = parameters;
            _preserveLegacyBatchBehavior = preserveLegacyBatchBehavior;
        }

        public string Name { get; }

        public string Suffix { get; }

        public object Options { get; }

        public AlgorithmDescriptor? Descriptor { get; }

        public bool IsFormatOnly => Descriptor == null && Options is NoBatchAlgorithmOptions;

        public Mat Apply(Mat source) => Apply(source, CancellationToken.None);

        public Mat Apply(Mat source, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            if (_legacyApply != null)
            {
                Mat legacyResult = _legacyApply(source, cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return legacyResult;
                }
                catch
                {
                    legacyResult.Dispose();
                    throw;
                }
            }

            AlgorithmDescriptor descriptor = Descriptor ?? throw new InvalidOperationException("The batch algorithm has no catalog descriptor.");
            IAlgorithmParameters parameters = CreateCompatibilityParameters((IAlgorithmParameters)Options);
            using Mat? compatibilityInput = _preserveLegacyBatchBehavior
                ? CreateLegacyCompatibilityInput(source, descriptor.Id, parameters, cancellationToken)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer inputImage = AlgorithmImageInterop.FromMat(compatibilityInput ?? source);
            AlgorithmInvocation invocation = new()
            {
                AlgorithmId = descriptor.Id,
                AlgorithmVersion = descriptor.Version,
                ParameterSchemaVersion = parameters.SchemaVersion,
                Parameters = AlgorithmJson.ToElement(parameters),
            };
            using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation,
                Inputs = new[]
                {
                    new AlgorithmInput
                    {
                        Name = "source",
                        Image = inputImage,
                        Ownership = AlgorithmInputOwnership.Transferred,
                    },
                },
                RequiredCapabilities = AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            }, cancellationToken).AsTask().GetAwaiter().GetResult();

            if (result.Status == AlgorithmResultStatus.Cancelled) throw new OperationCanceledException(cancellationToken);
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join("; ", result.Failures.Select(failure => $"{failure.Code}: {failure.Message}"));
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "Algorithm execution failed." : message);
            }

            AlgorithmImageArtifact image = result.GetArtifact<AlgorithmImageArtifact>()
                ?? throw new InvalidOperationException("The algorithm did not return a primary image artifact.");
            Mat output = AlgorithmImageInterop.ToMat(image.Image);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }

        private static Mat? CreateLegacyCompatibilityInput(
            Mat source,
            AlgorithmId algorithmId,
            IAlgorithmParameters parameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((algorithmId == StandardAlgorithmIds.PseudoColor && IsLegacyPseudoColorMode(parameters))
                || algorithmId == StandardAlgorithmIds.Canny)
            {
                using Mat gray = ConvertToGray(source);
                return ConvertTo8Bit(gray);
            }

            return algorithmId == StandardAlgorithmIds.HistogramEqualization
                ? ConvertTo8Bit(source)
                : null;
        }

        private static bool IsLegacyPseudoColorMode(IAlgorithmParameters parameters)
        {
            PseudoColorParameters value = (PseudoColorParameters)parameters;
            return value.UseNominalRange
                && value.Minimum == 0
                && value.Maximum == byte.MaxValue
                && value.Channel == -1
                && !value.AutoRange;
        }

        private static Mat ConvertToGray(Mat source)
        {
            if (source.Channels() == 1)
            {
                return new Mat(source, new Rect(0, 0, source.Cols, source.Rows));
            }

            Mat result = new();
            ColorConversionCodes conversion = source.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY;
            Cv2.CvtColor(source, result, conversion);
            return result;
        }

        private static Mat ConvertTo8Bit(Mat source)
        {
            if (source.Depth() == MatType.CV_8U)
            {
                return new Mat(source, new Rect(0, 0, source.Cols, source.Rows));
            }

            Mat result = new();
            if (source.Depth() == MatType.CV_16U)
            {
                Cv2.Normalize(source, result, 0, byte.MaxValue, NormTypes.MinMax, MatType.CV_8U);
                return result;
            }

            using Mat normalized = new();
            Cv2.Normalize(source, normalized, 0, byte.MaxValue, NormTypes.MinMax);
            normalized.ConvertTo(result, MatType.CV_8U);
            return result;
        }

        private static IAlgorithmParameters CreateCompatibilityParameters(IAlgorithmParameters parameters)
        {
            IAlgorithmParameters clone = AlgorithmJson.ToElement(parameters)
                .Deserialize(parameters.GetType(), AlgorithmJson.Options) as IAlgorithmParameters
                ?? throw new InvalidOperationException($"Could not copy parameters of type '{parameters.GetType().Name}'.");
            switch (clone)
            {
                case GaussianBlurParameters value:
                    value.KernelSize = NormalizeOdd(value.KernelSize, 1);
                    break;
                case MedianBlurParameters value:
                    value.KernelSize = NormalizeOdd(value.KernelSize, 3);
                    break;
                case MorphologyParameters value:
                    value.KernelSize = NormalizeOdd(value.KernelSize, 1);
                    break;
                case DenoiseParameters value:
                    value.KernelSize = NormalizeOdd(value.KernelSize, 1);
                    break;
            }
            return clone;
        }

        private static int NormalizeOdd(int value, int minimum)
        {
            value = Math.Max(minimum, value);
            return value % 2 == 0 ? value + 1 : value;
        }
    }

    public static class BatchImageAlgorithms
    {
        private static readonly AlgorithmId[] CompatibilityOrder =
        {
            StandardAlgorithmIds.Invert,
            StandardAlgorithmIds.PseudoColor,
            StandardAlgorithmIds.AutoLevels,
            StandardAlgorithmIds.WhiteBalance,
            StandardAlgorithmIds.BasicAdjustment,
            StandardAlgorithmIds.Threshold,
            StandardAlgorithmIds.Sharpen,
            StandardAlgorithmIds.GaussianBlur,
            StandardAlgorithmIds.MedianBlur,
            StandardAlgorithmIds.Canny,
            StandardAlgorithmIds.HistogramEqualization,
            StandardAlgorithmIds.Morphology,
            StandardAlgorithmIds.Denoise,
        };

        public static BatchImageAlgorithmDefinition CreateFormatOnly()
            => new("仅转换格式", string.Empty, new NoBatchAlgorithmOptions(), source => source.Clone());

        public static IReadOnlyList<BatchImageAlgorithmDefinition> CreateAll()
        {
            List<BatchImageAlgorithmDefinition> algorithms = new() { CreateFormatOnly() };
            foreach (AlgorithmId id in CompatibilityOrder)
            {
                if (!ImageAlgorithmPlatform.Catalog.TryResolve(id, out AlgorithmDescriptor? descriptor)
                    || descriptor == null
                    || (descriptor.Capabilities & AlgorithmHostCapabilities.Batch) == 0)
                {
                    continue;
                }
                algorithms.Add(new BatchImageAlgorithmDefinition(
                    descriptor,
                    CreateDefaultParameters(descriptor),
                    preserveLegacyBatchBehavior: true));
            }
            return algorithms;
        }

        public static bool TryCreateForCopilot(
            string idOrAlias,
            JsonElement? parameters,
            out BatchImageAlgorithmDefinition? algorithm,
            out string error)
        {
            algorithm = null;
            if (!ImageAlgorithmPlatform.Catalog.TryResolveAlias(idOrAlias, out AlgorithmDescriptor? descriptor) || descriptor == null)
            {
                error = $"Algorithm '{idOrAlias}' is not registered.";
                return false;
            }

            const AlgorithmHostCapabilities required = AlgorithmHostCapabilities.Copilot
                | AlgorithmHostCapabilities.Batch
                | AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Deterministic;
            if (!StandardAlgorithmCatalog.IsExplicitlyAllowedForCopilot(descriptor.Id)
                || (descriptor.Capabilities & required) != required)
            {
                error = $"Algorithm '{descriptor.Id}' is not on the explicit Copilot local/headless/deterministic whitelist.";
                return false;
            }

            try
            {
                JsonElement json = parameters is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null }
                    ? parameters.Value
                    : descriptor.ParameterSchema.Defaults;
                if (json.ValueKind != JsonValueKind.Object)
                {
                    error = "Algorithm parameters must be a JSON object.";
                    return false;
                }
                HashSet<string> allowedFields = descriptor.ParameterSchema.Fields
                    .Select(field => field.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                string? unknownField = json.EnumerateObject()
                    .Select(property => property.Name)
                    .FirstOrDefault(name => !allowedFields.Contains(name));
                if (unknownField != null)
                {
                    error = $"Parameter '{unknownField}' is not defined by '{descriptor.Id}' schema {descriptor.ParameterSchema.Version}.";
                    return false;
                }
                if (json.Deserialize(descriptor.ParameterType, AlgorithmJson.Options) is not IAlgorithmParameters parsed)
                {
                    error = $"Could not deserialize parameters for '{descriptor.Id}'.";
                    return false;
                }
                AlgorithmValidationResult validation = parsed.Validate();
                if (!validation.IsValid)
                {
                    error = string.Join("; ", validation.Issues.Select(issue => $"{issue.Path}: {issue.Message}"));
                    return false;
                }
                algorithm = new BatchImageAlgorithmDefinition(descriptor, parsed);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                error = $"Invalid parameters for '{descriptor.Id}': {exception.Message}";
                return false;
            }
        }

        private static IAlgorithmParameters CreateDefaultParameters(AlgorithmDescriptor descriptor)
        {
            IAlgorithmParameters parameters = descriptor.ParameterSchema.Defaults
                .Deserialize(descriptor.ParameterType, AlgorithmJson.Options) as IAlgorithmParameters
                ?? throw new InvalidOperationException($"Could not create default parameters for '{descriptor.Id}'.");
            if (parameters is CannyParameters canny)
            {
                canny.LowThreshold = 100;
                canny.HighThreshold = 200;
            }
            return parameters;
        }
    }

    internal sealed class NoBatchAlgorithmOptions
    {
    }
}
