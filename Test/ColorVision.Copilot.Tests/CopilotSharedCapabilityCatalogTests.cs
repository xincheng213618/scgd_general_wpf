using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSharedCapabilityCatalogTests
{
    [Fact]
    public void SharedCatalogAndInputParametersAreBackedByImmutableViews()
    {
        var catalog = Assert.IsAssignableFrom<IList<CopilotSharedCapabilityDefinition>>(
            CopilotSharedCapabilityCatalog.All);
        var parameters = Assert.IsAssignableFrom<IList<CopilotToolParameter>>(
            CopilotSharedCapabilityCatalog.SearchFiles.AgentInputSchema.Parameters);

        Assert.True(catalog.IsReadOnly);
        Assert.True(parameters.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => catalog[0] = catalog[1]);
        Assert.Throws<NotSupportedException>(() => parameters[0] = parameters[1]);
    }

    [Fact]
    public void McpDescriptorsExposeFrozenSchemasAndAnnotations()
    {
        var descriptor = new CopilotMcpToolDispatcher().ListTools()
            .Single(tool => string.Equals(
                tool.Name,
                "get_server_status",
                StringComparison.OrdinalIgnoreCase));
        var annotations = Assert.IsAssignableFrom<IDictionary<string, object>>(
            descriptor.Annotations);

        Assert.IsType<JsonElement>(descriptor.InputSchema);
        Assert.True(annotations.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => annotations["readOnlyHint"] = false);
    }

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

    [Theory]
    [InlineData("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":true}", "additionalProperties")]
    [InlineData("{\"type\":\"object\",\"properties\":{},\"required\":[\"missing\"],\"additionalProperties\":false}", "undeclared")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"mystery\"}},\"additionalProperties\":false}", "unsupported type")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"integer\",\"minimum\":10,\"maximum\":1}},\"additionalProperties\":false}", "invalid minimum/maximum")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"enum\":[1]}},\"additionalProperties\":false}", "does not match")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"minimum\":1}},\"additionalProperties\":false}", "numeric constraints")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\",\"$ref\":\"#/$defs/value\"}},\"additionalProperties\":false}", "unsupported keyword")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"object\",\"properties\":{},\"additionalProperties\":true}},\"additionalProperties\":false}", "additionalProperties")]
    public void McpSchemaValidationRejectsInvalidPublishedContracts(
        string schemaJson,
        string expectedError)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>(schemaJson);

        Assert.False(CopilotToolInputContractValidator.TryValidateSchema(schema, out var error));
        Assert.Contains(expectedError, error, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void CapabilitySchemaIdentityIgnoresObjectOrderButTracksConstraintChanges()
    {
        var firstSchema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"description\":\"Search text.\"},\"limit\":{\"type\":\"integer\",\"minimum\":1}},\"required\":[\"query\"],\"additionalProperties\":false}"));
        var reorderedSchema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                "{\"additionalProperties\":false,\"required\":[\"query\"],\"properties\":{\"limit\":{\"minimum\":1,\"type\":\"integer\"},\"query\":{\"description\":\"Search text.\",\"type\":\"string\"}},\"type\":\"object\"}"));
        var changedSchema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.Deserialize<JsonElement>(
                "{\"additionalProperties\":false,\"required\":[\"query\"],\"properties\":{\"limit\":{\"minimum\":2,\"type\":\"integer\"},\"query\":{\"description\":\"Search text.\",\"type\":\"string\"}},\"type\":\"object\"}"));
        var catalog = new CopilotCapabilityCatalog();

        var first = catalog.PublishSource(
            CopilotCapabilitySourceKind.ExternalMcp,
            "external:stable-schema",
            "Stable schema server",
            [new SchemaOverrideTool("StableSchemaTool", firstSchema)]);
        var reordered = catalog.PublishSource(
            CopilotCapabilitySourceKind.ExternalMcp,
            "external:stable-schema",
            "Stable schema server",
            [new SchemaOverrideTool("StableSchemaTool", reorderedSchema)]);

        Assert.Equal(
            Assert.Single(first.Capabilities).InputSchemaFingerprint,
            Assert.Single(reordered.Capabilities).InputSchemaFingerprint);
        Assert.Equal(first.Revision, reordered.Revision);
        Assert.Equal(
            Assert.Single(first.Capabilities).Fingerprint,
            Assert.Single(reordered.Capabilities).Fingerprint);

        var changed = catalog.PublishSource(
            CopilotCapabilitySourceKind.ExternalMcp,
            "external:stable-schema",
            "Stable schema server",
            [new SchemaOverrideTool("StableSchemaTool", changedSchema)]);

        Assert.True(changed.Revision > reordered.Revision);
        Assert.NotEqual(
            Assert.Single(reordered.Capabilities).Fingerprint,
            Assert.Single(changed.Capabilities).Fingerprint);
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
    public async Task McpRuntimeDispatchesThePublishedToolDefinition()
    {
        var dispatcher = new CopilotMcpToolDispatcher();
        var publishedName = dispatcher.ListTools()
            .Single(tool => string.Equals(
                tool.Name,
                "get_server_status",
                StringComparison.OrdinalIgnoreCase))
            .Name;

        var result = await dispatcher.CallAsync(
            publishedName,
            null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Text);
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
    public void AgentSharedToolsAreMaterializedOnceInCanonicalCatalogOrder()
    {
        var sharedToolNames = CopilotToolRegistry.CreateCoreDefaultTools()
            .Where(tool => CopilotSharedCapabilityCatalog.TryResolveAgentTool(tool.Name, out _))
            .Select(tool => tool.Name)
            .ToArray();

        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.AgentToolName),
            sharedToolNames);
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
    public void SharedCatalogOwnsEverySurfaceDescription()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        var mcpTools = new CopilotMcpToolDispatcher().ListTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.Equal(
                definition.AgentDescription,
                agentTools[definition.AgentToolName].Description);
            Assert.Equal(
                definition.McpDescription,
                mcpTools[definition.McpToolName].Description);
        }
    }

    [Fact]
    public void SharedCatalogOwnsEveryMcpDescriptorMetadata()
    {
        var mcpTools = new CopilotMcpToolDispatcher().ListTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.True(definition.McpMetadata.IsValid);
            Assert.Equal(
                definition.McpMetadata.Category,
                mcpTools[definition.McpToolName].Category);
            Assert.Equal(
                definition.McpMetadata.UsageHint,
                mcpTools[definition.McpToolName].UsageExample);
            Assert.Equal(
                definition.AgentCapability.Access == CopilotToolAccess.ReadOnly,
                Assert.IsType<bool>(mcpTools[definition.McpToolName].Annotations["readOnlyHint"]));
            Assert.Equal(
                definition.AgentCapability.Idempotency == CopilotToolIdempotency.Idempotent,
                Assert.IsType<bool>(mcpTools[definition.McpToolName].Annotations["idempotentHint"]));
            Assert.Equal(
                definition.McpMetadata.DestructiveHint,
                Assert.IsType<bool>(mcpTools[definition.McpToolName].Annotations["destructiveHint"]));
            Assert.Equal(
                definition.McpMetadata.OpenWorldHint,
                Assert.IsType<bool>(mcpTools[definition.McpToolName].Annotations["openWorldHint"]));
        }

        Assert.Equal(
            ["docs.search"],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.McpMetadata.OpenWorldHint)
                .Select(definition => definition.Id)
                .ToArray());
        Assert.Equal(
            [
                "application.execute-menu",
                "application.set-language",
                "application.set-theme",
                "flow.apply-patch",
                "template.apply-patch",
            ],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.McpMetadata.DestructiveHint)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
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
    public void ApplicationCapabilityToolsShareOneCompositionRootRuntime()
    {
        var tools = CopilotToolRegistry.CreateCoreDefaultTools();
        var clients = tools
            .Where(tool => tool is ICopilotApplicationCapabilityClient)
            .ToArray();

        Assert.Equal(12, clients.Length);
        Assert.Equal(
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.ExecutionRoute
                    == CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime)
                .Select(definition => definition.AgentToolName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(),
            clients.Select(tool => tool.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        var expectedInvoker = ((ICopilotApplicationCapabilityClient)clients[0]).ApplicationCapabilityInvoker;
        Assert.Same(
            CopilotApplicationCapabilityInvokerFactory.CreateDefault(),
            expectedInvoker);
        Assert.All(
            clients,
            tool => Assert.Same(
                expectedInvoker,
                ((ICopilotApplicationCapabilityClient)tool).ApplicationCapabilityInvoker));
    }

    [Fact]
    public void SurfaceAdaptersAreLimitedToNonWorkspaceReadCapabilities()
    {
        Assert.Equal(
            [
                "diagnostics.recent-log",
                "docs.search",
            ],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.ExecutionRoute
                    == CopilotSharedCapabilityExecutionRoute.SurfaceCapabilityAdapter)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void WorkspaceAuthorizationAdaptersAreLimitedToPerTurnEvidenceRoots()
    {
        Assert.Equal(
            [
                "workspace.grep-text",
                "workspace.list-directory",
                "workspace.read-file",
                "workspace.search-files",
            ],
            CopilotSharedCapabilityCatalog.All
                .Where(definition => definition.ExecutionRoute
                    == CopilotSharedCapabilityExecutionRoute.WorkspaceAuthorizationAdapter)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void EverySharedCapabilityDeclaresAnExecutionRoute()
    {
        Assert.DoesNotContain(
            CopilotSharedCapabilityCatalog.All,
            definition => definition.ExecutionRoute
                == CopilotSharedCapabilityExecutionRoute.Unspecified);
    }

    [Fact]
    public void DefaultApplicationCapabilityRuntimeIsProcessShared()
    {
        var first = CopilotApplicationCapabilityInvokerFactory.CreateDefault();
        var second = CopilotApplicationCapabilityInvokerFactory.CreateDefault();

        Assert.Same(first, second);
        Assert.Same(
            first,
            CopilotApplicationCapabilityInvokerFactory.GetDefaultDispatcher());
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

    [Theory]
    [InlineData("search_files", "EvidenceFile.cs", "EvidenceFile")]
    [InlineData("grep_text", "durable evidence", "durable evidence")]
    public async Task WorkspaceSearchMcpSurfaceUsesCanonicalCapabilityOutput(
        string toolName,
        string query,
        string expectedContent)
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(WorkspaceSearchMcpSurfaceUsesCanonicalCapabilityOutput), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "EvidenceFile.cs");
        await File.WriteAllTextAsync(filePath, "// durable evidence");

        try
        {
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = root,
                    SearchRootPaths = [root],
                },
            });
            var arguments = new Dictionary<string, JsonElement>
            {
                ["query"] = JsonSerializer.SerializeToElement(query),
            };

            var result = await dispatcher.CallAsync(toolName, arguments, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(expectedContent, result.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[Scan Complete] true", result.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ColorVision file search results", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("ColorVision text search results", result.Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SharedCatalogOwnsAgentTracePresentation()
    {
        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.True(definition.Presentation.IsValid);
            var running = new CopilotAgentTraceEntry
            {
                ToolName = definition.AgentToolName,
                State = CopilotToolExecutionState.Running,
            };
            var completed = new CopilotAgentTraceEntry
            {
                ToolName = definition.AgentToolName,
                State = CopilotToolExecutionState.Completed,
            };
            var failed = new CopilotAgentTraceEntry
            {
                ToolName = definition.AgentToolName,
                State = CopilotToolExecutionState.Failed,
            };

            Assert.Equal(definition.Presentation.RunningLabel, running.ActivityLabel);
            Assert.Equal(definition.Presentation.CompletedLabel, completed.ActivityLabel);
            Assert.Equal(definition.Presentation.SuccessSummary, completed.ActivityDescription);
            Assert.Equal(!definition.Presentation.IsSearch, failed.IsVisibleInActivity);
            Assert.Equal(
                definition.Presentation.TraceCategory,
                Assert.Single(CopilotAgentTraceGroup.Create([completed])).Category);
        }
    }

    [Fact]
    public void OnlyDocumentedSecurityOrBatchBoundariesUseSurfaceSpecificInputs()
    {
        var surfaceSpecific = CopilotSharedCapabilityCatalog.All
            .Where(definition => !definition.SharesInputSchema)
            .ToArray();

        Assert.Equal(
            ["application.execute-menu", "workspace.read-file"],
            surfaceSpecific.Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.All(
            surfaceSpecific,
            definition => Assert.False(string.IsNullOrWhiteSpace(definition.InputContractDifference)));
        Assert.All(
            CopilotSharedCapabilityCatalog.All.Except(surfaceSpecific),
            definition => Assert.True(string.IsNullOrWhiteSpace(definition.InputContractDifference)));
        Assert.Equal(16, CopilotSharedCapabilityCatalog.All.Count(definition => definition.SharesInputSchema));
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
            12,
            CopilotSharedCapabilityCatalog.All.Count(
                definition => definition.SafetyClass == CopilotSharedCapabilitySafetyClass.ReadOnly));
    }

    [Fact]
    public void SharedCatalogOwnsApprovalReversibilityMetadata()
    {
        var approvalRequired = CopilotSharedCapabilityCatalog.All
            .Where(definition => definition.SafetyClass
                == CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite)
            .ToArray();

        Assert.All(approvalRequired, definition =>
        {
            Assert.True(definition.ApprovalMetadata.IsValid);
            Assert.True(definition.ApprovalMetadata.HasPresentation);
            Assert.False(string.IsNullOrWhiteSpace(
                definition.ApprovalMetadata.ReversibilitySummary));
            Assert.True(CopilotSharedCapabilityCatalog.TryResolveMcpTool(
                definition.McpToolName,
                out var resolved));
            Assert.Same(definition, resolved);
        });
        Assert.Equal(
            [
                "application.create-flow",
                "application.set-language",
                "flow.apply-patch",
                "template.apply-patch",
            ],
            approvalRequired
                .Where(definition => definition.ApprovalMetadata.Reversibility
                    == CopilotApprovalReversibility.ManualOnly)
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            ["application.execute-menu"],
            approvalRequired
                .Where(definition => definition.ApprovalMetadata.Reversibility
                    == CopilotApprovalReversibility.Unknown)
                .Select(definition => definition.Id)
                .ToArray());

        var basePresentation = new CopilotToolApprovalPresentation("title", "description");
        var enriched = CopilotSharedCapabilityCatalog.ApplyApprovalMetadata(
            CopilotSharedCapabilityCatalog.ApplyFlowPatch.AgentToolName,
            basePresentation);
        Assert.Equal(CopilotApprovalReversibility.ManualOnly, enriched.Reversibility);
        Assert.Equal(
            CopilotSharedCapabilityCatalog.ApplyFlowPatch.ApprovalMetadata.ReversibilitySummary,
            enriched.ReversibilitySummary);
    }

    [Fact]
    public void AgentSurfaceValidationRejectsSchemaDrift()
    {
        var tools = CopilotToolRegistry.CreateCoreDefaultTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new SchemaOverrideTool(
            tools[index].Name,
            CopilotToolInputSchema.Query("Drifted query."));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateAgentSurface(tools));

        Assert.Contains("schema drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName, exception.Message);
    }

    [Fact]
    public void McpSurfaceValidationRejectsSchemaDrift()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.McpToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new CopilotMcpToolDescriptor
        {
            Name = tools[index].Name,
            InputSchema = CopilotToolInputSchema.Empty.JsonSchema,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(tools));

        Assert.Contains("schema drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.McpToolName, exception.Message);
    }

    [Fact]
    public void AgentSurfaceValidationRejectsExecutionPolicyDrift()
    {
        var tools = CopilotToolRegistry.CreateCoreDefaultTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new SchemaOverrideTool(
            tools[index].Name,
            tools[index].InputSchema,
            CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateAgentSurface(tools));

        Assert.Contains("execution policy drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName, exception.Message);
    }

    [Fact]
    public void McpSurfaceValidationRejectsExecutionPolicyDrift()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.McpToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new CopilotMcpToolDescriptor
        {
            Name = tools[index].Name,
            InputSchema = CopilotSharedCapabilityCatalog.SearchDocs.McpInputSchema.JsonSchema,
            RiskLevel = "low-risk-action",
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(tools));

        Assert.Contains("execution policy drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.McpToolName, exception.Message);
    }

    [Fact]
    public void AgentSurfaceValidationRejectsDescriptionDrift()
    {
        var tools = CopilotToolRegistry.CreateCoreDefaultTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new SchemaOverrideTool(
            tools[index].Name,
            tools[index].InputSchema,
            tools[index].Capability,
            "Drifted description.");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateAgentSurface(tools));

        Assert.Contains("description drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.AgentToolName, exception.Message);
    }

    [Fact]
    public void McpSurfaceValidationRejectsDescriptionDrift()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.McpToolName,
            StringComparison.OrdinalIgnoreCase));
        tools[index] = new CopilotMcpToolDescriptor
        {
            Name = tools[index].Name,
            Description = "Drifted description.",
            InputSchema = CopilotSharedCapabilityCatalog.SearchDocs.McpInputSchema.JsonSchema,
            RiskLevel = CopilotSharedCapabilityCatalog.SearchDocs.McpRiskLevel,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(tools));

        Assert.Contains("description drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.McpToolName, exception.Message);
    }

    [Fact]
    public void McpSurfaceValidationRejectsDescriptorMetadataDrift()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SearchDocs.McpToolName,
            StringComparison.OrdinalIgnoreCase));
        var original = tools[index];
        tools[index] = new CopilotMcpToolDescriptor
        {
            Name = original.Name,
            Description = original.Description,
            InputSchema = original.InputSchema,
            RiskLevel = original.RiskLevel,
            Category = "drifted-category",
            UsageExample = original.UsageExample,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(tools));

        Assert.Contains("descriptor metadata drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SearchDocs.McpToolName, exception.Message);
    }

    [Fact]
    public void McpSurfaceValidationRejectsAnnotationPolicyDrift()
    {
        var tools = new CopilotMcpToolDispatcher().ListTools().ToList();
        var index = tools.FindIndex(tool => string.Equals(
            tool.Name,
            CopilotSharedCapabilityCatalog.SetTheme.McpToolName,
            StringComparison.OrdinalIgnoreCase));
        var original = tools[index];
        tools[index] = new CopilotMcpToolDescriptor
        {
            Name = original.Name,
            Description = original.Description,
            InputSchema = original.InputSchema,
            RiskLevel = original.RiskLevel,
            Category = original.Category,
            UsageExample = original.UsageExample,
            Annotations = new Dictionary<string, object>(original.Annotations)
            {
                ["idempotentHint"] = false,
            },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(tools));

        Assert.Contains("descriptor metadata drift", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CopilotSharedCapabilityCatalog.SetTheme.McpToolName, exception.Message);
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
