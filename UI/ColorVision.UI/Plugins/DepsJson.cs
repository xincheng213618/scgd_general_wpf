using Newtonsoft.Json;

namespace ColorVision.UI.Plugins
{
    public class DepsJson
    {
        [JsonProperty("runtimeTarget")]
        public RuntimeTarget RuntimeTarget { get; set; }

        [JsonProperty("targets")]
        public Dictionary<string, Dictionary<string, DepsTargetEntry>> Targets { get; set; }
    }

    public class RuntimeTarget
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class DepsTargetEntry
    {
        [JsonProperty("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }

        [JsonProperty("runtime")]
        public Dictionary<string, RuntimeAssemblyInfo> Runtime { get; set; }
    }

    public class RuntimeAssemblyInfo
    {
        [JsonProperty("assemblyVersion")]
        public string AssemblyVersion { get; set; }

        [JsonProperty("fileVersion")]
        public string FileVersion { get; set; }
    }
}