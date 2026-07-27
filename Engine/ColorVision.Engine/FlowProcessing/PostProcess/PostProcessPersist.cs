#pragma warning disable CA1852
namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    internal class PostProcessPersist
    {
        public string Name { get; set; }
        public string TemplateName { get; set; }
        public string ProcessTypeFullName { get; set; }

        /// <summary>
        /// JSON representation of the post-processor configuration.
        /// </summary>
        public string ConfigJson { get; set; }

        /// <summary>
        /// User-defined tag/label for this post-processor.
        /// </summary>
        public string Tag { get; set; }
    }
}
