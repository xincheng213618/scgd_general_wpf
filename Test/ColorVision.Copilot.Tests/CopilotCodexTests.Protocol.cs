using System.IO;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotCodexTests
{
    [Fact]
    public async Task RpcRepliesDoNotConsumeOrReorderInterleavedNotifications()
    {
        using var input = new StringWriter();
        using var output = new CopilotBoundedTextLineReader(new StringReader("""
            {"method":"notification/first"}
            {"id":1,"result":{"stage":"first"}}
            {"method":"notification/second"}
            {"id":2,"result":{"stage":"second"}}
            {"method":"notification/third"}
            """), CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex protocol");
        var protocol = new CopilotCodexProtocol(input, output);

        var first = await protocol.RequestAsync("first", new { }, CancellationToken.None);
        var second = await protocol.RequestAsync("second", new { }, CancellationToken.None);
        Assert.Equal("first", first["stage"]!.GetValue<string>());
        Assert.Equal("second", second["stage"]!.GetValue<string>());
        foreach (var expected in new[] { "first", "second", "third" })
            Assert.Equal("notification/" + expected, (await protocol.ReadAsync(CancellationToken.None))["method"]!.GetValue<string>());
        var requests = input.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!).ToArray();
        Assert.Equal(new[] { 1, 2 }, requests.Select(request => request["id"]!.GetValue<int>()));
    }

    [Fact]
    public async Task CancelledReadDoesNotDeliverOrConsumeAQueuedNotification()
    {
        using var input = new StringWriter();
        using var output = new CopilotBoundedTextLineReader(new StringReader("""
            {"method":"item/tool/call","params":{"callId":"queued-tool"}}
            {"id":1,"result":{}}
            """), CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex protocol");
        var protocol = new CopilotCodexProtocol(input, output);
        await protocol.RequestAsync("turn/start", new { }, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => protocol.ReadAsync(cancellation.Token));
        var queued = await protocol.ReadAsync(CancellationToken.None);
        Assert.Equal("queued-tool", queued["params"]!["callId"]!.GetValue<string>());
    }

    [Fact]
    public async Task CancelledRpcDoesNotWriteARequestOrConsumeItsId()
    {
        using var input = new StringWriter();
        using var output = new CopilotBoundedTextLineReader(new StringReader("""{"id":1,"result":{"ok":true}}"""),
            CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex protocol");
        var protocol = new CopilotCodexProtocol(input, output);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => protocol.RequestAsync("cancelled", new { }, cancellation.Token));
        Assert.Empty(input.ToString());
        Assert.True((await protocol.RequestAsync("current", new { }, CancellationToken.None))["ok"]!.GetValue<bool>());
        Assert.Equal(1, JsonNode.Parse(input.ToString())!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task CancellationDuringACompletedWriteStopsBeforeReadingAReply()
    {
        using var cancellation = new CancellationTokenSource();
        using var input = new CancellingProtocolWriter(cancellation);
        using var rawOutput = new StringReader("""{"id":1,"result":{}}""");
        using var output = new CopilotBoundedTextLineReader(rawOutput, CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex protocol");
        var protocol = new CopilotCodexProtocol(input, output);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => protocol.RequestAsync("first", new { }, cancellation.Token));
        Assert.Equal('{', rawOutput.Peek());
        Assert.Equal(1, input.Writes);
    }

    [Theory]
    [InlineData("{\"id\":1,\"result\":{}}")]
    [InlineData("")]
    public async Task CancellationDuringALateSuccessfulReadRejectsBothReplyAndEof(string reply)
    {
        using var cancellation = new CancellationTokenSource();
        using var input = new StringWriter();
        using var output = new CopilotBoundedTextLineReader(new CancellingProtocolReader(reply, cancellation),
            CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex protocol");
        var protocol = new CopilotCodexProtocol(input, output);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => protocol.RequestAsync("first", new { }, cancellation.Token));
    }

    private sealed class CancellingProtocolWriter(CancellationTokenSource cancellation) : StringWriter
    {
        public int Writes { get; private set; }
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            Writes++;
            cancellation.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingProtocolReader(string text, CancellationTokenSource cancellation) : StringReader(text)
    {
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var read = base.ReadAsync(buffer, CancellationToken.None);
            cancellation.Cancel();
            return read;
        }
    }
}
