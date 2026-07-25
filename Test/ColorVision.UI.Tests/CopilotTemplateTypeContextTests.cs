using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotTemplateTypeContextTests
{
    [Fact]
    public void TypeContextReturnsBoundedSchemaWithoutValuesOrSensitiveFields()
    {
        var names = Enumerable.Range(0, 100)
            .Select(index => $"Saved template {index:D3} " + new string('x', 180))
            .ToArray();
        var result = CopilotSavedTemplateContextSupport.BuildTypeContext(
            "SFR",
            "TemplateSFR",
            "SFR template",
            9,
            typeof(CopilotTemplateTypeContextTests),
            typeof(SampleParameter),
            names);

        Assert.True(result.Success);
        Assert.Contains("Would read template values: False", result.Text, StringComparison.Ordinal);
        Assert.Contains("Would query database: False", result.Text, StringComparison.Ordinal);
        Assert.Contains("Loaded saved template count: 100", result.Text, StringComparison.Ordinal);
        Assert.Contains("Exposure", result.Text, StringComparison.Ordinal);
        Assert.Contains("Exposure time in milliseconds.", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", result.Text, StringComparison.Ordinal);
        Assert.Contains("Sensitive parameter fields omitted: 1", result.Text, StringComparison.Ordinal);
        Assert.Contains("Metadata truncated: True", result.Text, StringComparison.Ordinal);
        Assert.True(result.Text.Length <= 12_000);
    }

    [Fact]
    public async Task DispatcherExposesAndRoutesTemplateTypeContextTool()
    {
        var dispatcher = new CopilotMcpToolDispatcher();
        var descriptor = Assert.Single(
            dispatcher.ListTools(),
            tool => string.Equals(tool.Name, "get_template_type_context", StringComparison.Ordinal));

        Assert.Equal("read-only", descriptor.RiskLevel);

        var result = await dispatcher.CallAsync(
            "get_template_type_context",
            arguments: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("missing_template_code", result.ErrorCode);
    }

    [Fact]
    public void TypeOnlyReferenceExposesOnlyTemplateTypeInspection()
    {
        var typeRequest = Request(
            "composer-template-type:sfr",
            "[ColorVision template type reference]",
            "检查关联的模板类型");
        var savedRequest = Request(
            "composer-template:saved-sfr-default",
            "[ColorVision saved template reference]",
            "检查关联模板");
        var invoker = new RecordingCapabilityInvoker();

        Assert.True(CopilotToolIntentPolicy.NeedsTemplateTypeContext(typeRequest));
        Assert.True(new CopilotInspectTemplateTypeTool(invoker).IsAvailable(typeRequest));
        Assert.False(CopilotToolIntentPolicy.NeedsSavedTemplateContext(typeRequest));
        Assert.False(CopilotToolIntentPolicy.NeedsTemplateTypeContext(savedRequest));
        Assert.False(new CopilotInspectTemplateTypeTool(invoker).IsAvailable(savedRequest));
        Assert.Contains(
            CopilotToolRegistry.CreateBuiltInCatalogTools(),
            tool => string.Equals(tool.Name, "InspectTemplateType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentToolPassesExactTemplateCodeToReadCapability()
    {
        var invoker = new RecordingCapabilityInvoker();
        var tool = new CopilotInspectTemplateTypeTool(invoker);
        var result = await tool.ExecuteAsync(
            Request(
                "composer-template-type:sfr",
                "[ColorVision template type reference]",
                "检查关联的模板类型"),
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["template_code"] = "SFR",
                },
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("get_template_type_context", invoker.CapabilityName);
        Assert.Equal("SFR", invoker.Arguments["template_code"].GetString());
        Assert.Equal(CopilotApplicationCapabilityCaller.InAppAgent, invoker.Caller);
    }

    private static CopilotAgentRequest Request(string id, string content, string userText)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            ContextItems =
            [
                new CopilotContextItem
                {
                    Id = id,
                    Content = content,
                },
            ],
        };
    }

    private sealed class SampleParameter
    {
        [Category("Camera")]
        [Description("Exposure time in milliseconds.")]
        public int Exposure { get; set; }

        public string ApiKey { get; set; } = "must-never-be-read";
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
