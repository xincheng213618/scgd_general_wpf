using System;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    /// <summary>
    /// Represents the context for processing a completed flow batch.
    /// </summary>
    public class PostProcessContext
    {
        public PostProcessContext()
            : this(PostProcessConfig.Instance)
        {
        }

        public PostProcessContext(PostProcessConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Gets or sets the name of the flow associated with this instance.
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// Gets or sets the post-process configuration settings.
        /// </summary>
        public PostProcessConfig Config { get; set; }

        /// <summary>
        /// Gets or sets the batch of measurements associated with the current operation.
        /// </summary>
        public MeasureBatchModel Batch { get; set; }
    }
}
