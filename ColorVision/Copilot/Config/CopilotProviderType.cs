using System.ComponentModel;

namespace ColorVision.Copilot
{
    public enum CopilotProviderType
    {
        [Description("OpenAI Compatible")]
        OpenAICompatible,
        [Description("Anthropic Compatible")]
        AnthropicCompatible,
    }
}