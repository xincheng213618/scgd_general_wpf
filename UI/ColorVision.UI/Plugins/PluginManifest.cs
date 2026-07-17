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

        [JsonProperty("requires")]
        public string Requires { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("dllpath")]
        public string DllName { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("entry_point")]
        public string EntryPoint { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("copilot_agents")]
        public List<CopilotSubagentRoleManifest> CopilotAgents { get; set; } = new();
    }

    public sealed class CopilotSubagentRoleManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("tool")]
        public string ToolName { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("instructions")]
        public string Instructions { get; set; } = string.Empty;

        [JsonProperty("scope")]
        public string Scope { get; set; } = string.Empty;

        [JsonProperty("capabilities")]
        public List<string> Capabilities { get; set; } = new();

        [JsonProperty("child_mode")]
        public string ChildMode { get; set; } = string.Empty;

        [JsonProperty("parent_modes")]
        public List<string> ParentModes { get; set; } = new();

        [JsonProperty("maximum_tool_calls")]
        public int MaximumToolCalls { get; set; }

        [JsonProperty("maximum_agent_passes")]
        public int MaximumAgentPasses { get; set; }

        [JsonProperty("maximum_duration_seconds")]
        public int MaximumDurationSeconds { get; set; }

        [JsonProperty("maximum_answer_characters")]
        public int MaximumAnswerCharacters { get; set; }
    }
}
