using ColorVisionServiceHost;
using Newtonsoft.Json;
using System.IO.Pipes;

namespace ColorVision.UI.Tests;

public class ServiceHostPipeServerTests
{
    [Fact]
    public async Task AdmissionCheckAndCommandRegistrationAreAtomicWithStop()
    {
        TaskCompletionSource acceptCheckPassed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseRegistration = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource commandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource closeReachedGateLock = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ServiceHostCommandAdmissionGate gate = new(
            _ => { },
            () =>
            {
                acceptCheckPassed.TrySetResult();
                releaseRegistration.Task.GetAwaiter().GetResult();
            },
            () => closeReachedGateLock.TrySetResult());
        Task<(bool Admitted, Task<int>? CommandTask)> admissionCall = Task.Factory.StartNew(
            () =>
            {
                bool admitted = gate.TryRun(
                    () =>
                    {
                        commandStarted.TrySetResult();
                        releaseCommand.Task.GetAwaiter().GetResult();
                        return 1;
                    },
                    out Task<int>? commandTask);
                return (admitted, commandTask);
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await acceptCheckPassed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<Task> stopCall = Task.Factory.StartNew(
            gate.CloseAndDrainAsync,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await closeReachedGateLock.Task.WaitAsync(TimeSpan.FromSeconds(2));
        bool stopReturnedBeforeRegistration = stopCall.IsCompleted;
        releaseRegistration.TrySetResult();
        (bool Admitted, Task<int>? CommandTask) admission = default;
        Task? drainTask = null;
        try
        {
            admission = await admissionCall.WaitAsync(TimeSpan.FromSeconds(2));
            drainTask = await stopCall.WaitAsync(TimeSpan.FromSeconds(2));
            await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(stopReturnedBeforeRegistration);
            Assert.True(admission.Admitted);
            Assert.NotNull(admission.CommandTask);
            Assert.False(drainTask.IsCompleted);
        }
        finally
        {
            releaseRegistration.TrySetResult();
            releaseCommand.TrySetResult();
            if (admission.CommandTask != null)
                await admission.CommandTask.WaitAsync(TimeSpan.FromSeconds(2));
            if (drainTask != null)
                await drainTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task StopCancelsClientStalledBeforeRequest()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (_, _) => throw new InvalidOperationException("No command expected."));
        Task runTask = server.RunAsync(CancellationToken.None);
        await using NamedPipeClientStream client = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(2000);

        Task stopTask = server.StopAsync();

        Assert.Same(runTask, stopTask);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task CancellationAtAdmissionBoundaryDoesNotInvokeHandler()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        TaskCompletionSource admissionReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseAdmission = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerCalls = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (request, _) =>
            {
                Interlocked.Increment(ref handlerCalls);
                return ServiceHostResponse.FromObject(request.RequestId, true, "unexpected");
            },
            beforeCommandAdmission: () =>
            {
                admissionReached.TrySetResult();
                return releaseAdmission.Task;
            });
        using CancellationTokenSource cancellation = new();
        Task runTask = server.RunAsync(cancellation.Token);
        await using NamedPipeClientStream client = await ConnectAndSendAsync(pipeName, runTask);
        await admissionReached.Task.WaitAsync(TimeSpan.FromSeconds(2));

        try
        {
            cancellation.Cancel();
            Assert.Equal(ServiceHostPipeServerState.Stopping, server.State);
        }
        finally
        {
            releaseAdmission.TrySetResult();
            await runTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(0, Volatile.Read(ref handlerCalls));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task StopStartsNoHandlerAfterAdmissionGateCloses()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        TaskCompletionSource firstCommandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondAdmissionReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecondAdmission = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int admissionCount = 0;
        int handlerCalls = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (request, _) =>
            {
                if (Interlocked.Increment(ref handlerCalls) == 1)
                {
                    firstCommandStarted.TrySetResult();
                    releaseFirstCommand.Task.GetAwaiter().GetResult();
                }
                return ServiceHostResponse.FromObject(request.RequestId, true, "completed");
            },
            beforeCommandAdmission: () =>
            {
                if (Interlocked.Increment(ref admissionCount) != 2)
                    return Task.CompletedTask;

                secondAdmissionReached.TrySetResult();
                return releaseSecondAdmission.Task;
            });
        Task runTask = server.RunAsync(CancellationToken.None);
        await using NamedPipeClientStream firstClient = await ConnectAndSendAsync(pipeName, runTask);
        await firstCommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await using NamedPipeClientStream secondClient = await ConnectAndSendAsync(pipeName, runTask);
        await secondAdmissionReached.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task stopTask = server.StopAsync();
        try
        {
            releaseSecondAdmission.TrySetResult();
            Assert.Equal(1, Volatile.Read(ref handlerCalls));
            Assert.False(stopTask.IsCompleted);
        }
        finally
        {
            releaseSecondAdmission.TrySetResult();
            releaseFirstCommand.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(1, Volatile.Read(ref handlerCalls));
    }

    [Fact]
    public async Task StopDoesNotCompleteUntilAdmittedCommandFinishes()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        TaskCompletionSource commandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int commandFinished = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (request, _) =>
            {
                try
                {
                    commandStarted.TrySetResult();
                    releaseCommand.Task.GetAwaiter().GetResult();
                    return ServiceHostResponse.FromObject(request.RequestId, true, "completed");
                }
                finally
                {
                    Volatile.Write(ref commandFinished, 1);
                }
            });
        Task runTask = server.RunAsync(CancellationToken.None);
        await using NamedPipeClientStream client = await ConnectAndSendAsync(pipeName, runTask);
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task stopTask = server.StopAsync();

        try
        {
            Assert.Same(runTask, stopTask);
            Assert.Equal(ServiceHostPipeServerState.Stopping, server.State);
            Assert.False(stopTask.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref commandFinished));
        }
        finally
        {
            releaseCommand.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(1, Volatile.Read(ref commandFinished));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task HandlerFaultIsObservedBeforeShutdownCompletes()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        InvalidOperationException marker = new("expected handler failure");
        TaskCompletionSource commandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Exception> observedFailure = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int reportCount = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (_, _) =>
            {
                commandStarted.TrySetResult();
                releaseCommand.Task.GetAwaiter().GetResult();
                throw marker;
            },
            reportCommandFailure: failure =>
            {
                Interlocked.Increment(ref reportCount);
                observedFailure.TrySetResult(failure);
            });
        Task runTask = server.RunAsync(CancellationToken.None);
        await using NamedPipeClientStream client = await ConnectAndSendAsync(pipeName, runTask);
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task stopTask = server.StopAsync();
        try
        {
            Assert.False(stopTask.IsCompleted);
        }
        finally
        {
            releaseCommand.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.True(observedFailure.Task.IsCompleted);
        Exception observed = await observedFailure.Task.WaitAsync(TimeSpan.FromSeconds(2));
        AggregateException aggregate = Assert.IsType<AggregateException>(observed);
        Assert.Contains(marker, aggregate.Flatten().InnerExceptions);
        Assert.Equal(1, Volatile.Read(ref reportCount));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task ConcurrentAndRepeatedStopReturnsSameCompletion()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        TaskCompletionSource commandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerCalls = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (request, _) =>
            {
                Interlocked.Increment(ref handlerCalls);
                commandStarted.TrySetResult();
                releaseCommand.Task.GetAwaiter().GetResult();
                return ServiceHostResponse.FromObject(request.RequestId, true, "completed");
            });
        Task runTask = server.RunAsync(CancellationToken.None);
        Assert.Same(runTask, server.RunAsync(CancellationToken.None));
        await using NamedPipeClientStream client = await ConnectAndSendAsync(pipeName, runTask);
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        const int callerCount = 4;
        using Barrier barrier = new(callerCount);
        Task<Task>[] stopCalls = Enumerable.Range(0, callerCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait();
                    return server.StopAsync();
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        Task[] stopTasks;
        try
        {
            stopTasks = await Task.WhenAll(stopCalls).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.All(stopTasks, stopTask => Assert.Same(runTask, stopTask));
            Assert.Equal(ServiceHostPipeServerState.Stopping, server.State);
            Assert.False(runTask.IsCompleted);
        }
        finally
        {
            releaseCommand.TrySetResult();
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Task repeatedStop = server.StopAsync();
        Assert.Same(runTask, repeatedStop);
        await repeatedStop;
        Assert.Equal(1, Volatile.Read(ref handlerCalls));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task StopBeforeRunReturnsSameStoppedCompletion()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (_, _) => throw new InvalidOperationException("No command expected."));

        Task stopTask = server.StopAsync();
        Task runTask = server.RunAsync(CancellationToken.None);

        Assert.Same(stopTask, runTask);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(stopTask, server.StopAsync());
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task ConcurrentRunAndStopReturnSameCompletion()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (_, _) => throw new InvalidOperationException("No command expected."));
        using Barrier barrier = new(2);
        Task<Task> runCall = Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                return server.RunAsync(CancellationToken.None);
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Task<Task> stopCall = Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                return server.StopAsync();
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Task[] returnedTasks = await Task
            .WhenAll(runCall, stopCall)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(returnedTasks[0], returnedTasks[1]);
        await returnedTasks[0].WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public async Task OverBudgetWaitKeepsStoppingUntilAdmittedCommandCompletes()
    {
        string pipeName = $"ColorVisionServiceHostTest-{Guid.NewGuid():N}";
        TaskCompletionSource commandStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCommand = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int handlerCalls = 0;
        using ServiceHostPipeServer server = CreateServer(
            pipeName,
            (request, _) =>
            {
                Interlocked.Increment(ref handlerCalls);
                commandStarted.TrySetResult();
                releaseCommand.Task.GetAwaiter().GetResult();
                return ServiceHostResponse.FromObject(request.RequestId, true, "completed");
            });
        Task runTask = server.RunAsync(CancellationToken.None);
        await using NamedPipeClientStream client = await ConnectAndSendAsync(pipeName, runTask);
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task stopTask = server.StopAsync();
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();
        bool overBudgetReported = false;

        try
        {
            bool overBudget = ColorVisionServiceHostService.WaitForShutdown(
                stopTask,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(4),
                15000,
                _ => { },
                _ =>
                {
                    overBudgetReported = true;
                    Assert.Equal(ServiceHostPipeServerState.Stopping, server.State);
                    Assert.False(stopTask.IsCompleted);
                    Assert.Throws<InvalidOperationException>(server.Dispose);
                    Assert.Same(stopTask, server.StopAsync());
                    Assert.Equal(1, Volatile.Read(ref handlerCalls));
                    releaseCommand.TrySetResult();
                },
                timeProvider,
                stopStartedTimestamp,
                (task, waitDuration) =>
                {
                    if (overBudgetReported)
                        return task.Wait(TimeSpan.FromSeconds(2));

                    timeProvider.Advance(waitDuration);
                    return task.IsCompleted;
                });

            Assert.True(overBudget);
        }
        finally
        {
            releaseCommand.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        int additionalTimeRequests = 0;
        ColorVisionServiceHostService.WaitForShutdown(
            stopTask,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4),
            15000,
            _ => Interlocked.Increment(ref additionalTimeRequests),
            _ => throw new InvalidOperationException("Completed shutdown must not report an overrun."),
            timeProvider,
            stopStartedTimestamp);
        Assert.Equal(0, Volatile.Read(ref additionalTimeRequests));
        Assert.Same(stopTask, server.StopAsync());
        Assert.Equal(ServiceHostPipeServerState.Stopped, server.State);
    }

    [Fact]
    public void ServiceWaitRequestsAdditionalTimeUntilShutdownCompletes()
    {
        TaskCompletionSource shutdown = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();
        List<TimeSpan> waitDurations = [];
        int requestCount = 0;

        ColorVisionServiceHostService.WaitForShutdown(
            shutdown.Task,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2),
            5000,
            _ =>
            {
                if (Interlocked.Increment(ref requestCount) == 2)
                    shutdown.TrySetResult();
            },
            _ => throw new InvalidOperationException("Shutdown completed within budget."),
            timeProvider,
            stopStartedTimestamp,
            (task, waitDuration) =>
            {
                waitDurations.Add(waitDuration);
                timeProvider.Advance(waitDuration);
                return task.IsCompleted;
            });

        Assert.Equal(2, Volatile.Read(ref requestCount));
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, waitDurations);
    }

    [Fact]
    public void AdditionalTimeFailureDoesNotResetOrExtendNormalStopBudget()
    {
        TaskCompletionSource shutdown = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();
        List<int> waitHints = [];
        List<TimeSpan> waitDurations = [];
        int overBudgetReports = 0;

        bool overBudget = ColorVisionServiceHostService.WaitForShutdown(
            shutdown.Task,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4),
            15000,
            waitHintMilliseconds =>
            {
                waitHints.Add(waitHintMilliseconds);
                timeProvider.Advance(TimeSpan.FromSeconds(1));
                throw new InvalidOperationException("expected wait-hint failure");
            },
            _ =>
            {
                Interlocked.Increment(ref overBudgetReports);
                shutdown.TrySetResult();
            },
            timeProvider,
            stopStartedTimestamp,
            (task, waitDuration) =>
            {
                waitDurations.Add(waitDuration);
                timeProvider.Advance(waitDuration);
                return task.IsCompleted;
            });

        Assert.True(overBudget);
        Assert.Equal(1, Volatile.Read(ref overBudgetReports));
        Assert.True(shutdown.Task.IsCompletedSuccessfully);
        Assert.Collection(
            waitHints,
            waitHint => Assert.Equal(10000, waitHint),
            waitHint => Assert.Equal(5000, waitHint));
        Assert.Equal(
            new[] { TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4) },
            waitDurations);
        Assert.Equal(
            TimeSpan.FromSeconds(10),
            timeProvider.GetElapsedTime(stopStartedTimestamp));
    }

    [Fact]
    public void WaitUsesOnlyRemainingBudgetFromOnStopEntry()
    {
        TaskCompletionSource shutdown = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        List<int> waitHints = [];
        List<TimeSpan> waitDurations = [];
        int overBudgetReports = 0;

        bool overBudget = ColorVisionServiceHostService.WaitForShutdown(
            shutdown.Task,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4),
            15000,
            waitHints.Add,
            _ =>
            {
                Interlocked.Increment(ref overBudgetReports);
                shutdown.TrySetResult();
            },
            timeProvider,
            stopStartedTimestamp,
            (task, waitDuration) =>
            {
                waitDurations.Add(waitDuration);
                timeProvider.Advance(waitDuration);
                return task.IsCompleted;
            });

        Assert.True(overBudget);
        Assert.Equal(1, Volatile.Read(ref overBudgetReports));
        Assert.Collection(
            waitHints,
            waitHint => Assert.Equal(7000, waitHint),
            waitHint => Assert.Equal(3000, waitHint));
        Assert.Equal(
            new[] { TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3) },
            waitDurations);
        Assert.Equal(
            TimeSpan.FromSeconds(10),
            timeProvider.GetElapsedTime(stopStartedTimestamp));
    }

    [Fact]
    public void CompletionAtNormalStopBudgetBoundaryIsNotReportedAsOverBudget()
    {
        TaskCompletionSource shutdown = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();

        ColorVisionServiceHostService.WaitForShutdown(
            shutdown.Task,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            15000,
            _ => { },
            _ => throw new InvalidOperationException("Boundary completion is not over budget."),
            timeProvider,
            stopStartedTimestamp,
            (_, waitDuration) =>
            {
                Assert.Equal(TimeSpan.FromSeconds(3), waitDuration);
                timeProvider.Advance(waitDuration);
                shutdown.TrySetResult();
                return false;
            });

        Assert.True(shutdown.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public void ServiceWaitPropagatesShutdownFailure()
    {
        InvalidOperationException marker = new("shutdown failed");
        ManualTimeProvider timeProvider = new();
        long stopStartedTimestamp = timeProvider.GetTimestamp();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => ColorVisionServiceHostService.WaitForShutdown(
                Task.FromException(marker),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(1),
                100,
                _ => { },
                _ => { },
                timeProvider,
                stopStartedTimestamp));

        Assert.Same(marker, thrown);
    }

    private static ServiceHostPipeServer CreateServer(
        string pipeName,
        Func<ServiceHostRequest, ServiceHostRequestContext, ServiceHostResponse> handler,
        Func<Task>? beforeCommandAdmission = null,
        Action<Exception>? reportCommandFailure = null)
    {
        return new ServiceHostPipeServer(
            handler,
            ResolveTestCaller,
            pipeName,
            () => new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous),
            beforeCommandAdmission,
            reportCommandFailure);
    }

    private static bool ResolveTestCaller(
        NamedPipeServerStream pipe,
        out ServiceHostRequestContext context,
        out string error)
    {
        context = new ServiceHostRequestContext();
        error = string.Empty;
        return true;
    }

    private static async Task<NamedPipeClientStream> ConnectAndSendAsync(
        string pipeName,
        Task runTask)
    {
        NamedPipeClientStream client = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(2000);
        string requestJson = JsonConvert.SerializeObject(
            new ServiceHostRequest { Command = "long-command" },
            ServiceHostJson.Settings);
        byte[] requestBytes = ServiceHostJson.Encoding.GetBytes(
            requestJson + Environment.NewLine);
        try
        {
            await client.WriteAsync(requestBytes);
            await client.FlushAsync();
        }
        catch when (runTask.IsFaulted)
        {
            await runTask;
            throw;
        }
        return client;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref _timestamp);
        }

        public void Advance(TimeSpan duration)
        {
            Interlocked.Add(ref _timestamp, duration.Ticks);
        }
    }
}
