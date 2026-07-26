using ColorVision.Copilot;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextRegistryCancellationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CallerCancellationReleasesBlockedProviderInvocation(bool blockCanProvide)
    {
        using var provider = new BlockingContextProvider(blockCanProvide);
        using var cancellation = new CancellationTokenSource();
        var registry = new CopilotContextRegistry(
            [provider],
            extensionBridge: null,
            providerCaptureTimeout: TimeSpan.FromSeconds(10),
            requestCaptureTimeout: TimeSpan.FromSeconds(10),
            maximumConcurrentProviders: 1);
        var request = new CopilotContextRequest
        {
            Scope = CopilotContextScope.Agent,
            UserText = "inspect the current application",
        };
        var captureTask = Task.Run(() => registry.CaptureAsync(request, cancellation.Token));

        try
        {
            Assert.True(provider.Started.Wait(TimeSpan.FromSeconds(1)));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => captureTask.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(provider.Release.IsSet);
        }
        finally
        {
            provider.Release.Set();
            _ = provider.Completed.Wait(TimeSpan.FromSeconds(2));
            try
            {
                await captureTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ProviderTimeoutDoesNotWaitForBlockingCancellationCallback()
    {
        using var provider = new BlockingCancellationContextProvider();
        var registry = new CopilotContextRegistry(
            [provider],
            extensionBridge: null,
            providerCaptureTimeout: TimeSpan.FromMilliseconds(50),
            requestCaptureTimeout: TimeSpan.FromSeconds(5),
            maximumConcurrentProviders: 1);
        var request = new CopilotContextRequest
        {
            Scope = CopilotContextScope.Agent,
            UserText = "inspect the current application",
        };
        var captureTask = registry.CaptureAsync(request, CancellationToken.None);

        try
        {
            Assert.True(provider.Started.Wait(TimeSpan.FromSeconds(1)));
            var items = await captureTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(provider.CancellationCallbackStarted.Wait(TimeSpan.FromSeconds(1)));
            Assert.Contains(items, item => item.Id == "copilot:context-capture-warning");
            Assert.False(provider.ReleaseCancellationCallback.IsSet);
        }
        finally
        {
            provider.ReleaseCancellationCallback.Set();
            provider.ReleaseInvocation.Set();
            try
            {
                await captureTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }
    }

    private sealed class BlockingContextProvider : ICopilotContextProvider, IDisposable
    {
        private readonly bool _blockCanProvide;

        public BlockingContextProvider(bool blockCanProvide)
        {
            _blockCanProvide = blockCanProvide;
        }

        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public ManualResetEventSlim Completed { get; } = new();

        public int Order => 1;

        public bool CanProvide(CopilotContextScope scope)
        {
            if (_blockCanProvide)
                Block();
            return true;
        }

        public Task<CopilotContextItem?> CaptureAsync(
            CopilotContextRequest request,
            CancellationToken cancellationToken)
        {
            if (!_blockCanProvide)
                Block();
            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = "blocking-provider",
                Title = "Blocking provider",
            });
        }

        public void Dispose()
        {
            Release.Set();
            Started.Dispose();
            Release.Dispose();
            Completed.Dispose();
        }

        private void Block()
        {
            Started.Set();
            try
            {
                Release.Wait();
            }
            finally
            {
                Completed.Set();
            }
        }
    }

    private sealed class BlockingCancellationContextProvider : ICopilotContextProvider, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim CancellationCallbackStarted { get; } = new();

        public ManualResetEventSlim ReleaseCancellationCallback { get; } = new();

        public ManualResetEventSlim ReleaseInvocation { get; } = new();

        public int Order => 1;

        public bool CanProvide(CopilotContextScope scope) => true;

        public Task<CopilotContextItem?> CaptureAsync(
            CopilotContextRequest request,
            CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() =>
            {
                CancellationCallbackStarted.Set();
                ReleaseCancellationCallback.Wait(CancellationToken.None);
            });
            Started.Set();
            ReleaseInvocation.Wait(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = "blocking-cancellation-provider",
                Title = "Blocking cancellation provider",
            });
        }

        public void Dispose()
        {
            ReleaseCancellationCallback.Set();
            ReleaseInvocation.Set();
            Started.Dispose();
            CancellationCallbackStarted.Dispose();
            ReleaseCancellationCallback.Dispose();
            ReleaseInvocation.Dispose();
        }
    }
}
