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

        [JsonProperty("resources")]
        public Dictionary<string, ResourceInfo> Resources { get; set; }

        [JsonProperty("runtimeTargets")]
        public Dictionary<string, RuntimeTargetInfo> RuntimeTargets { get; set; }
    }

    public class RuntimeAssemblyInfo
    {
        [JsonProperty("assemblyVersion")]
        public string AssemblyVersion { get; set; }

        [JsonProperty("fileVersion")]
        public string FileVersion { get; set; }
    }

    public class ResourceInfo
    {
        [JsonProperty("locale")]
        public string Locale { get; set; }
    }

    public class RuntimeTargetInfo
    {
        [JsonProperty("rid")]
        public string Rid { get; set; }

        [JsonProperty("assetType")]
        public string AssetType { get; set; }

        [JsonProperty("fileVersion")]
        public string FileVersion { get; set; }

        [JsonProperty("assemblyVersion")]
        public string AssemblyVersion { get; set; }
    }
}