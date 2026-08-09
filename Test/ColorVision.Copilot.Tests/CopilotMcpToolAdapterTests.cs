using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpToolAdapterTests
{
    [Fact]
    public void RequestOptionsIncludeCurrentCallIdWithoutLeakingInvocationContext()
    {
        Assert.Null(CopilotMcpToolAdapter.CreateRequestOptionsForCurrentInvocation());

        using (CopilotToolInvocationContext.Enter(new CopilotToolInvocation
        {
            CallId = "mcp-call-42",
        }))
        {
            var options = Assert.IsType<ModelContextProtocol.RequestOptions>(
                CopilotMcpToolAdapter.CreateRequestOptionsForCurrentInvocation());
            var meta = options.GetMetaForRequest();

            Assert.Equal("mcp-call-42", meta["callId"]?.GetValue<string>());
        }

        Assert.Null(CopilotMcpToolAdapter.CreateRequestOptionsForCurrentInvocation());
    }

    [Fact]
    public void RequestOptionsOmitBlankCallIds()
    {
        using var context = CopilotToolInvocationContext.Enter(new CopilotToolInvocation
        {
            CallId = "   ",
        });

        Assert.Null(CopilotMcpToolAdapter.CreateRequestOptionsForCurrentInvocation());
    }
}
