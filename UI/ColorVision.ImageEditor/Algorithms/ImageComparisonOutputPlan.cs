using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms
{
    [Flags]
    public enum ImageComparisonArtifactOutputs
    {
        None = 0,
        AbsoluteDifference = 1 << 0,
        SignedDifference = 1 << 1,
        AbsoluteVisualization = 1 << 2,
        SignedVisualization = 1 << 3,
        Heatmap = 1 << 4,
        Exact = AbsoluteDifference | SignedDifference,
        InteractiveVisualizations = AbsoluteVisualization | SignedVisualization | Heatmap,
        All = Exact | InteractiveVisualizations,
    }

    /// <summary>
    /// Serializable execution hint for selecting retained comparison image artifacts.
    /// Metrics, tables, geometry and diagnostics are always produced.
    /// </summary>
    public static class ImageComparisonOutputPlan
    {
        public const string MetadataKey = "colorvision.image-comparison.requested-artifacts";

        private static readonly (ImageComparisonArtifactOutputs Output, string Name)[] OrderedOutputs =
        [
            (ImageComparisonArtifactOutputs.AbsoluteDifference, "absolute-difference"),
            (ImageComparisonArtifactOutputs.SignedDifference, "signed-difference"),
            (ImageComparisonArtifactOutputs.AbsoluteVisualization, "absolute-difference-visualization"),
            (ImageComparisonArtifactOutputs.SignedVisualization, "signed-difference-visualization"),
            (ImageComparisonArtifactOutputs.Heatmap, "difference-heatmap"),
        ];

        public static IReadOnlyDictionary<string, string> CreateMetadata(
            ImageComparisonArtifactOutputs outputs,
            IReadOnlyDictionary<string, string>? existing = null)
        {
            ValidateFlags(outputs);
            Dictionary<string, string> metadata = existing == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : existing.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            metadata[MetadataKey] = outputs == ImageComparisonArtifactOutputs.None
                ? "metrics-only"
                : string.Join(',', OrderedOutputs.Where(item => outputs.HasFlag(item.Output)).Select(item => item.Name));
            return metadata;
        }

        internal static bool TryResolve(
            AlgorithmInvocation invocation,
            out ImageComparisonArtifactOutputs outputs,
            out string? reason)
        {
            outputs = ImageComparisonArtifactOutputs.All;
            reason = null;
            if (invocation.Metadata == null || !invocation.Metadata.TryGetValue(MetadataKey, out string? value)) return true;
            if (string.Equals(value, "metrics-only", StringComparison.OrdinalIgnoreCase))
            {
                outputs = ImageComparisonArtifactOutputs.None;
                return true;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                reason = "The comparison requested-artifacts metadata value cannot be empty.";
                return false;
            }

            ImageComparisonArtifactOutputs parsed = ImageComparisonArtifactOutputs.None;
            foreach (string name in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                (ImageComparisonArtifactOutputs Output, string Name) match = OrderedOutputs.FirstOrDefault(
                    item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (match.Output == ImageComparisonArtifactOutputs.None)
                {
                    reason = $"Unknown comparison image artifact '{name}'.";
                    return false;
                }
                parsed |= match.Output;
            }
            if (parsed == ImageComparisonArtifactOutputs.None)
            {
                reason = "The comparison requested-artifacts metadata did not name an output.";
                return false;
            }
            outputs = parsed;
            return true;
        }

        internal static string Describe(ImageComparisonArtifactOutputs outputs)
        {
            ValidateFlags(outputs);
            return outputs == ImageComparisonArtifactOutputs.None
                ? "metrics-only"
                : string.Join(',', OrderedOutputs.Where(item => outputs.HasFlag(item.Output)).Select(item => item.Name));
        }

        private static void ValidateFlags(ImageComparisonArtifactOutputs outputs)
        {
            if ((outputs & ~ImageComparisonArtifactOutputs.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(outputs));
        }
    }
}
