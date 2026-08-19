using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotExternalToolLeaseTests
{
    [Fact]
    public async Task ConstructorDetachesAndFreezesPublishedSurfaces()
    {
        var originalTool = new TestTool("original");
        var sourceTools = new List<ICopilotTool> { originalTool };
        var sourceDiagnostics = new List<string> { "original diagnostic" };
        var lease = new CopilotExternalToolLease(sourceTools, sourceDiagnostics);

        sourceTools[0] = new TestTool("source-mutated");
        sourceTools.Add(new TestTool("source-added"));
        sourceDiagnostics[0] = "source-mutated";
        sourceDiagnostics.Add("source-added");

        Assert.Same(originalTool, Assert.Single(lease.Tools));
        Assert.Equal("original diagnostic", Assert.Single(lease.Diagnostics));
        AssertReadOnly(lease.Tools, new TestTool("replacement"));
        AssertReadOnly(lease.Diagnostics, "replacement");

        await lease.DisposeAsync();
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values, T replacement)
    {
        var items = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(items.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => items[0] = replacement);
    }

    private sealed class TestTool(string name) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => Name;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
            => Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            });
    }
}
