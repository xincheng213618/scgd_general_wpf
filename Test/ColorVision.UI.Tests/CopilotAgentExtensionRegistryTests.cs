#pragma warning disable CA1707
using ColorVision.Copilot;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentExtensionRegistryTests
{
    [Fact]
    public void Registry_RegistersAndUnregistersAtomicallyAndRejectsConflicts()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var changes = new List<CopilotAgentExtensionRegistryChangedEventArgs>();
        registry.Changed += (_, change) => changes.Add(change);
        var provider = new TestContextProvider("module-context");
        var tool = new TestModuleTool("InspectModule");

        var registration = registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:module",
            SourceName = "Test module",
            SourceVersion = "1.2.3",
            ContextProviders = [provider],
            Tools = [tool],
        });

        var active = Assert.Single(registry.GetSnapshot().Extensions);
        Assert.Equal("test:module", active.SourceId);
        Assert.Same(provider, Assert.Single(active.ContextProviders));
        Assert.Same(tool, Assert.Single(active.Tools));
        Assert.Throws<InvalidOperationException>(() => registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:module",
            SourceName = "Duplicate source",
            ContextProviders = [new TestContextProvider("duplicate-source")],
        }));
        Assert.Throws<InvalidOperationException>(() => registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:other",
            SourceName = "Duplicate tool",
            Tools = [new TestModuleTool("inspectmodule")],
        }));

        registration.Dispose();
        registration.Dispose();

        var removed = registry.GetSnapshot();
        Assert.Empty(removed.Extensions);
        Assert.Equal(2, removed.Revision);
        Assert.Collection(
            changes,
            change =>
            {
                Assert.Equal(1, change.ExtensionCount);
                Assert.Equal(1, change.ContextProviderCount);
                Assert.Equal(1, change.ToolCount);
            },
            change =>
            {
                Assert.Equal(0, change.ExtensionCount);
                Assert.Equal(0, change.ContextProviderCount);
                Assert.Equal(0, change.ToolCount);
            });
    }

    [Fact]
    public async Task Bridge_UpdatesLiveContextToolSurfaceAndCapabilityCatalogWithRegistrationLifetime()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var catalog = new CopilotCapabilityCatalog();
        using var bridge = new CopilotAgentExtensionBridge(registry, catalog);
        var contextRegistry = new CopilotContextRegistry(Array.Empty<ICopilotContextProvider>(), bridge);
        var toolRegistry = new CopilotToolRegistry(Array.Empty<ICopilotTool>(), bridge);
        var moduleTool = new TestModuleTool("InspectModule");
        using var registration = registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:dynamic-module",
            SourceName = "Dynamic module",
            SourceVersion = "4.5.6",
            ContextProviders = [new TestContextProvider("dynamic-context")],
            Tools = [moduleTool],
        });

        var contextItems = await contextRegistry.CaptureAsync(
            new CopilotContextRequest { Scope = CopilotContextScope.Agent, UserText = "inspect it" },
            CancellationToken.None);
        var request = CreateAgentRequest("inspect it");
        var activeTool = Assert.Single(toolRegistry.FindTools(request));
        var result = await activeTool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["query"] = "current state" },
                Query = "current state",
            },
            CancellationToken.None);

        Assert.Equal("dynamic-context", Assert.Single(contextItems).Id);
        Assert.True(result.Success);
        Assert.Equal("current state", moduleTool.LastRequest?.Arguments["query"]);
        var source = Assert.Single(bridge.GetSnapshot().Sources);
        Assert.Equal("Dynamic module", source.SourceName);
        Assert.Equal(1, source.ContextProviderCount);
        Assert.Equal(1, source.DeclaredToolCount);
        Assert.Equal(1, source.ActiveToolCount);
        var catalogEntry = Assert.Single(catalog.GetSnapshot().Capabilities);
        Assert.Equal("extension:test:dynamic-module:inspectmodule", catalogEntry.Id);
        Assert.Equal(CopilotCapabilitySourceKind.Plugin, catalogEntry.SourceKind);
        Assert.Equal(CopilotToolAccess.ReadOnly, catalogEntry.Access);

        registration.Dispose();

        var staleResult = await activeTool.ExecuteAsync(request, CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.Empty(bridge.GetSnapshot().ContextProviders);
        Assert.Empty(bridge.GetSnapshot().Sources);
        Assert.Empty(toolRegistry.Tools);
        Assert.Empty(catalog.GetSnapshot().Capabilities);
        Assert.False(staleResult.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, staleResult.FailureKind);
        Assert.Equal(1, moduleTool.ExecuteCount);
    }

    [Fact]
    public async Task ModuleWriteTool_FailsClosedUntilFrameworkApprovalAndRedactsApprovalArguments()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var catalog = new CopilotCapabilityCatalog();
        using var bridge = new CopilotAgentExtensionBridge(registry, catalog);
        var moduleTool = new TestModuleTool("UpdateModule", CopilotModuleToolAccess.Write);
        using var registration = registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:write-module",
            SourceName = "Write module",
            Tools = [moduleTool],
        });
        var tool = Assert.Single(bridge.GetSnapshot().Tools);
        var request = CreateAgentRequest("update the module");
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["query"] = "apply",
                ["api_key"] = "secret-value",
            },
            Query = "apply",
        };

        var denied = await tool.ExecuteAsync(request, input, CancellationToken.None);

        Assert.False(denied.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, denied.FailureKind);
        Assert.Equal(0, moduleTool.ExecuteCount);
        var approvedTool = Assert.IsAssignableFrom<ICopilotFrameworkApprovedTool>(tool);
        var approved = await approvedTool.ExecuteApprovedAsync(request, input, CancellationToken.None);
        var presentation = Assert.IsAssignableFrom<ICopilotFrameworkApprovalPresentation>(tool).CreateApprovalPresentation(input);

        Assert.True(approved.Success);
        Assert.Equal(1, moduleTool.ExecuteCount);
        Assert.True(moduleTool.LastRequest?.IsApproved);
        Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);
        Assert.Contains("<redacted>", presentation.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", presentation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Bridge_ReportsAndExcludesReservedToolNameConflicts()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var catalog = new CopilotCapabilityCatalog();
        using var bridge = new CopilotAgentExtensionBridge(registry, catalog, ["InspectModule"]);
        using var registration = registry.Register(new CopilotAgentExtensionRegistration
        {
            SourceId = "test:conflict",
            SourceName = "Conflicting module",
            Tools = [new TestModuleTool("inspectmodule")],
        });

        var snapshot = bridge.GetSnapshot();

        Assert.Empty(snapshot.Tools);
        var source = Assert.Single(snapshot.Sources);
        Assert.Equal(1, source.DeclaredToolCount);
        Assert.Equal(0, source.ActiveToolCount);
        var issue = Assert.Single(snapshot.Issues);
        Assert.Equal("test:conflict", issue.SourceId);
        Assert.Equal("inspectmodule", issue.CapabilityName);
        Assert.Empty(catalog.GetSnapshot().Capabilities);
    }

    [Fact]
    public async Task ContextRegistry_IsolatesCanProvideFailuresFromOtherProviders()
    {
        var contextRegistry = new CopilotContextRegistry(
        [
            new ThrowingContextProvider(),
            new TestContextProvider("healthy-context"),
        ]);

        var items = await contextRegistry.CaptureAsync(
            new CopilotContextRequest { Scope = CopilotContextScope.Agent },
            CancellationToken.None);

        Assert.Equal("healthy-context", Assert.Single(items).Id);
    }

    private static CopilotAgentRequest CreateAgentRequest(string userText)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            ContextItems = [new CopilotContextItem { Id = "request-context", Summary = "Current state" }],
            SearchRootPaths = ["C:\\workspace"],
            ActiveDocumentPath = "C:\\workspace\\active.json",
        };
    }

    private sealed class TestContextProvider : ICopilotContextProvider
    {
        private readonly string _id;

        public TestContextProvider(string id)
        {
            _id = id;
        }

        public int Order => 25;

        public bool CanProvide(CopilotContextScope scope) => scope != CopilotContextScope.Chat;

        public Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = _id,
                Title = "Module context",
                Summary = request.UserText,
            });
        }
    }

    private sealed class TestModuleTool : ICopilotModuleTool
    {
        public TestModuleTool(string name, CopilotModuleToolAccess access = CopilotModuleToolAccess.ReadOnly)
        {
            Name = name;
            Access = access;
        }

        public string Name { get; }

        public string Description => "Inspect or update test module state.";

        public CopilotModuleToolAccess Access { get; }

        public string InputJsonSchema => "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"},\"api_key\":{\"type\":\"string\"}},\"additionalProperties\":false}";

        public int ExecuteCount { get; private set; }

        public CopilotModuleToolRequest? LastRequest { get; private set; }

        public bool IsAvailable(CopilotModuleToolRequest request) => request.Mode != CopilotModuleAgentMode.Chat;

        public Task<CopilotModuleToolResult> ExecuteAsync(CopilotModuleToolRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            LastRequest = request;
            return Task.FromResult(CopilotModuleToolResult.Ok("Module operation completed."));
        }
    }

    private sealed class ThrowingContextProvider : ICopilotContextProvider
    {
        public int Order => 5;

        public bool CanProvide(CopilotContextScope scope) => throw new InvalidOperationException("Simulated provider failure.");

        public Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This method should not be reached.");
        }
    }
}
