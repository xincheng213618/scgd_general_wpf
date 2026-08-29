using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;

namespace ColorVision.UI.Tests;

/// <summary>
/// Provider-level implementation harness. These providers remain testable without opting them
/// back into the default product runtime or its menus.
/// </summary>
internal static class ExperimentalAlgorithmTestRuntime
{
    public static AlgorithmRuntime Runtime { get; } = new(
        StandardAlgorithmCatalog.Create(),
        new IImageAlgorithmProvider[]
        {
            new BlobAnalysisAlgorithmProvider(),
            new ContourAnalysisAlgorithmProvider(),
            new SubpixelEdgeAlgorithmProvider(),
            new LineFitAlgorithmProvider(),
            new CircleFitAlgorithmProvider(),
            new FrequencySpectrumAlgorithmProvider(),
            new MoireAnalysisAlgorithmProvider(),
        },
        new AlgorithmExecutionScheduler());
}
