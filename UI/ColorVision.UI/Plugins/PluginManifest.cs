using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.UI.Plugins
{
    public class PluginManifest : ViewModelBase
    {
        [JsonProperty("id")]
        public string Id { get; set; } // 新增，插件唯一ID

        [JsonProperty("manifest_version")]
        public int ManifestVersion { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("dllpath")]
        public string DllName { get; set; }

        [JsonProperty("requires")]
        public Version Requires { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("entry_point")]
        public string EntryPoint { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }
}