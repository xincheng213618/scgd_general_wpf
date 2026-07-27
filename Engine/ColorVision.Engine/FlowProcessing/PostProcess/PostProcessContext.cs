namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    /// <summary>
    /// Represents the context for processing a completed flow batch.
    /// </summary>
    public class PostProcessContext
    {
        /// <summary>
        /// Gets or sets the name of the flow associated with this instance.
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// Gets or sets the post-process configuration settings.
        /// </summary>
        public PostProcessConfig Config { get; set; } = PostProcessConfig.Instance;

        /// <summary>
        /// Gets or sets the batch of measurements associated with the current operation.
        /// </summary>
        public MeasureBatchModel Batch { get; set; }
    }
}
