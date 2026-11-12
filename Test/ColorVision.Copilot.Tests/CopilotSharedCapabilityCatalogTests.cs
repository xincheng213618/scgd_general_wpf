using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSharedCapabilityCatalogTests
{

    [Fact]
    public void EveryMcpDescriptorPublishesAClosedValidInputSchema()
    {
        foreach (var descriptor in new CopilotMcpToolDispatcher().ListTools())
        {
            Assert.True(
                CopilotToolInputContractValidator.TryValidateSchema(
                    descriptor.InputSchema,
                    out var error),
                $"{descriptor.Name}: {error}");
        }
    }

    [Fact]
    public void EveryBuiltInAgentToolPublishesAClosedValidInputSchema()
    {
        foreach (var tool in CopilotToolRegistry.CreateBuiltInCatalogTools())
        {
            Assert.True(
                CopilotToolInputContractValidator.TryValidateSchema(
                    tool.InputSchema.JsonSchema,
                    out var error),
                $"{tool.Name}: {error}");
        }
    }

    [Fact]
    public void ExternalMcpRegistrationAllowsOpenObjectsButRejectsUnsupportedSemantics()
    {
        var openSchema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                "{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\",\"type\":\"object\",\"title\":\"External input\",\"properties\":{\"payload\":{\"type\":\"object\",\"default\":{},\"examples\":[{\"kind\":\"provider-specific\"}]}}}"));
        var unsupportedSchema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"$ref\":\"#/$defs/value\"}}}"));
        var openTool = new SchemaOverrideTool("OpenExternalTool", openSchema);
        var unsupportedTool = new SchemaOverrideTool("UnsupportedExternalTool", unsupportedSchema);

        var compatible = CopilotMcpToolProvider.SelectRuntimeCompatibleTools(
            [openTool, unsupportedTool],
            out var rejectedCount);
        var catalog = new CopilotCapabilityCatalog();
        var snapshot = catalog.PublishSource(
            CopilotCapabilitySourceKind.ExternalMcp,
            "external:test",
            "External test",
            compatible);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            "{\"payload\":{\"provider_specific\":true},\"provider_extension\":42}");

        Assert.Equal(1, rejectedCount);
        Assert.Same(openTool, Assert.Single(compatible));
        Assert.Single(snapshot.Capabilities);
        Assert.True(CopilotToolInputContractValidator.TryValidateSchema(
            openSchema.JsonSchema,
            out var schemaError,
            requireClosedObjects: false), schemaError);
        Assert.True(CopilotToolInputContractValidator.TryValidate(
            openSchema.JsonSchema,
            arguments,
            out var validationError), validationError);
        Assert.Throws<ArgumentException>(() => catalog.PublishSource(
            CopilotCapabilitySourceKind.BuiltIn,
            "builtin:open-test",
            "Built-in open test",
            [openTool]));
    }

    [Theory]
    [InlineData("{\"config\":{\"name\":\"valid\",\"unexpected\":true}}", "config.unexpected", "not declared")]
    [InlineData("{\"config\":{}}", "config.name", "missing")]
    public void McpRuntimeValidatorRecursivelyEnforcesClosedObjectContracts(
        string argumentsJson,
        string expectedPath,
        string expectedError)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "config": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" }
                  },
                  "required": ["name"],
                  "additionalProperties": false
                }
              },
              "required": ["config"],
              "additionalProperties": false
            }
            """);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);

        Assert.True(CopilotToolInputContractValidator.TryValidateSchema(schema, out var schemaError), schemaError);
        Assert.False(CopilotToolInputContractValidator.TryValidate(schema, arguments, out var error));
        Assert.Contains(expectedPath, error, StringComparison.Ordinal);
        Assert.Contains(expectedError, error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("get_server_status", "{\"unexpected\":true}", "not declared")]
    [InlineData("search_files", "{\"query\":42}", "must be a string")]
    [InlineData("get_flow_graph", "{\"max_nodes\":201}", "less than or equal to 200")]
    [InlineData("preview_flow_patch", "{\"operation\":\"replace_everything\",\"expected_revision\":\"rev-1\"}", "not one of the values")]
    [InlineData("get_agent_task_events", "{\"event_types\":[\"notAnEvent\"]}", "event_types[0]")]
    public async Task McpRuntimeEnforcesPublishedInputSchema(
        string toolName,
        string argumentsJson,
        string expectedError)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);

        var result = await new CopilotMcpToolDispatcher().CallAsync(
            toolName,
            arguments,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Contains(expectedError, result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task McpRuntimeRejectsMissingPublishedRequiredArgumentBeforeDispatch()
    {
        var result = await new CopilotMcpToolDispatcher().CallAsync(
            "preview_flow_patch",
            new Dictionary<string, JsonElement>
            {
                ["operation"] = JsonSerializer.SerializeToElement("add_node"),
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Contains("expected_revision", result.Text, StringComparison.Ordinal);
        Assert.Contains("missing", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task McpRuntimeRejectsNamesWithoutPublishedDefinitions()
    {
        var result = await new CopilotMcpToolDispatcher().CallAsync(
            "not_a_published_tool",
            null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("tool_not_found", result.ErrorCode);
        Assert.Equal(CopilotToolFailureKind.NotFound, result.FailureKind);
    }

    [Fact]
    public async Task McpInputContractFailureDoesNotReachApplicationHandler()
    {
        var handlerInvoked = false;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            SetThemeHandler = (_, _) =>
            {
                handlerInvoked = true;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("theme changed"));
            },
        });

        var result = await dispatcher.CallAsync(
            "set_theme",
            new Dictionary<string, JsonElement>
            {
                ["theme"] = JsonSerializer.SerializeToElement("Dark"),
                ["unexpected"] = JsonSerializer.SerializeToElement(true),
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.RequiresApproval);
        Assert.False(handlerInvoked);
    }

    [Theory]
    [InlineData("failure-metadata")]
    [InlineData("partial-null")]
    [InlineData("failure-partial")]
    [InlineData("approval-partial")]
    public async Task McpRuntimeRejectsContradictoryHandlerOutcomeBeforePublication(string violation)
    {
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            SetThemeHandler = (_, _) => Task.FromResult(violation switch
            {
                "failure-metadata" => new CopilotMcpToolCallResult
                {
                    Success = true, Text = "untrusted success payload", ErrorCode = "theme_change_failed", FailureKind = CopilotToolFailureKind.Internal,
                },
                "partial-null" => CopilotMcpToolCallResult.Ok("untrusted success payload", null!),
                "failure-partial" => new CopilotMcpToolCallResult
                {
                    Success = false, Text = "untrusted success payload", ErrorCode = "theme_change_failed",
                    FailureKind = CopilotToolFailureKind.Validation, PartialResultMessage = "untrusted partial coverage",
                },
                "approval-partial" => new CopilotMcpToolCallResult
                {
                    Success = false, Text = "untrusted success payload", ErrorCode = "confirmation_required", RequiresApproval = true,
                    ApprovalActionId = "action:valid", ApprovalTitle = "Confirm", ApprovalRiskLevel = "write",
                    ApprovalExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1), PartialResultMessage = "untrusted partial coverage",
                },
                _ => throw new InvalidOperationException(),
            }),
        });

        var result = await dispatcher.CallAsync(
            "set_theme",
            new Dictionary<string, JsonElement>
            {
                ["theme"] = JsonSerializer.SerializeToElement("Dark"),
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.RequiresApproval);
        Assert.Equal(CopilotMcpToolResultContract.InvalidOutputFailureCode, result.ErrorCode);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.DoesNotContain("untrusted success payload", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.PartialResultMessage);
    }

    [Fact]
    public async Task McpRuntimeRejectsIncompleteApprovalOutcomeBeforePublication()
    {
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            SetThemeHandler = (_, _) => Task.FromResult(new CopilotMcpToolCallResult
            {
                Text = "approval required",
                ErrorCode = "confirmation_required",
                RequiresApproval = true,
                ApprovalActionId = "action:incomplete",
            }),
        });

        var result = await dispatcher.CallAsync(
            "set_theme",
            new Dictionary<string, JsonElement>
            {
                ["theme"] = JsonSerializer.SerializeToElement("Dark"),
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.RequiresApproval);
        Assert.Equal(CopilotMcpToolResultContract.InvalidOutputFailureCode, result.ErrorCode);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
    }

    [Fact]
    public async Task McpRuntimeBoundsNonCooperativeHandlersWithStructuredTimeout()
    {
        var release = new TaskCompletionSource<CopilotMcpToolCallResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerToken = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new CopilotMcpToolDispatcher(
            new CopilotMcpToolEnvironment
            {
                SetThemeHandler = (_, cancellationToken) =>
                {
                    handlerToken.TrySetResult(cancellationToken);
                    return release.Task;
                },
            },
            executionTimeoutLimit: TimeSpan.FromMilliseconds(200));

        try
        {
            var call = dispatcher.CallAsync(
                "set_theme",
                new Dictionary<string, JsonElement>
                {
                    ["theme"] = JsonSerializer.SerializeToElement("Dark"),
                },
                CancellationToken.None);
            var cancellationToken = await handlerToken.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var result = await call;

            Assert.False(result.Success);
            Assert.False(result.RequiresApproval);
            Assert.Equal("tool_execution_timeout", result.ErrorCode);
            Assert.Equal(CopilotToolFailureKind.Transient, result.FailureKind);
            Assert.True(cancellationToken.IsCancellationRequested);
        }
        finally
        {
            release.TrySetResult(CopilotMcpToolCallResult.Ok("late completion"));
        }
    }

    [Fact]
    public async Task McpRuntimePropagatesCallerCancellationAcrossExecutionBoundary()
    {
        var release = new TaskCompletionSource<CopilotMcpToolCallResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerToken = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            SetThemeHandler = (_, cancellationToken) =>
            {
                handlerToken.TrySetResult(cancellationToken);
                return release.Task;
            },
        });
        using var callerCancellation = new CancellationTokenSource();

        try
        {
            var call = dispatcher.CallAsync(
                "set_theme",
                new Dictionary<string, JsonElement>
                {
                    ["theme"] = JsonSerializer.SerializeToElement("Dark"),
                },
                callerCancellation.Token);
            var cancellationToken = await handlerToken.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await callerCancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
            Assert.True(cancellationToken.IsCancellationRequested);
        }
        finally
        {
            release.TrySetResult(CopilotMcpToolCallResult.Ok("late completion"));
        }
    }

    [Fact]
    public void SharedCatalogMapsEveryDeclaredCapabilityToBothSurfaces()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools();
        var mcpTools = new CopilotMcpToolDispatcher().ListTools();

        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.AgentToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.McpToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.Contains(agentTools, tool => string.Equals(
                tool.Name,
                definition.AgentToolName,
                StringComparison.OrdinalIgnoreCase));
            Assert.Contains(mcpTools, tool => string.Equals(
                tool.Name,
                definition.McpToolName,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SharedCatalogOwnsEveryAgentAndMcpInputContract()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        var mcpTools = new CopilotMcpToolDispatcher().ListTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.Same(
                definition.AgentInputSchema,
                agentTools[definition.AgentToolName].InputSchema);
            var mcpSchema = Assert.IsType<JsonElement>(mcpTools[definition.McpToolName].InputSchema);
            Assert.Equal(
                definition.McpInputSchema.JsonSchema.GetRawText(),
                mcpSchema.GetRawText());
        }
    }

    [Fact]
    public void SharedCatalogOwnsEveryAgentExecutionPolicy()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.Same(
                definition.AgentCapability,
                agentTools[definition.AgentToolName].Capability);
        }
    }

    [Fact]
    public void NonSharedMcpWritesDefaultToConservativeAnnotations()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        Assert.True(Assert.IsType<bool>(tools["get_server_status"].Annotations["readOnlyHint"]));
        Assert.False(Assert.IsType<bool>(tools["get_server_status"].Annotations["destructiveHint"]));
        Assert.False(Assert.IsType<bool>(tools["confirm_action"].Annotations["readOnlyHint"]));
        Assert.True(Assert.IsType<bool>(tools["confirm_action"].Annotations["destructiveHint"]));
        Assert.False(Assert.IsType<bool>(tools["confirm_action"].Annotations["idempotentHint"]));
    }

    [Fact]
    public async Task SharedRuntimeReadsCurrentEnvironmentForEveryInvocation()
    {
        var currentSnapshot = new CopilotMcpWorkspaceSnapshot
        {
            SolutionDirectoryPath = @"C:\workspace\first",
            ActiveDocumentPath = @"C:\workspace\first\First.cs",
            SearchRootPaths = [@"C:\workspace\first"],
        };
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => currentSnapshot,
        });

        var first = await dispatcher.CallAsync("get_workspace_context", null, CancellationToken.None);

        currentSnapshot = new CopilotMcpWorkspaceSnapshot
        {
            SolutionDirectoryPath = @"C:\workspace\second",
            ActiveDocumentPath = @"C:\workspace\second\Second.cs",
            SearchRootPaths = [@"C:\workspace\second"],
        };
        var second = await dispatcher.CallAsync("get_workspace_context", null, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Contains(@"C:\workspace\first", first.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\workspace\second", first.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(second.Success);
        Assert.Contains(@"C:\workspace\second", second.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\workspace\first", second.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedCatalogOwnsTheCrossSurfaceSafetyClassification()
    {
        Assert.Equal(
            ["application.set-theme"],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.SafetyClass == CopilotSharedCapabilitySafetyClass.LowRiskWrite)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            [
                "application.create-flow",
                "application.execute-menu",
                "application.set-language",
                "flow.apply-patch",
                "template.apply-patch",
            ],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.SafetyClass == CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            24,
            CopilotSharedCapabilityCatalog.All.Count(
                definition => definition.SafetyClass == CopilotSharedCapabilitySafetyClass.ReadOnly));
    }

    private sealed class SchemaOverrideTool(
        string name,
        CopilotToolInputSchema inputSchema,
        CopilotToolCapabilityDescriptor? capability = null,
        string description = "Schema validation test tool.") : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description { get; } = description;

        public CopilotToolInputSchema InputSchema { get; } = inputSchema;

        public CopilotToolCapabilityDescriptor Capability { get; } =
            capability ?? CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true });
    }
}
