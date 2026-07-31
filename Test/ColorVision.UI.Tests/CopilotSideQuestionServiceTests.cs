using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotSideQuestionServiceTests
{
    [Fact]
    public async Task AskUsesConversationOnlyAndKeepsSideQuestionEphemeral()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CopilotSideQuestionService(new CopilotChatService(httpClient));
        var profile = CreateProfile();
        profile.UseSystemPromptOverride("Original profile instruction.");
        var history = new CopilotConversationHistorySnapshot(
            [
                new CopilotRequestMessage("user", "Inspect the current module."),
                new CopilotRequestMessage("assistant", "The configuration file is CopilotConfig.cs."),
            ],
            [
                new CopilotRequestMessage("user", "Inspect the current module."),
                new CopilotRequestMessage("assistant", "The configuration file is CopilotConfig.cs."),
            ]);

        var result = await service.AskAsync(
            profile,
            history,
            new CopilotConversationHistoryLimits(16, 64_000, 16_000),
            "What was the configuration file called?",
            CancellationToken.None);

        Assert.Equal("It was CopilotConfig.cs.", result.Answer);
        Assert.Equal(new CopilotTokenUsage(12, 4, 16), result.Usage);
        Assert.False(result.IsIncomplete);
        Assert.Equal(2, history.ModelMessages.Count);
        Assert.Equal(1, handler.RequestCount);

        using var payload = JsonDocument.Parse(handler.LastPayload);
        var root = payload.RootElement;
        Assert.Equal(CopilotSideQuestionService.MaximumOutputTokens, root.GetProperty("max_tokens").GetInt32());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.False(root.TryGetProperty("tools", out _));

        var messages = root.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        var systemPrompt = messages[0].GetProperty("content").GetString();
        Assert.Contains("Original profile instruction.", systemPrompt);
        Assert.Contains("ephemeral side question", systemPrompt);
        Assert.Contains("Do not use or claim to use tools", systemPrompt);
        Assert.Equal("Inspect the current module.", messages[1].GetProperty("content").GetString());
        Assert.Equal("The configuration file is CopilotConfig.cs.", messages[2].GetProperty("content").GetString());
        Assert.Equal("What was the configuration file called?", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task EmptyQuestionFailsBeforeCallingProvider()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CopilotSideQuestionService(new CopilotChatService(httpClient));

        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(
            CreateProfile(),
            CopilotConversationHistorySnapshot.Empty,
            new CopilotConversationHistoryLimits(8, 32_000, 8_000),
            "   ",
            CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("/side what changed?", "/side")]
    [InlineData("/btw what changed?", "/btw")]
    public void SideQuestionAliasesAcceptQuestionsAndRemainAvailableDuringAgentRuns(
        string input,
        string expectedName)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(expectedName, invocation.Command.Name);
        Assert.Equal(CopilotLocalCommandKind.SideQuestion, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal("what changed?", invocation.Arguments);
    }

    [Fact]
    public void SideAliasAppearsInComposerSuggestions()
    {
        var suggestion = Assert.Single(CopilotLocalCommandCatalog.Suggest("/sid"));

        Assert.Equal("/side", suggestion.Name);
        Assert.Equal("/side [问题]", suggestion.Usage);
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
        public int RequestCount { get; private set; }

        public string LastPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            const string response =
                """
                {
                  "choices": [
                    {
                      "message": {
                        "role": "assistant",
                        "content": "It was CopilotConfig.cs."
                      },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 12,
                    "completion_tokens": 4,
                    "total_tokens": 16
                  }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
