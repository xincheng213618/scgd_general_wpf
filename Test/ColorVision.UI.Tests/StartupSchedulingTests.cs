using ColorVision.Startup;
using ColorVision.UI.Desktop.Operations;
using System.Collections.Concurrent;

namespace ColorVision.UI.Tests;

public sealed class StartupSchedulingTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DeclaredExtensionDoesNotSerializeIndependentPrerequisites()
    {
        var database = Signal();
        var connectivity = Signal();
        var finishDatabase = Signal();
        var calls = new ConcurrentQueue<string>();
        IInitializer[] steps =
        [
            new Declared("database", [], async () => { database.SetResult(); await finishDatabase.Task; calls.Enqueue("database"); }),
            new Declared("extension", ["database"], () => { calls.Enqueue("extension"); return Task.CompletedTask; }),
            new Declared("connectivity", [], () => { connectivity.SetResult(); return Task.CompletedTask; }),
            new Declared("workspace", ["database", "extension", "connectivity"], () => { calls.Enqueue("workspace"); return Task.CompletedTask; }),
        ];
        Task<IReadOnlyList<StartupInitializerResult>> run = StartupInitializerRunner.RunAsync(steps);
        try
        {
            await Task.WhenAll(database.Task, connectivity.Task).WaitAsync(Timeout);
            Assert.Empty(calls);
        }
        finally { finishDatabase.TrySetResult(); }
        Assert.All(await run.WaitAsync(Timeout), result => Assert.True(result.Succeeded));
        Assert.Equal(["database", "extension", "workspace"], calls.ToArray());
    }

    [Fact]
    public async Task LegacyBarrierWaitsForAllPrecedingWorkAndBlocksLaterWork()
    {
        var started = Signal();
        var release = Signal();
        var calls = new ConcurrentQueue<string>();
        IInitializer[] steps =
        [
            new Declared("first", [], async () => { started.SetResult(); await release.Task; calls.Enqueue("first"); }),
            new Legacy("legacy", () => { calls.Enqueue("legacy"); return Task.CompletedTask; }),
            new Declared("last", [], () => { calls.Enqueue("last"); return Task.CompletedTask; }),
        ];
        var run = StartupInitializerRunner.RunAsync(steps);
        try { await started.Task.WaitAsync(Timeout); Assert.Empty(calls); }
        finally { release.TrySetResult(); }
        await run.WaitAsync(Timeout);
        Assert.Equal(["first", "legacy", "last"], calls.ToArray());
    }

    [Fact]
    public async Task InvalidDependencyPlanIsRejectedBeforeAnySideEffect()
    {
        int calls = 0;
        Task Initialize() { calls++; return Task.CompletedTask; }
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupInitializerRunner.RunAsync(
            [new Declared("a", ["b"], Initialize), new Declared("b", ["a"], Initialize)]));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task FailureIsQueryableWhileIndependentAndOfflineCompatibleWorkContinues()
    {
        var error = new InvalidOperationException("Synthetic failure");
        var results = await StartupInitializerRunner.RunAsync(
        [
            new Declared("connection", [], () => Task.FromException(error)),
            new Declared("offline-workspace", ["connection", "explicitly-skipped"], () => Task.CompletedTask),
        ]);
        Assert.Same(error, results[0].Error);
        Assert.True(results[1].Succeeded);
    }

    [Fact]
    public async Task CancellationPreventsDependentWorkAfterTheRunningStepFinishes()
    {
        using var cancellation = new CancellationTokenSource();
        var started = Signal();
        var release = Signal();
        bool dependentStarted = false;
        Task run = StartupInitializerRunner.RunAsync(
        [
            new Declared("running", [], async () => { started.SetResult(); await release.Task; }),
            new Legacy("dependent", () => { dependentStarted = true; return Task.CompletedTask; }),
        ], cancellationToken: cancellation.Token);
        try
        {
            await started.Task.WaitAsync(Timeout);
            cancellation.Cancel();
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Timeout));
        Assert.False(dependentStarted);
    }

    [Fact]
    public async Task ExitDuringWatchdogHandshakeReturnsWithoutWaitingAndCannotReactivate()
    {
        var started = Signal();
        var allowReturn = Signal();
        var session = new FailureWatchdogSession(
            () => new EventWaitHandle(false, EventResetMode.ManualReset),
            cleanExit =>
            {
                started.SetResult();
                Assert.True(cleanExit.WaitOne(Timeout));
                allowReturn.Task.GetAwaiter().GetResult();
                return true; // Simulate ready racing with shutdown.
            });
        Task<bool> first = session.StartAsync();
        Assert.Same(first, session.StartAsync());
        try
        {
            await started.Task.WaitAsync(Timeout);
            session.SignalCleanExit();
            Assert.False(first.IsCompleted);
            Assert.False(session.Active);
            Assert.False(await session.StartAsync());
        }
        finally { allowReturn.TrySetResult(); session.SignalCleanExit(); }
        Assert.False(await first.WaitAsync(Timeout));
        Assert.False(session.Active);
    }

    [Fact]
    public async Task ExitBeforeWatchdogStartsDoesNotLaunchAProcess()
    {
        int launches = 0;
        var session = new FailureWatchdogSession(() => new(false, EventResetMode.ManualReset), _ => { launches++; return true; });
        session.SignalCleanExit();
        Assert.False(await session.StartAsync());
        Assert.Equal(0, launches);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadinessRequiresBothRenderingAndRequiredInitialization(bool renderedFirst)
    {
        var session = new StartupSession();
        session.AddResults([new StartupInitializerResult("failed-component", TimeSpan.Zero, new Exception("Synthetic"))]);
        if (renderedFirst) session.MarkFirstFrame();
        else session.MarkInitializationCompleted();
        Assert.False(session.TryPublishCompletion());
        if (renderedFirst) session.MarkInitializationCompleted();
        else session.MarkFirstFrame();
        Assert.Equal(StartupReadiness.Degraded, session.Readiness);
        Assert.True(session.TryPublishCompletion());
        Assert.False(session.TryPublishCompletion());
    }

    private class Legacy(string name, Func<Task> initialize) : InitializerBase
    {
        public override string Name => name;
        public override Task InitializeAsync() => initialize();
    }
    private sealed class Declared(string name, string[] dependencies, Func<Task> initialize)
        : Legacy(name, initialize), IInitializerDependencies
    {
        public IReadOnlyCollection<string> Dependencies => dependencies;
    }
}
