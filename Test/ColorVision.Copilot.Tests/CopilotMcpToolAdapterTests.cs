using ColorVision.Copilot;
using ModelContextProtocol.Protocol;

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

    [Fact]
    public void ResultProjectionPreservesBlockOrderAndResourceLinksWithoutBinaryPayloads()
    {
        byte[] binaryData = [1, 2, 3, 4];
        var encodedData = Convert.ToBase64String(binaryData);
        var result = new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = "before" },
                ImageContentBlock.FromBytes(binaryData, "image/png"),
                new ResourceLinkBlock
                {
                    Name = "Design",
                    Uri = "https://example.test/design",
                },
                AudioContentBlock.FromBytes(binaryData, "audio/mpeg"),
                new TextContentBlock { Text = "after" },
            ],
        };

        var content = CopilotMcpToolAdapter.BuildResultContent(result);

        var beforeIndex = content.IndexOf("before", StringComparison.Ordinal);
        var imageIndex = content.IndexOf("MCP image result", StringComparison.Ordinal);
        var linkIndex = content.IndexOf(
            "MCP resource link: Design (https://example.test/design)",
            StringComparison.Ordinal);
        var audioIndex = content.IndexOf("MCP audio result", StringComparison.Ordinal);
        var afterIndex = content.IndexOf("after", StringComparison.Ordinal);
        Assert.True(beforeIndex >= 0 && beforeIndex < imageIndex);
        Assert.True(imageIndex < linkIndex);
        Assert.True(linkIndex < audioIndex);
        Assert.True(audioIndex < afterIndex);
        Assert.DoesNotContain(encodedData, content, StringComparison.Ordinal);
    }
}
