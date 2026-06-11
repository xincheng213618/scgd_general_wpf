#pragma warning disable CA1852
namespace ProjectARVRPro.Process
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

    internal class ProcessManagerConfigPersist
    {
        public int Version { get; set; } = 1;
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public ProcessGroupsRoot ProcessGroups { get; set; } = new();
        public RecipeConfig RecipeConfig { get; set; } = new();
    }
}
