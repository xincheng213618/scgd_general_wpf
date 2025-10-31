namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Represents the context for a batch operation, including configuration settings and the associated batch of
    /// measurements.
    /// </summary>
    public class IBatchContext
    {
        /// <summary>
        /// Gets or sets the batch configuration settings used for processing operations.
        /// </summary>
        public BatchConfig Config { get; set; } = BatchConfig.Instance;

        /// <summary>
        /// Gets or sets the batch of measurements associated with the current operation.
        /// </summary>
        public MeasureBatchModel Batch { get; set; }

    }
}
