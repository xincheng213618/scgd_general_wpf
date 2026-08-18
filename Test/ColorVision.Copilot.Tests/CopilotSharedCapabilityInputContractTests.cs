using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSharedCapabilityInputContractTests
{
    [Fact]
    public void StructuredAgentInputsBindMcpStyleNamesAndFileCoordinates()
    {
        Assert.True(
            CopilotSharedCapabilityCatalog.RecentLog.AgentInputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["query"] = "timeout",
                    ["max_lines"] = 175,
                },
                out var logInput,
                out var logError),
            logError);
        Assert.Equal("timeout", logInput.Query);
        Assert.Equal(175, logInput.GetInt32Argument("max_lines"));

        Assert.True(
            CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentInputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["path"] = "ColorVision/Copilot/CopilotChatViewModel.cs",
                    ["start_line"] = 20,
                    ["start_column"] = 3,
                    ["end_line"] = 40,
                },
                out var fileInput,
                out var fileError),
            fileError);
        Assert.Equal(20, fileInput.StartLine);
        Assert.Equal(3, fileInput.StartColumn);
        Assert.Equal(40, fileInput.EndLine);

        Assert.False(
            CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentInputSchema.TryBind(
                new Dictionary<string, object?> { ["start_column"] = 3 },
                out _,
                out var invalidRangeError));
        Assert.Contains("requires", invalidRangeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StructuredAgentInputsRecursivelyEnforceTheSharedJsonSchemaValidator()
    {
        var schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "config": {
                      "type": "object",
                      "properties": {
                        "name": { "type": "string", "minLength": 1 }
                      },
                      "required": ["name"],
                      "additionalProperties": false
                    },
                    "modes": {
                      "type": "array",
                      "items": { "type": "string", "enum": ["safe", "fast"] },
                      "minItems": 1
                    }
                  },
                  "required": ["config", "modes"],
                  "additionalProperties": false
                }
                """));

        Assert.False(schema.TryBind(
            new Dictionary<string, object?>
            {
                ["config"] = new Dictionary<string, object?>
                {
                    ["name"] = "valid",
                    ["unexpected"] = true,
                },
                ["modes"] = new[] { "safe" },
            },
            out _,
            out var nestedError));
        Assert.Contains("config.unexpected", nestedError, StringComparison.Ordinal);

        Assert.False(schema.TryBind(
            new Dictionary<string, object?>
            {
                ["config"] = new Dictionary<string, object?> { ["name"] = "valid" },
                ["modes"] = new[] { "unsafe" },
            },
            out _,
            out var arrayError));
        Assert.Contains("modes[0]", arrayError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigratedApplicationToolsForwardStructuredArguments()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation:test",
            TaskId = "task:test",
            UserText = "确认应用这个预览",
            Mode = CopilotAgentMode.Auto,
        };

        var createFlowInvoker = new RecordingInvoker();
        var createFlowTool = new CopilotCreateFlowTool(createFlowInvoker);
        var createFlowInput = Bind(
            createFlowTool.InputSchema,
            new Dictionary<string, object?> { ["name"] = "CalibrationFlow" });
        var createFlowResult = await createFlowTool.ExecuteAsync(
            request,
            createFlowInput,
            CancellationToken.None);
        Assert.True(createFlowResult.Success);
        Assert.Equal("create_flow", createFlowInvoker.CapabilityName);
        Assert.Equal("CalibrationFlow", createFlowInvoker.Arguments["name"].GetString());

        var previewInvoker = new RecordingInvoker();
        var previewTool = new CopilotTemplatePatchTool(previewInvoker);
        var previewInput = Bind(
            previewTool.InputSchema,
            new Dictionary<string, object?>
            {
                ["template_identifier"] = "active-template",
                ["proposed_changes"] = JsonSerializer.SerializeToElement(new { Exposure = 12 }),
            });
        var previewResult = await previewTool.ExecuteAsync(
            request,
            previewInput,
            CancellationToken.None);
        Assert.True(previewResult.Success);
        Assert.Equal("preview_template_patch", previewInvoker.CapabilityName);
        Assert.Equal(12, previewInvoker.Arguments["proposed_changes"].GetProperty("Exposure").GetInt32());

        var applyInvoker = new RecordingInvoker();
        var applyTool = new CopilotApplyTemplatePatchTool(applyInvoker);
        var applyInput = Bind(
            applyTool.InputSchema,
            new Dictionary<string, object?> { ["preview_id"] = "preview:123" });
        var applyResult = await applyTool.ExecuteAsync(
            request,
            applyInput,
            CancellationToken.None);
        Assert.True(applyResult.Success);
        Assert.Equal("apply_template_patch", applyInvoker.CapabilityName);
        Assert.Equal("preview:123", applyInvoker.Arguments["preview_id"].GetString());

        var languageInvoker = new RecordingInvoker();
        var languageTool = new CopilotSetLanguageTool(languageInvoker);
        var languageInput = Bind(
            languageTool.InputSchema,
            new Dictionary<string, object?> { ["language"] = "en-US" });
        var languageResult = await languageTool.ExecuteAsync(
            request,
            languageInput,
            CancellationToken.None);
        Assert.True(languageResult.Success);
        Assert.Equal("set_language", languageInvoker.CapabilityName);
        Assert.Equal("en-US", languageInvoker.Arguments["language"].GetString());

        var themeInvoker = new RecordingInvoker();
        var themeTool = new CopilotSetThemeTool(themeInvoker);
        var themeInput = Bind(
            themeTool.InputSchema,
            new Dictionary<string, object?> { ["theme"] = "dark" });
        var themeResult = await themeTool.ExecuteAsync(
            request,
            themeInput,
            CancellationToken.None);
        Assert.True(themeResult.Success);
        Assert.Equal("set_theme", themeInvoker.CapabilityName);
        Assert.Equal("dark", themeInvoker.Arguments["theme"].GetString());
    }

    [Fact]
    public async Task LegacyCreateFlowQueryStillMapsToTheStructuredCapability()
    {
        var invoker = new RecordingInvoker();
        var tool = new CopilotCreateFlowTool(invoker);

        var result = await tool.ExecuteAsync(
            new CopilotAgentRequest
            {
                ConversationId = "conversation:test",
                TaskId = "task:test",
                UserText = "创建流程",
                Mode = CopilotAgentMode.Auto,
            },
            new CopilotAgentToolInput { Query = "LegacyFlow" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("LegacyFlow", invoker.Arguments["name"].GetString());
    }

    [Fact]
    public void ApplicationCapabilityResultAdapterPreservesApprovalAndFailureSemantics()
    {
        var approval = new CopilotToolApprovalInfo
        {
            ActionId = "approval:1",
            Title = "Confirm",
        };
        var waiting = CopilotApplicationCapabilityInvocation.ToToolResult(
            new CopilotApplicationCapabilityCallResult
            {
                Content = "approval detail",
                Approval = approval,
            },
            "ExampleTool",
            "completed",
            "failed",
            "waiting");
        var failed = CopilotApplicationCapabilityInvocation.ToToolResult(
            new CopilotApplicationCapabilityCallResult
            {
                Content = "denied",
                ErrorCode = "policy_denied",
                FailureKind = CopilotToolFailureKind.Authorization,
            },
            "ExampleTool",
            "completed",
            "failed");

        Assert.True(waiting.Success);
        Assert.Equal("waiting", waiting.Summary);
        Assert.Same(approval, waiting.Approval);
        Assert.Empty(waiting.ErrorMessage);
        Assert.False(failed.Success);
        Assert.Equal("failed", failed.Summary);
        Assert.Equal("denied", failed.ErrorMessage);
        Assert.Equal("policy_denied", failed.FailureCode);
        Assert.Equal(CopilotToolFailureKind.Authorization, failed.FailureKind);
    }

    private static CopilotAgentToolInput Bind(
        CopilotToolInputSchema schema,
        IReadOnlyDictionary<string, object?> arguments)
    {
        Assert.True(schema.TryBind(arguments, out var input, out var error), error);
        return input;
    }

    private sealed class RecordingInvoker : ICopilotApplicationCapabilityInvoker
    {
        public string CapabilityName { get; private set; } = string.Empty;

        public Dictionary<string, JsonElement> Arguments { get; private set; } =
            new Dictionary<string, JsonElement>();

        public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityName = capabilityName;
            Arguments = (arguments ?? new Dictionary<string, JsonElement>())
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new CopilotApplicationCapabilityCallResult
            {
                Success = true,
                Content = "ok",
            });
        }
    }
}
