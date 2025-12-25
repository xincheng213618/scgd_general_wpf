namespace ColorVision.Engine.Batch
{
    internal class PreProcessMetaPersist
    {
        public string Name { get; set; }
        public string TemplateName { get; set; }
        public string ProcessTypeFullName { get; set; }
        
        /// <summary>
        /// JSON representation of the pre-processor configuration.
        /// </summary>
        public string ConfigJson { get; set; }
        
        /// <summary>
        /// User-defined tag/label for this pre-processor.
        /// </summary>
        public string Tag { get; set; }
        
        /// <summary>
        /// Whether this pre-processor is enabled for execution.
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}
