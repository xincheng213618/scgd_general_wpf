namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Represents the context for a pre-processing operation before flow execution.
    /// </summary>
    public class IPreProcessContext
    {
        /// <summary>
        /// Gets or sets the name of the flow that will be executed.
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// Gets or sets the serial number for the flow execution.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the batch configuration settings.
        /// </summary>
        public BatchConfig Config { get; set; } = BatchConfig.Instance;
    }
}
