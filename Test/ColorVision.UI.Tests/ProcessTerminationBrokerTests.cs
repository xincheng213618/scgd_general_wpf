using ColorVision.UI.ServiceHost;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class ProcessTerminationBrokerTests
{
    [Fact]
    public async Task SendsGenericTargetIdentityAndWaitsForAnIssuedCommandAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var reply = new TaskCompletionSource<ServiceHostResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedStart = DateTime.UtcNow.AddMinutes(-5);
        var broker = new ProcessTerminationBroker((command, data, timeout, token) =>
        {
            Assert.Equal("process-terminate", command);
            var payload = JObject.FromObject(data!);
            Assert.Equal(123, payload.Value<int>("processId"));
            Assert.Equal(expectedStart.Ticks, payload.Value<long>("startTimeUtcTicks"));
            Assert.Equal(@"C:\Tools\AnyWorker.exe", payload.Value<string>("executablePath"));
            Assert.InRange(payload.Value<long>("notAfterUtcTicks"), DateTime.UtcNow.Ticks, DateTime.UtcNow.Add(timeout).Ticks);
            Assert.False(token.CanBeCanceled);
            return reply.Task;
        });
        Task operation = broker.TerminateAsync(123, expectedStart, @"C:\Tools\AnyWorker.exe", new Progress<string>(), cancellation.Token);
        cancellation.Cancel();
        Assert.False(operation.IsCompleted);
        reply.SetResult(new() { Success = true });
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OlderServiceUsesExistingSelfUpdateAndRetriesOnlyAfterCapabilityIsReady()
    {
        List<string> calls = [];
        var broker = new ProcessTerminationBroker((command, _, _, _) =>
        {
            calls.Add(command);
            return Task.FromResult(calls.Count == 1
                ? new ServiceHostResponse { Message = "Unsupported command: process-terminate" }
                : new ServiceHostResponse { Success = true, Data = JObject.FromObject(new { supportsProcessTermination = true }) });
        });
        await broker.TerminateAsync(123, DateTime.UtcNow, @"C:\Tools\Worker.exe", new Progress<string>(), CancellationToken.None);
        Assert.Equal(["process-terminate", "self-update", "status", "process-terminate"], calls);
    }

    [Theory]
    [InlineData("process_still_running", "仍未退出")]
    [InlineData("process_identity_mismatch", "身份")]
    [InlineData("process_request_expired", "过期")]
    [InlineData("process_target_not_allowed", "程序清单")]
    public async Task FailedServiceResponseProvidesTheReasonWithoutRetrying(string error, string expected)
    {
        int calls = 0;
        var broker = new ProcessTerminationBroker((_, _, _, _) => { calls++; return Task.FromResult(new ServiceHostResponse { Message = error }); });
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            broker.TerminateAsync(123, DateTime.UtcNow, @"C:\Tools\Worker.exe", new Progress<string>(), CancellationToken.None));
        Assert.Contains("PID 123", exception.Message);
        Assert.Contains(expected, exception.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancelBeforeDispatchDoesNotSendAnyCommand()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var broker = new ProcessTerminationBroker((_, _, _, _) => throw new InvalidOperationException("Must not send."));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => broker.TerminateAsync(
            123, DateTime.UtcNow, @"C:\Tools\Worker.exe", new Progress<string>(), cancellation.Token));
    }
}
