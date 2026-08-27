using ColorVision.Algorithms;
using System;

namespace ColorVision.ImageEditor.Algorithms
{
    public static class ImageAlgorithmPlatform
    {
        private static readonly Lazy<AlgorithmCatalog> CatalogInstance = new(StandardAlgorithmCatalog.Create);
        private static readonly Lazy<AlgorithmExecutionScheduler> SchedulerInstance = new(() => new AlgorithmExecutionScheduler());
        private static readonly Lazy<AlgorithmRunner> RunnerInstance = new(() => new AlgorithmRunner(
            CatalogInstance.Value,
            new IImageAlgorithmProvider[] { new RoiStatisticsAlgorithmProvider(), new ImageProfileAlgorithmProvider(), new ImageComparisonAlgorithmProvider(), new OpenCvAlgorithmProvider(), new NativeCompatibilityAlgorithmProvider() },
            SchedulerInstance.Value,
            new IAlgorithmParameterMigrator[] { new ImageComparisonParametersV1ToV2Migrator() }));

        public static AlgorithmCatalog Catalog => CatalogInstance.Value;

        public static AlgorithmRunner Runner => RunnerInstance.Value;
    }
}
