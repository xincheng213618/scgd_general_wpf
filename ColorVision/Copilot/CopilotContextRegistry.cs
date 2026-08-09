using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1001 // The provider gate intentionally matches the registry lifetime and contains late extension work across requests.

namespace ColorVision.Copilot
{
    public sealed class CopilotContextRegistry
    {
        private static readonly TimeSpan DefaultProviderCaptureTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultRequestCaptureTimeout = TimeSpan.FromSeconds(12);
        private const int DefaultMaximumConcurrentProviders = 4;
        private readonly IReadOnlyList<ICopilotContextProvider> _providers;
        private readonly CopilotAgentExtensionBridge? _extensionBridge;
        private readonly TimeSpan _providerCaptureTimeout;
        private readonly TimeSpan _requestCaptureTimeout;
        private readonly SemaphoreSlim _providerConcurrencyGate;

        public CopilotContextRegistry(IEnumerable<ICopilotContextProvider> providers)
            : this(
                providers,
                null,
                DefaultProviderCaptureTimeout,
                DefaultRequestCaptureTimeout,
                DefaultMaximumConcurrentProviders)
        {
        }

        public CopilotContextRegistry(IEnumerable<ICopilotContextProvider> providers, CopilotAgentExtensionBridge? extensionBridge)
            : this(
                providers,
                extensionBridge,
                DefaultProviderCaptureTimeout,
                DefaultRequestCaptureTimeout,
                DefaultMaximumConcurrentProviders)
        {
        }

        internal CopilotContextRegistry(
            IEnumerable<ICopilotContextProvider> providers,
            CopilotAgentExtensionBridge? extensionBridge,
            TimeSpan providerCaptureTimeout,
            TimeSpan requestCaptureTimeout,
            int maximumConcurrentProviders)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(providerCaptureTimeout, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestCaptureTimeout, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentProviders);

            _providers = (providers ?? Array.Empty<ICopilotContextProvider>())
                .Where(provider => provider != null)
                .OrderBy(provider => provider.Order)
                .ToArray();
            _extensionBridge = extensionBridge;
            _providerCaptureTimeout = providerCaptureTimeout;
            _requestCaptureTimeout = requestCaptureTimeout;
            _providerConcurrencyGate = new SemaphoreSlim(maximumConcurrentProviders, maximumConcurrentProviders);
        }

        public static CopilotContextRegistry CreateDefault()
        {
            var providers = AssemblyHandler.GetInstance().LoadImplementations<ICopilotContextProvider>();
            if (providers.All(provider => provider is not CopilotWorkspaceContextProvider))
                providers.Add(new CopilotWorkspaceContextProvider());

            return new CopilotContextRegistry(providers, CopilotAgentExtensionBridge.Shared);
        }

        public async Task<IReadOnlyList<CopilotContextItem>> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var providers = _extensionBridge == null || !request.IncludeExtensionProviders
                ? _providers
                : _providers.Concat(_extensionBridge.GetSnapshot().ContextProviders).OrderBy(provider => provider.Order).ToArray();
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCancellation.CancelAfter(_requestCaptureTimeout);
            var captureTasks = providers
                .Select((provider, index) => CaptureProviderAsync(
                    index,
                    provider,
                    request,
                    _providerConcurrencyGate,
                    requestCancellation.Token,
                    cancellationToken))
                .ToArray();
            var results = captureTasks.Length == 0
                ? Array.Empty<ProviderCaptureResult>()
                : await Task.WhenAll(captureTasks).ConfigureAwait(false);

            var items = new List<CopilotContextItem>();
            var failedProviderCount = 0;
            foreach (var result in results.OrderBy(result => result.Index))
            {
                if (result.Item != null)
                    items.Add(result.Item);
                if (!result.Failed)
                    continue;

                failedProviderCount++;
                Trace.TraceWarning($"Copilot context provider '{result.ProviderName}' failed: {result.Error}");
            }

            if (failedProviderCount > 0)
            {
                items.Add(new CopilotContextItem
                {
                    Id = "copilot:context-capture-warning",
                    Title = "Application context capture warning",
                    Summary = $"{failedProviderCount} application context source(s) were unavailable for this request.",
                    Content = "The embedded application context is incomplete. Do not claim that unavailable app state was inspected; rely on the remaining evidence or state the limitation when it materially affects the answer.",
                });
            }

