using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotSavedTemplateContextTests
{
    [Fact]
    public void SnapshotIsBoundedRedactedAndReadOnly()
    {
        var result = CopilotSavedTemplateContextSupport.BuildSnapshot(
            "SFR",
            "Default",
            "SFR template",
            42,
            typeof(CopilotSavedTemplateContextTests),
            new
            {
                Exposure = 12,
                ApiKey = "super-secret-token",
                Nested = Enumerable.Range(0, 40).Select(index => new
                {
                    Index = index,
                    Value = new string('x', 2_000),
                }),
            });

        Assert.True(result.Success);
        Assert.Contains("\"Exposure\": 12", result.Text, StringComparison.Ordinal);
        Assert.Contains("<redacted>", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", result.Text, StringComparison.Ordinal);
        Assert.Contains("Would modify: False", result.Text, StringComparison.Ordinal);
        Assert.Contains("Would save: False", result.Text, StringComparison.Ordinal);
        Assert.Contains("No database query, mutation, or save was performed.", result.Text, StringComparison.Ordinal);
        Assert.True(result.Text.Length < 14_000);
    }

    [Fact]
    public async Task DispatcherExposesAndRoutesSavedTemplateContextTool()
    {
        var dispatcher = new CopilotMcpToolDispatcher();
        var descriptor = Assert.Single(
            dispatcher.ListTools(),
            tool => string.Equals(tool.Name, "get_saved_template_context", StringComparison.Ordinal));

        Assert.Equal("read-only", descriptor.RiskLevel);

        var result = await dispatcher.CallAsync(
            "get_saved_template_context",
            arguments: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("missing_template_code", result.ErrorCode);
    }

    [Fact]
    public void SavedTemplateReferenceExposesOnlyTheSavedTemplateReadTool()
    {
        var savedRequest = Request(new CopilotContextItem
        {
            Id = "composer-template:saved-sfr-default",
            Title = "Default",
            Content = "[ColorVision saved template reference]",
        });
        var typeOnlyRequest = Request(new CopilotContextItem
        {
            Id = "composer-template-type:sfr",
            Title = "SFR",
            Content = "[ColorVision template type reference]",
        });

        Assert.True(CopilotToolIntentPolicy.NeedsSavedTemplateContext(savedRequest));
        Assert.True(new CopilotInspectSavedTemplateTool(new RecordingCapabilityInvoker()).IsAvailable(savedRequest));
        Assert.False(CopilotToolIntentPolicy.NeedsSavedTemplateContext(typeOnlyRequest));
        Assert.False(new CopilotInspectSavedTemplateTool(new RecordingCapabilityInvoker()).IsAvailable(typeOnlyRequest));
        Assert.Contains(
            CopilotToolRegistry.CreateBuiltInCatalogTools(),
            tool => string.Equals(tool.Name, "InspectSavedTemplate", StringComparison.Ordinal));
    }

    [Fact]
    public void ComposerContextAttachmentExposesSavedTemplateReadTool()
    {
        var content = CopilotConversationRequestBuilder.BuildContextAttachmentContent(
        [
            new CopilotContextItem
            {
                Id = "composer-template:saved-sfr-default",
                Content = "[ColorVision saved template reference]",
            },
        ]);
        var request = new CopilotAgentRequest
        {
            UserText = "检查关联项",
            Mode = CopilotAgentMode.Auto,
            Attachments =
            [
                CopilotAttachmentItem.CreateContext(
                    content,
                    "Default",
                    "composer-template:saved-sfr-default"),
            ],
        };

        Assert.True(CopilotToolIntentPolicy.NeedsSavedTemplateContext(request));
        Assert.True(new CopilotInspectSavedTemplateTool(new RecordingCapabilityInvoker()).IsAvailable(request));
    }

    [Fact]
    public void ManualContextCannotImpersonateSavedTemplateReference()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "检查关联项",
            Mode = CopilotAgentMode.Auto,
            Attachments =
            [
                CopilotAttachmentItem.CreateContext(
                    "[ColorVision saved template reference]",
                    "Manual",
                    "manual-context"),
            ],
        };

        Assert.False(CopilotToolIntentPolicy.NeedsSavedTemplateContext(request));
    }

    [Fact]
    public async Task AgentToolPassesExactReferenceIdentityToReadCapability()
    {
        var invoker = new RecordingCapabilityInvoker();
        var tool = new CopilotInspectSavedTemplateTool(invoker);
        var result = await tool.ExecuteAsync(
            Request(new CopilotContextItem
            {
                Id = "composer-template:saved-sfr-default",
                Content = "[ColorVision saved template reference]",
            }),
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["template_code"] = "SFR",
                    ["template_name"] = "Default",
                },
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("get_saved_template_context", invoker.CapabilityName);
        Assert.Equal(CopilotApplicationCapabilityCaller.InAppAgent, invoker.Caller);
        Assert.Equal("SFR", invoker.Arguments["template_code"].GetString());
        Assert.Equal("Default", invoker.Arguments["template_name"].GetString());
    }

    private static CopilotAgentRequest Request(CopilotContextItem contextItem)
    {
        return new CopilotAgentRequest
        {
            UserText = "检查这个模板",
            Mode = CopilotAgentMode.Auto,
            ContextItems = [contextItem],
        };
    }

    private sealed class RecordingCapabilityInvoker : ICopilotApplicationCapabilityInvoker
    {
        public string CapabilityName { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, JsonElement> Arguments { get; private set; } =
            new Dictionary<string, JsonElement>();

        public CopilotApplicationCapabilityCaller Caller { get; private set; }

        public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            CapabilityName = capabilityName;
            Arguments = arguments ?? new Dictionary<string, JsonElement>();
            Caller = caller;
            return Task.FromResult(new CopilotApplicationCapabilityCallResult
            {
                Success = true,
                Content = "ok",
            });
        }
    }
}
