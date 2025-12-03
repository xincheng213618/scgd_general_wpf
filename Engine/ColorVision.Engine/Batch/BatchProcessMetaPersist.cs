namespace ColorVision.Engine.Batch
{
    internal class BatchProcessMetaPersist
    {
        public string Name { get; set; }
        public string TemplateName { get; set; }
        public string ProcessTypeFullName { get; set; }
        
        /// <summary>
        /// JSON representation of the batch process configuration.
        /// </summary>
        public string ConfigJson { get; set; }
        
        /// <summary>
        /// User-defined tag/label for this batch process.
        /// </summary>
        public string Tag { get; set; }
    }
}
