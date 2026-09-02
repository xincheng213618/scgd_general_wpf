using ColorVision.Copilot;
using System;
using System.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexPreventIdleSleepTests
{
    [Fact]
    public void ActiveTurnPolicyAcquiresOnlyForAnExplicitTrueSnapshotAndAlwaysReleases()
    {
        var factory = new RecordingSleepRequestFactory();
        var enabled = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredPreventIdleSleep = true,
            HasPreventIdleSleepOverride = true,
        };
        var disabled = enabled with
        {
            ConfiguredPreventIdleSleep = false,
        };
        var unconfigured = enabled with
        {
            HasPreventIdleSleepOverride = false,
        };

        using (CopilotActiveTurnSleepPrevention.Acquire(disabled, factory))
        {
            Assert.Equal(0, factory.AcquireCount);
        }
        using (CopilotActiveTurnSleepPrevention.Acquire(unconfigured, factory))
        {
            Assert.Equal(0, factory.AcquireCount);
        }
        using (CopilotActiveTurnSleepPrevention.Acquire(enabled, factory))
        {
            Assert.Equal(1, factory.AcquireCount);
            Assert.Equal(0, factory.DisposeCount);
        }

        Assert.Equal(1, factory.DisposeCount);
    }

    private sealed class RecordingSleepRequestFactory : ICopilotSystemSleepRequestFactory
    {
        public int AcquireCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IDisposable Acquire()
        {
            AcquireCount++;
            return new CallbackDisposable(() => DisposeCount++);
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private Action? _callback;

        public CallbackDisposable(Action callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _callback, null)?.Invoke();
        }
    }
}
