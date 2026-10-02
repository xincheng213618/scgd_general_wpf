using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class FlowPerformanceSamplerTests
{
    private const long MiB = 1048576;

    [Fact]
    public void FirstSampleReportsGaugesWithoutInventingZeroRates()
    {
        var sampler = new FlowPerformanceSampler(() => new() { ProcessorCount = 4, PrivateBytes = 800 * MiB }, new TestClock());
        Assert.True(sampler.TrySample(out var sample));
        Assert.Equal(800, sample!.PrivateMiB);
        Assert.Null(sample.WindowSeconds);
        Assert.Null(sample.ProcessCpuPercent);
        Assert.Null(sample.SystemCpuPercent);
        Assert.Null(sample.ProcessIoWriteMiBPerSecond);
        Assert.Null(sample.Gen0Collections);
    }

    [Fact]
    public void RatesUseTheSamplingWindowAndNormalizeProcessCpuAcrossProcessors()
    {
        var start = new FlowPerformanceReading
        {
            ProcessorCount = 4, ProcessCpuTicks = TimeSpan.FromSeconds(10).Ticks,
            SystemCpu = new(1000, 5000, 3000), IoReadBytes = 10, IoWriteBytes = 20,
            AllocatedBytes = 1024, Gen0 = 10, Gen1 = 5, Gen2 = 3,
        };
        var end = start with
        {
            ProcessCpuTicks = TimeSpan.FromSeconds(16).Ticks, SystemCpu = new(2000, 7500, 4500),
            IoReadBytes = 10 + 30 * (ulong)MiB, IoWriteBytes = 20 + 60 * (ulong)MiB,
            AllocatedBytes = 1024 + 30 * MiB, Gen0 = 13, Gen1 = 6, Gen2 = 3,
        };
        var sample = FlowPerformanceMetrics.Create(end, start, 30, 1.5);
        Assert.Equal(5, sample.ProcessCpuPercent);
        Assert.Equal(75, sample.SystemCpuPercent); // Kernel time already includes idle time.
        Assert.Equal(1, sample.ProcessIoReadMiBPerSecond);
        Assert.Equal(2, sample.ProcessIoWriteMiBPerSecond);
        Assert.Equal(1, sample.AllocatedMiBPerSecond);
        Assert.Equal(3, sample.Gen0Collections);
        Assert.Equal(1, sample.Gen1Collections);
        Assert.Equal(0, sample.Gen2Collections);
    }

    [Fact]
    public void FrequentCompletionsDoNotReadProcessCountersAgain()
    {
        int reads = 0;
        var clock = new TestClock();
        var sampler = new FlowPerformanceSampler(() => { reads++; return new(); }, clock);
        Assert.True(sampler.TrySample(out _));
        clock.Advance(TimeSpan.FromSeconds(29));
        for (int i = 0; i < 100; i++) Assert.False(sampler.TrySample(out _));
        Assert.Equal(1, reads);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(sampler.TrySample(out var sample));
        Assert.Equal(30, sample!.WindowSeconds);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void FailedReadsAreThrottledAndDoNotInterruptExecution()
    {
        int reads = 0;
        var clock = new TestClock();
        var sampler = new FlowPerformanceSampler(() =>
        {
            if (++reads == 1) throw new InvalidOperationException("counter unavailable");
            return new();
        }, clock);
        Assert.False(sampler.TrySample(out _));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(sampler.TrySample(out _));
        Assert.Equal(1, reads);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(sampler.TrySample(out var sample));
        Assert.Null(sample!.WindowSeconds);
    }

    [Fact]
    public async Task AnotherCompletionSkipsSamplingWhileAReadIsInProgress()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var sampler = new FlowPerformanceSampler(() =>
        {
            entered.Set();
            release.Wait();
            return new();
        }, new TestClock());
        Task<bool> sampling = Task.Run(() => sampler.TrySample(out _));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(sampler.TrySample(out _));
        }
        finally { release.Set(); }
        Assert.True(await sampling);
    }

    [Fact]
    public void MissingOrResetCountersRemainUnknown()
    {
        var previous = new FlowPerformanceReading { IoWriteBytes = 10, ProcessCpuTicks = 100, SystemCpu = new(50, 100, 100) };
        var current = new FlowPerformanceReading { IoWriteBytes = 1, ProcessCpuTicks = 1, SystemCpu = new(1, 1, 1) };
        var sample = FlowPerformanceMetrics.Create(current, previous, 30, 0);
        Assert.Null(sample.ProcessIoReadMiBPerSecond);
        Assert.Null(sample.ProcessIoWriteMiBPerSecond);
        Assert.Null(sample.ProcessCpuPercent);
        Assert.Null(sample.SystemCpuPercent);
    }

    [Fact]
    public void CurrentWindowsProcessCanBeSampledWithoutStartingHardware()
    {
        var sampler = new FlowPerformanceSampler(FlowPerformanceSampler.ReadCurrent, TimeProvider.System);
        Assert.True(sampler.TrySample(out var sample));
        Assert.True(sample!.WorkingSetMiB > 0);
        Assert.True(sample.PrivateMiB > 0);
        Assert.Equal(Environment.ProcessorCount, sample.ProcessorCount);
    }

    [Fact]
    public void CompletionOnlyQueuesWorkAndPreservesContextUntilTheActualSample()
    {
        int reads = 0;
        var clock = new TestClock();
        var samples = new List<FlowPerformanceSample>();
        var jobs = new Queue<Action>();
        var sampler = new FlowPerformanceSampler(() => { reads++; return new(); }, clock);
        var worker = new FlowPerformanceSamplingWorker(sampler, clock, samples.Add, job => { jobs.Enqueue(job); return true; });

        Assert.True(worker.TryQueue("SN-first", "White51", 42, "start"));
        Assert.Equal(0, reads);
        Assert.Empty(samples);
        clock.Advance(TimeSpan.FromSeconds(45));
        for (int i = 0; i < 100; i++) Assert.False(worker.TryQueue("SN-later", "White255", 43, "other"));
        Assert.Single(jobs);

        jobs.Dequeue()();
        var sample = Assert.Single(samples);
        Assert.Equal(1, reads);
        Assert.Equal("SN-first", sample.Request.SerialNumber);
        Assert.Equal("White51", sample.Request.FlowName);
        Assert.Equal(42, sample.Request.BatchId);
        Assert.Equal("start", sample.Request.StartNodeName);
        Assert.Equal(45000, sample.QueueDelayMs);
        Assert.Equal(TimeSpan.FromSeconds(45), sample.SampledAt - sample.Request.RequestedAt);
        Assert.Null(sample.Metrics.WindowSeconds);
    }

    [Fact]
    public void DelayedWorkerKeepsTheIntervalAndRatesAtTheActualReadTimes()
    {
        int reads = 0;
        var clock = new TestClock();
        var samples = new List<FlowPerformanceSample>();
        var jobs = new Queue<Action>();
        var sampler = new FlowPerformanceSampler(() => new() { AllocatedBytes = ++reads * 30 * MiB }, clock);
        var worker = new FlowPerformanceSamplingWorker(sampler, clock, samples.Add, job => { jobs.Enqueue(job); return true; });

        Assert.True(worker.TryQueue("first", null, null, null));
        clock.Advance(TimeSpan.FromSeconds(45));
        jobs.Dequeue()();
        Assert.False(worker.TryQueue("too-soon", null, null, null));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(worker.TryQueue("still-too-soon", null, null, null));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(worker.TryQueue("second", null, null, null));
        jobs.Dequeue()();
        Assert.Equal(2, reads);
        Assert.Equal(30, samples[1].Metrics.WindowSeconds);
        Assert.Equal(1, samples[1].Metrics.AllocatedMiBPerSecond);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedOrFailedSchedulingIsThrottledAndReleasesTheSlot(bool throws)
    {
        int attempts = 0, reads = 0;
        var clock = new TestClock();
        var samples = new List<FlowPerformanceSample>();
        var jobs = new Queue<Action>();
        var worker = new FlowPerformanceSamplingWorker(new(() => { reads++; return new(); }, clock), clock, samples.Add, job =>
        {
            if (++attempts == 1)
            {
                if (throws) throw new InvalidOperationException("scheduler unavailable");
                return false;
            }
            jobs.Enqueue(job);
            return true;
        });
        Assert.False(worker.TryQueue("rejected", null, null, null));
        Assert.False(worker.TryQueue("no-retry-storm", null, null, null));
        Assert.Equal(1, attempts);
        Assert.Equal(0, reads);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(worker.TryQueue("retry", null, null, null));
        jobs.Dequeue()();
        Assert.Equal(1, reads);
        Assert.Equal("retry", Assert.Single(samples).Request.SerialNumber);
    }

    [Fact]
    public void BackgroundReadAndLogFailuresReleaseTheSlotWithoutAnImmediateRetryStorm()
    {
        int reads = 0, writes = 0;
        var clock = new TestClock();
        var jobs = new Queue<Action>();
        var sampler = new FlowPerformanceSampler(() =>
        {
            if (++reads == 1) throw new InvalidOperationException("counter unavailable");
            return new();
        }, clock);
        var worker = new FlowPerformanceSamplingWorker(sampler, clock, sample =>
        {
            if (++writes == 1) throw new InvalidOperationException("appender unavailable");
        }, job => { jobs.Enqueue(job); return true; });

        Assert.True(worker.TryQueue("bad-read", null, null, null));
        jobs.Dequeue()();
        Assert.False(worker.TryQueue("no-read-retry-storm", null, null, null));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(worker.TryQueue("bad-write", null, null, null));
        jobs.Dequeue()();
        Assert.False(worker.TryQueue("no-write-retry-storm", null, null, null));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(worker.TryQueue("recovered", null, null, null));
        jobs.Dequeue()();
        Assert.Equal(3, reads);
        Assert.Equal(2, writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusyReadOrAppenderCannotBlockAnotherCompletionOrAccumulateJobs(bool blockAppender)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task? background = null;
        int schedules = 0;
        var clock = new TestClock();
        void Block() { entered.Set(); release.Wait(); }
        var sampler = new FlowPerformanceSampler(() => { if (!blockAppender) Block(); return new(); }, clock);
        var worker = new FlowPerformanceSamplingWorker(sampler, clock, sample => { if (blockAppender) Block(); }, job =>
        {
            Interlocked.Increment(ref schedules);
            background = Task.Run(job);
            return true;
        });
        Assert.True(worker.TryQueue("first", null, null, null));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            clock.Advance(TimeSpan.FromSeconds(60));
            var submit = Task.Run(() => worker.TryQueue("second", null, null, null));
            Assert.False(await submit.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, schedules);
        }
        finally { release.Set(); }
        await background!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(worker.TryQueue("after-completion", null, null, null));
        await background!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, schedules);
    }

    private sealed class TestClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public void Advance(TimeSpan amount) => Interlocked.Add(ref timestamp, amount.Ticks);
    }
}