            return items;
        }

        private async Task<ProviderCaptureResult> CaptureProviderAsync(
            int index,
            ICopilotContextProvider provider,
            CopilotContextRequest request,
            SemaphoreSlim concurrencyGate,
            CancellationToken requestCancellationToken,
            CancellationToken callerCancellationToken)
        {
            var gateEntered = false;
            Task<CopilotContextItem?>? captureTask = null;
            CopilotNonBlockingCancellationSource? providerCancellation = null;
            try
            {
                await concurrencyGate.WaitAsync(requestCancellationToken).ConfigureAwait(false);
                gateEntered = true;
                providerCancellation = new CopilotNonBlockingCancellationSource();
                var providerCancellationToken = providerCancellation.Token;
                captureTask = Task.Run(async () =>
                {
                    if (!provider.CanProvide(request.Scope))
                        return null;
                    providerCancellationToken.ThrowIfCancellationRequested();
                    return await provider.CaptureAsync(request, providerCancellationToken).ConfigureAwait(false);
                }, providerCancellationToken);
                var item = await captureTask
                    .WaitAsync(_providerCaptureTimeout, requestCancellationToken)
                    .ConfigureAwait(false);
                return IsEmpty(item)
                    ? ProviderCaptureResult.Empty(index, provider)
                    : ProviderCaptureResult.Success(index, provider, item!);
            }
            catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
            {
                providerCancellation?.RequestCancellation();
                CopilotCancellationBoundary.ObserveLateFault(captureTask);
                throw;
            }
            catch (OperationCanceledException) when (requestCancellationToken.IsCancellationRequested)
            {
                providerCancellation?.RequestCancellation();
                CopilotCancellationBoundary.ObserveLateFault(captureTask);
                return ProviderCaptureResult.Failure(
                    index,
                    provider,
                    $"the request-level context deadline elapsed after {FormatDuration(_requestCaptureTimeout)}");
            }
            catch (TimeoutException)
            {
                providerCancellation?.RequestCancellation();
                CopilotCancellationBoundary.ObserveLateFault(captureTask);
                return ProviderCaptureResult.Failure(
                    index,
                    provider,
                    $"timed out after {FormatDuration(_providerCaptureTimeout)}");
            }
            catch (Exception exception)
            {
                providerCancellation?.RequestCancellation();
                CopilotCancellationBoundary.ObserveLateFault(captureTask);
                return ProviderCaptureResult.Failure(
                    index,
                    provider,
                    CopilotUserFacingErrorFormatter.Sanitize(exception.Message));
            }
            finally
            {
                providerCancellation?.Dispose();
                if (gateEntered)
                    ReleaseProviderSlot(concurrencyGate, captureTask);
            }
        }

        private static void ReleaseProviderSlot(SemaphoreSlim concurrencyGate, Task? captureTask)
        {
            if (captureTask == null || captureTask.IsCompleted)
            {
                concurrencyGate.Release();
                return;
            }

            _ = ReleaseProviderSlotWhenCompletedAsync(concurrencyGate, captureTask);
        }

        private static async Task ReleaseProviderSlotWhenCompletedAsync(
            SemaphoreSlim concurrencyGate,
            Task captureTask)
        {
            try
            {
                await captureTask.ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                concurrencyGate.Release();
            }
        }

        private static bool IsEmpty(CopilotContextItem? item)
        {
            return item == null
                || string.IsNullOrWhiteSpace(item.Title)
                    && string.IsNullOrWhiteSpace(item.Summary)
                    && string.IsNullOrWhiteSpace(item.Content);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalSeconds >= 1
                ? $"{duration.TotalSeconds:0.#} seconds"
                : $"{Math.Max(1, duration.TotalMilliseconds):0} milliseconds";
        }

        private sealed record ProviderCaptureResult(
            int Index,
            string ProviderName,
            CopilotContextItem? Item,
            bool Failed,
            string Error)
        {
            public static ProviderCaptureResult Success(int index, ICopilotContextProvider provider, CopilotContextItem item) =>
                new(index, GetProviderName(provider), item, Failed: false, string.Empty);

            public static ProviderCaptureResult Empty(int index, ICopilotContextProvider provider) =>
                new(index, GetProviderName(provider), null, Failed: false, string.Empty);

            public static ProviderCaptureResult Failure(int index, ICopilotContextProvider provider, string error) =>
                new(index, GetProviderName(provider), null, Failed: true, error);

            private static string GetProviderName(ICopilotContextProvider provider) =>
                provider.GetType().FullName ?? provider.GetType().Name;
        }
    }
}
