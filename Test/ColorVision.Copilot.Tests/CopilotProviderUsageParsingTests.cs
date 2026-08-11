using System;
using System.Text.Json;
using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderUsageParsingTests
{
    [Fact]
    public void OpenAiUsageWithoutTotalSaturatesDerivedTokenCount()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "usage": {
                "prompt_tokens": 2147483647,
                "completion_tokens": 2147483647
              }
            }
            """);

        var usage = CopilotChatService.ExtractOpenAiUsage(document.RootElement);

        Assert.Equal(int.MaxValue, usage.InputTokens);
        Assert.Equal(int.MaxValue, usage.OutputTokens);
        Assert.Equal(int.MaxValue, usage.EffectiveTotalTokens);
    }

    [Fact]
    public void AnthropicUsageSaturatesAdditionalInputFields()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "usage": {
                "input_tokens": 2147483647,
                "output_tokens": 1,
                "cache_creation_input_tokens": 2147483647,
                "cache_read_input_tokens": 2147483647
              }
            }
            """);

        var usage = CopilotChatService.ExtractAnthropicUsage(document.RootElement);

        Assert.Equal(int.MaxValue, usage.InputTokens);
        Assert.Equal(1, usage.OutputTokens);
        Assert.Equal(int.MaxValue, usage.EffectiveTotalTokens);
        Assert.Equal(int.MaxValue, usage.EffectiveCachedInputTokens);
    }
}
