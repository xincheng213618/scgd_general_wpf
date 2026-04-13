namespace ProjectLUX.Process
{
    internal class ProcessGroupPersist
    {
        public string Name { get; set; }
        public List<ProcessMetaPersist> Metas { get; set; } = new();
    }

    internal class ProcessGroupsRoot
    {
        public int Version { get; set; } = 1;
        public int ActiveGroupIndex { get; set; }
        public List<ProcessGroupPersist> Groups { get; set; } = new();
    }
}
