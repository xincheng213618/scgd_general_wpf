using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCheckpointingChatHistoryProviderTests
{
    [Fact]
    public void CapturePreservesProviderCommitOrderAndDeduplicatesEntries()
    {
        var requestMessages = new[]
        {
            new ChatMessage(
                ChatRole.Tool,
                [
                    new FunctionResultContent("previous-call", "done"),
                    new FunctionResultContent("previous-call", "duplicate"),
                ]),
        };
        var responseMessages = new[]
        {
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("next-call", "next_tool", new Dictionary<string, object?>()),
                    new FunctionCallContent("next-call", "next_tool", new Dictionary<string, object?>()),
                    new FunctionCallContent("ignored-call", "ignored_tool", new Dictionary<string, object?>())
                    {
                        InformationalOnly = true,
                    },
                    new FunctionCallContent("server-call", "server_tool", new Dictionary<string, object?>()),
                    new FunctionResultContent("server-call", "already handled"),
                ]),
        };

        var delta = CopilotProviderToolHistoryDelta.Capture(
            requestMessages,
            responseMessages);

        Assert.Collection(
            delta.Entries,
            entry => AssertEntry(entry, CopilotProviderToolHistoryEntryKind.Result, "previous-call", string.Empty),
            entry => AssertEntry(entry, CopilotProviderToolHistoryEntryKind.Call, "next-call", "next_tool"),
            entry => AssertEntry(entry, CopilotProviderToolHistoryEntryKind.Call, "server-call", "server_tool"),
            entry => AssertEntry(entry, CopilotProviderToolHistoryEntryKind.Result, "server-call", string.Empty));
    }

    private static void AssertEntry(
        CopilotProviderToolHistoryEntry entry,
        CopilotProviderToolHistoryEntryKind kind,
        string callId,
        string toolName)
    {
        Assert.Equal(kind, entry.Kind);
        Assert.Equal(callId, entry.CallId);
        Assert.Equal(toolName, entry.ToolName);
    }
}
