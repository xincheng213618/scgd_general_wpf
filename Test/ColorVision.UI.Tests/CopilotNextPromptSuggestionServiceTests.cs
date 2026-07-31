using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotNextPromptSuggestionServiceTests
{
    [Fact]
    public async Task SuggestUsesOnlyVisibleHistoryWithNoToolsAndBoundedOutput()
    {
        var handler = new CapturingHandler("继续检查剩余改动");
        using var httpClient = new HttpClient(handler);
        var service = new CopilotNextPromptSuggestionService(new CopilotChatService(httpClient));
        var profile = CreateProfile();
        profile.UseSystemPromptOverride("Original instruction.");
        var history = new CopilotConversationHistorySnapshot(
            [
                new CopilotRequestMessage("user", "hidden model request"),
                new CopilotRequestMessage("assistant", "hidden model evidence"),
            ],
            [
                new CopilotRequestMessage("user", "检查 Copilot 模块"),
                new CopilotRequestMessage("assistant", "已完成第一轮检查。"),
            ]);

        var result = await service.SuggestAsync(
            profile,
            history,
            new CopilotConversationHistoryLimits(64, 128_000, 32_000),
            CancellationToken.None);

        Assert.Equal("继续检查剩余改动", result.Suggestion);
        Assert.Equal(new CopilotTokenUsage(10, 3, 13), result.Usage);
        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.Equal(CopilotNextPromptSuggestionService.MaximumOutputTokens, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("tools", out _));
        var messages = root.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Contains("Original instruction.", messages[0].GetProperty("content").GetString());
        Assert.Contains("predict one optional next user message", messages[0].GetProperty("content").GetString());
        Assert.Contains("what the user would actually type", messages[0].GetProperty("content").GetString());
        Assert.Equal("检查 Copilot 模块", messages[1].GetProperty("content").GetString());
        Assert.Equal("已完成第一轮检查。", messages[2].GetProperty("content").GetString());
        Assert.Contains("Predict what the user is most likely", messages[3].GetProperty("content").GetString());
        Assert.DoesNotContain("hidden model", handler.LastPayload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NONE", "")]
    [InlineData(" suggestion: 继续运行测试 ", "继续运行测试")]
    [InlineData("“先提交当前改动”", "先提交当前改动")]
    [InlineData("继续", "继续")]
    [InlineData("/review", "/review")]
    public void NormalizationProducesOneComposerSafePrompt(string value, string expected)
    {
        Assert.Equal(expected, CopilotNextPromptSuggestionService.NormalizeSuggestion(value));
    }

    [Fact]
    public void NormalizationRejectsUnsafeOrLowQualitySuggestions()
    {
        string[] rejected =
        [
            "- 检查最终差异",
            "第一行\n第二行",
            "Run the tests. Then commit the changes.",
            "I'll run the tests",
            "我来运行测试",
            "looks good",
            "谢谢",
            "what should we do next?",
            "refactor",
            "NONE.",
            "User: run the tests",
            "建议：继续运行测试",
            new string('x', CopilotNextPromptSuggestionService.MaximumSuggestionCharacters + 1),
            string.Join(' ', Enumerable.Repeat("word", CopilotNextPromptSuggestionService.MaximumSuggestionWords + 1)),
        ];

        foreach (string value in rejected)
            Assert.Empty(CopilotNextPromptSuggestionService.NormalizeSuggestion(value));
    }

    [Fact]
    public void NormalizationRejectsRepeatedLongUserRequestsButAllowsShortCommands()
    {
        CopilotRequestMessage[] history =
        [
            new("user", "Fix  the flaky\nauth test."),
            new("assistant", "Fixed it."),
            new("user", "继续检查剩余改动"),
            new("assistant", "检查完成。"),
        ];

        Assert.Empty(CopilotNextPromptSuggestionService.NormalizeSuggestion("fix the flaky auth test!", history));
        Assert.Empty(CopilotNextPromptSuggestionService.NormalizeSuggestion("继续检查剩余改动。", history));
        Assert.Equal("run the tests", CopilotNextPromptSuggestionService.NormalizeSuggestion("run the tests", history));
        Assert.Equal("继续", CopilotNextPromptSuggestionService.NormalizeSuggestion("继续", history));
    }

    [Fact]
    public async Task SuggestRejectsRepeatedUserRequestAndRetainsUsage()
    {
        var handler = new CapturingHandler("fix the flaky auth test");
        using var httpClient = new HttpClient(handler);
        var service = new CopilotNextPromptSuggestionService(new CopilotChatService(httpClient));
        var history = new CopilotConversationHistorySnapshot(
            [],
            [
                new CopilotRequestMessage("user", "Fix the flaky auth test."),
                new CopilotRequestMessage("assistant", "Fixed it."),
            ]);

        var result = await service.SuggestAsync(
            CreateProfile(),
            history,
            new CopilotConversationHistoryLimits(64, 128_000, 32_000),
            CancellationToken.None);

        Assert.Empty(result.Suggestion);
        Assert.Equal(new CopilotTokenUsage(10, 3, 13), result.Usage);
    }

    [Fact]
    public async Task SuggestCapsEachVisibleMessageForLowNoisePrediction()
    {
        var handler = new CapturingHandler("continue");
        using var httpClient = new HttpClient(handler);
        var service = new CopilotNextPromptSuggestionService(new CopilotChatService(httpClient));
        var history = new CopilotConversationHistorySnapshot(
            [],
            [
                new CopilotRequestMessage("user", new string('x', 4_000)),
                new CopilotRequestMessage("assistant", "done"),
            ]);

        var result = await service.SuggestAsync(
            CreateProfile(),
            history,
            new CopilotConversationHistoryLimits(64, 128_000, 32_000),
            CancellationToken.None);

        Assert.Equal("continue", result.Suggestion);
        using var payload = JsonDocument.Parse(handler.LastPayload);
        string userContent = payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        Assert.Equal(CopilotNextPromptSuggestionService.MaximumMessageCharacters, userContent.Length);
    }

    [Fact]
    public void RequestProfileDisablesReasoningWithoutMutatingConfiguredProfile()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.DeepSeek;
        profile.ReasoningMode = CopilotReasoningMode.Max;

        var requestProfile = CopilotNextPromptSuggestionService.CreateRequestProfile(profile);

        Assert.Equal(CopilotReasoningMode.Max, profile.ReasoningMode);
        Assert.Equal(CopilotReasoningMode.Disabled, requestProfile.ReasoningMode);
        Assert.Equal(CopilotNextPromptSuggestionService.MaximumOutputTokens, requestProfile.MaxTokens);
    }

    [Fact]
    public void PredictionPolicyRejectsInterruptedAndCancelledAgentTurns()
    {
        var completedRun = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        completedRun.SetAgentStopReason(CopilotAgentStopReason.Completed);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "已完成。")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        Assert.True(CopilotChatViewModel.CanPredictNextPrompt(completedRun, assistant));

        assistant.MarkResponseInterrupted("provider failed");
        Assert.False(CopilotChatViewModel.CanPredictNextPrompt(completedRun, assistant));

        var cancelledRun = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        cancelledRun.SetAgentStopReason(CopilotAgentStopReason.Cancelled);
        var completedAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "部分内容")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        Assert.False(CopilotChatViewModel.CanPredictNextPrompt(cancelledRun, completedAssistant));
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _content;

        public CapturingHandler(string content)
        {
            _content = content;
        }

        public string LastPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new { role = "assistant", content = _content },
                        finish_reason = "stop",
                    },
                },
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 3,
                    total_tokens = 13,
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
