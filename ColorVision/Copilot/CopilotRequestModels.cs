using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotRequestMessage(string Role, string Content)
    {
        [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSteering { get; init; }
    }

    public sealed class CopilotProviderOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotProviderType Value { get; init; }
    }
}
