namespace ProjectARVRPro.Process
{
    internal class ProcessMetaPersist
    {
        public string Name { get; set; }
        public string FlowTemplate { get; set; }
        public string ProcessTypeFullName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string ConfigJson { get; set; }
        public string RecipeConfigTypeName { get; set; }
        public string FixConfigTypeName { get; set; }
    }
}
