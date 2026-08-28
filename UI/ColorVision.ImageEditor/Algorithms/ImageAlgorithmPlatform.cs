using ColorVision.Algorithms;
using System;

namespace ColorVision.ImageEditor.Algorithms
{
    public static class ImageAlgorithmPlatform
    {
        private static readonly Lazy<AlgorithmRuntime> RuntimeInstance = new(CreateRuntime);

        public static AlgorithmRuntime Runtime => RuntimeInstance.Value;

        public static AlgorithmCatalog Catalog => (AlgorithmCatalog)Runtime.Catalog;

        public static AlgorithmRunner Runner => Runtime.Runner;

        public static AlgorithmInvocationCoordinator InvocationCoordinator => Runtime.InvocationCoordinator;

        private static AlgorithmRuntime CreateRuntime()
            => new(
                StandardAlgorithmCatalog.Create(),
                CreateDefaultProviders(),
                new AlgorithmExecutionScheduler(),
                new IAlgorithmParameterMigrator[]
                {
                    new ImageComparisonParametersV1ToV2Migrator(),
                    new ThresholdParametersV1ToV2Migrator(),
                    new DenoiseParametersV1ToV2Migrator(),
                });

        private static IImageAlgorithmProvider[] CreateDefaultProviders() =>
        [
            new ImagingCorrectionAlgorithmProvider(),
            Experimental(
                new MoireAnalysisAlgorithmProvider(),
                "moire_analysis_release_validation_pending",
                "Moire analysis is retained for evaluation but is not released while spectral correctness and full-frame resource validation remain open."),
            Experimental(
                new FrequencySpectrumAlgorithmProvider(),
                "frequency_spectrum_release_validation_pending",
                "Frequency-spectrum analysis is retained for evaluation but is not released while quantitative and full-frame resource validation remain open."),
            new LensDistortionCorrectionAlgorithmProvider(),
            new ImageRegistrationAlgorithmProvider(),
            new GeometricTransformAlgorithmProvider(),
            Experimental(
                new CircleFitAlgorithmProvider(),
                "circle_fit_release_validation_pending",
                "Circle fitting is retained for evaluation but is not released while numerical and consensus-budget validation remain open."),
            Experimental(
                new LineFitAlgorithmProvider(),
                "line_fit_release_validation_pending",
                "Line fitting is retained for evaluation but is not released while numerical and iteration-budget validation remain open."),
            Experimental(
                new SubpixelEdgeAlgorithmProvider(),
                "subpixel_edge_release_validation_pending",
                "Subpixel edge measurement is retained for evaluation but is not released while accuracy and large-caliper validation remain open."),
            Experimental(
                new ContourAnalysisAlgorithmProvider(),
                "contour_release_validation_pending",
                "Contour analysis is retained for evaluation but is not released while worst-case point and native-memory bounds remain open."),
            Experimental(
                new BlobAnalysisAlgorithmProvider(),
                "blob_release_validation_pending",
                "Blob analysis is retained for evaluation but is not released while worst-case component and artifact resource bounds remain open."),
            new RoiStatisticsAlgorithmProvider(),
            new ImageProfileAlgorithmProvider(),
            new ImageComparisonAlgorithmProvider(),
            new OpenCvAlgorithmProvider(),
            new NativeCompatibilityAlgorithmProvider(),
        ];

        private static IImageAlgorithmProvider Experimental(
            IImageAlgorithmProvider provider,
            string reasonCode,
            string reason)
            => new ExperimentalAlgorithmProviderGate(provider, reasonCode, reason);
    }
}
