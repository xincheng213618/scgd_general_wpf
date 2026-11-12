using ColorVision.Common.Utilities;
using System.Timers;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class DebounceTimerTests
{
    [Fact]
    public void ResetKeepsOnlyLatestActionAndIgnoresExpiredCallbacks()
    {
        DebounceScheduler scheduler = new();
        List<string> calls = [];
        ManualTimer first = AddTimer(scheduler, "key", () => calls.Add("first"));
        ManualTimer second = AddTimer(scheduler, "key", () => calls.Add("second"));
        ManualTimer latest = AddTimer(scheduler, "key", () => calls.Add("latest"));

        first.Fire();
        second.Fire();
        Assert.Empty(calls);
        Assert.Equal(1, first.StartCount);
        Assert.Equal(1, second.StartCount);
        Assert.Equal(1, latest.StartCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(0, latest.DisposeCount);

        latest.Fire();
        latest.Fire();
        scheduler.Cancel("key");
        Assert.Equal(["latest"], calls);
        Assert.Equal(1, latest.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantResetSurvivesOriginalCallbackCompletionOrFailure(bool throwAfterReset)
    {
        DebounceScheduler scheduler = new();
        List<string> calls = [];
        ManualTimer? replacement = null;
        InvalidOperationException failure = new("Callback failure");
        ManualTimer original = AddTimer(scheduler, "key", () =>
        {
            calls.Add("original");
            replacement = AddTimer(scheduler, "key", () => calls.Add("replacement"));
            if (throwAfterReset) throw failure;
        });

        if (throwAfterReset) Assert.Same(failure, Assert.Throws<InvalidOperationException>(original.Fire));
        else original.Fire();

        Assert.Equal(1, original.DisposeCount);
        Assert.NotNull(replacement);
        Assert.Equal(0, replacement.DisposeCount);
        original.Fire();
        replacement.Fire();
        Assert.Equal(["original", "replacement"], calls);
        Assert.Equal(1, replacement.DisposeCount);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Reset")]
    [InlineData("DisposeAll")]
    public void CancelResetOrDisposeAllInvalidatesAnActionAlreadyQueuedOnDispatcher(string invalidation)
    {
        WpfTestHost.Invoke(() =>
        {
            DebounceScheduler scheduler = new();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            List<string> calls = [];
            Action<Action, Action> dispatch = (execute, abandon) => DebounceTimer.QueueOnDispatcher(dispatcher, execute, abandon);
            ManualTimer original = AddTimer(scheduler, "key", () => calls.Add("original"), dispatch);
            original.Fire();
            Assert.Empty(calls);
            Assert.Equal(0, original.DisposeCount);

            ManualTimer? replacement = null;
            bool reset = invalidation == "Reset";
            if (reset)
            {
                replacement = AddTimer(scheduler, "key", () => calls.Add("replacement"), dispatch);
                replacement.Fire();
            }
            else if (invalidation == "DisposeAll") scheduler.DisposeAll();
            else scheduler.Cancel("key");

            Assert.Empty(calls);
            Assert.Equal(1, original.DisposeCount);
            DrainDispatcher(dispatcher);
            Assert.Equal(reset ? new[] { "replacement" } : Array.Empty<string>(), calls);
            Assert.Equal(1, original.DisposeCount);
            if (replacement != null) Assert.Equal(1, replacement.DisposeCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingDispatcherOrAbortedOperationAbandonsAndReleasesItsTimer(bool abortQueuedOperation)
    {
        WpfTestHost.Invoke(() =>
        {
            DebounceScheduler scheduler = new();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            int calls = 0;
            ManualTimer timer = AddTimer(scheduler, "key", () => calls++,
                (execute, abandon) => DebounceTimer.QueueOnDispatcher(abortQueuedOperation ? dispatcher : null, execute, abandon));

            if (abortQueuedOperation)
            {
                int postingThread = Environment.CurrentManagedThreadId;
                List<DispatcherOperation> posted = [];
                void CapturePostedOperation(object? sender, DispatcherHookEventArgs args)
                {
                    if (Environment.CurrentManagedThreadId == postingThread) posted.Add(args.Operation);
                }

                dispatcher.Hooks.OperationPosted += CapturePostedOperation;
                try { timer.Fire(); }
                finally { dispatcher.Hooks.OperationPosted -= CapturePostedOperation; }

                DispatcherOperation operation = Assert.Single(posted);
                Assert.Equal(DispatcherOperationStatus.Pending, operation.Status);
                Assert.True(operation.Abort());
                DrainDispatcher(dispatcher);
            }
            else timer.Fire();

            Assert.Equal(0, calls);
            Assert.Equal(1, timer.DisposeCount);
            timer.Fire();
            ManualTimer replacement = AddTimer(scheduler, "key", () => calls++);
            replacement.Fire();
            Assert.Equal(1, calls);
            Assert.Equal(1, replacement.DisposeCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryOrStartFailurePreservesThePreviousEntryAndReleasesAnyNewTimer(bool failDuringFactory)
    {
        DebounceScheduler scheduler = new();
        List<string> calls = [];
        ManualTimer previous = AddTimer(scheduler, "key", () => calls.Add("previous"));
        ManualTimer? failedTimer = null;
        InvalidOperationException failure = new("Unable to create or start timer");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => scheduler.AddOrReset("key", fire =>
        {
            if (failDuringFactory) throw failure;
            return failedTimer = new ManualTimer(fire, () => calls.Add("failed")) { StartFailure = failure };
        })));

        if (failDuringFactory) Assert.Null(failedTimer);
        else
        {
            Assert.NotNull(failedTimer);
            Assert.Equal(1, failedTimer.StartCount);
            Assert.Equal(1, failedTimer.DisposeCount);
            failedTimer.Fire();
        }
        Assert.Equal(0, previous.DisposeCount);
        previous.Fire();
        Assert.Equal(["previous"], calls);
        Assert.Equal(1, previous.DisposeCount);
    }

    [Fact]
    public void DisposeAllInvalidatesOldCallbacksAndAllowsNewEntries()
    {
        DebounceScheduler scheduler = new();
        List<string> calls = [];
        ManualTimer first = AddTimer(scheduler, "first", () => calls.Add("first"));
        ManualTimer second = AddTimer(scheduler, "second", () => calls.Add("second"));

        scheduler.DisposeAll();
        scheduler.DisposeAll();
        first.Fire();
        second.Fire();
        Assert.Empty(calls);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);

        ManualTimer replacement = AddTimer(scheduler, "first", () => calls.Add("replacement"));
        replacement.Fire();
        Assert.Equal(["replacement"], calls);
        Assert.Equal(1, replacement.DisposeCount);
    }

    [Fact]
    public async Task BlockedActionDoesNotLockActionsForOtherKeys()
    {
        DebounceScheduler scheduler = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int secondCalls = 0;
        ManualTimer first = AddTimer(scheduler, "first", () =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        });
        ManualTimer second = AddTimer(scheduler, "second", () => secondCalls++);
        Task firstCallback = Task.Run(first.Fire);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(second.Fire).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, secondCalls);
            Assert.Equal(1, second.DisposeCount);
        }
        finally
        {
            release.TrySetResult();
            await firstCallback.WaitAsync(TimeSpan.FromSeconds(5));
            scheduler.DisposeAll();
        }
        Assert.Equal(1, first.DisposeCount);
    }

    [Fact]
    public async Task ElapsedHandlerReceivesTheRealTimerAndElapsedArguments()
    {
        string key = $"{nameof(ElapsedHandlerReceivesTheRealTimerAndElapsedArguments)}-{Guid.NewGuid():N}";
        TaskCompletionSource<(object? Sender, ElapsedEventArgs Args)> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ElapsedEventHandler handler = (sender, args) => completion.TrySetResult((sender, args));
        try
        {
            DebounceTimer.AddOrResetTimer(key, 10, handler);
            (object? sender, ElapsedEventArgs args) = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<System.Timers.Timer>(sender);
            Assert.NotNull(args);
            Assert.NotEqual(default, args.SignalTime);
        }
        finally { DebounceTimer.Cancel(key); }
    }

    private static ManualTimer AddTimer(DebounceScheduler scheduler, string key, Action action, Action<Action, Action>? dispatch = null)
    {
        ManualTimer? timer = null;
        scheduler.AddOrReset(key, fire => timer = new ManualTimer(fire, action), dispatch);
        return timer!;
    }

    private static void DrainDispatcher(Dispatcher dispatcher)
    {
        DispatcherFrame frame = new();
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class ManualTimer(Action<Action> fire, Action action) : IDebounceTimer
    {
        internal int StartCount { get; private set; }
        internal int DisposeCount { get; private set; }
        internal Exception? StartFailure { get; init; }

        public void Start()
        {
            StartCount++;
            if (StartFailure != null) throw StartFailure;
        }

        // A callback already queued by a timer can arrive after that timer was disposed.
        internal void Fire() => fire(action);

        public void Dispose() => DisposeCount++;
    }
}
