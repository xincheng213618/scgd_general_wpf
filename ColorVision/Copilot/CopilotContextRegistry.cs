using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotContextRegistry
    {
        private static readonly TimeSpan ProviderCaptureTimeout = TimeSpan.FromSeconds(10);
        private readonly IReadOnlyList<ICopilotContextProvider> _providers;
        private readonly CopilotAgentExtensionBridge? _extensionBridge;

        public CopilotContextRegistry(IEnumerable<ICopilotContextProvider> providers)
            : this(providers, null)
        {
        }

        public CopilotContextRegistry(IEnumerable<ICopilotContextProvider> providers, CopilotAgentExtensionBridge? extensionBridge)
        {
            _providers = (providers ?? Array.Empty<ICopilotContextProvider>())
                .Where(provider => provider != null)
                .OrderBy(provider => provider.Order)
                .ToArray();
            _extensionBridge = extensionBridge;
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

            var items = new List<CopilotContextItem>();
            var providers = _extensionBridge == null
                ? _providers
                : _providers.Concat(_extensionBridge.GetSnapshot().ContextProviders).OrderBy(provider => provider.Order).ToArray();
            var failedProviderCount = 0;
            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Task<CopilotContextItem?>? captureTask = null;
                using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                try
                {
                    if (!provider.CanProvide(request.Scope))
                        continue;

                    captureTask = provider.CaptureAsync(request, providerCancellation.Token);
                    var item = await captureTask.WaitAsync(ProviderCaptureTimeout, cancellationToken);
                    if (item == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(item.Title)
                        && string.IsNullOrWhiteSpace(item.Summary)
                        && string.IsNullOrWhiteSpace(item.Content))
                    {
                        continue;
                    }

                    items.Add(item);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failedProviderCount++;
                    TryCancel(providerCancellation);
                    if (captureTask != null)
                        ObserveFault(captureTask);

                    var providerName = provider.GetType().FullName ?? provider.GetType().Name;
                    var error = exception is TimeoutException
                        ? $"timed out after {ProviderCaptureTimeout.TotalSeconds:N0} seconds"
                        : CopilotUserFacingErrorFormatter.Sanitize(exception.Message);
                    Trace.TraceWarning($"Copilot context provider '{providerName}' failed: {error}");
                }
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

        private static void TryCancel(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AggregateException exception)
            {
                Trace.TraceWarning($"Copilot context provider cancellation callback failed: {CopilotUserFacingErrorFormatter.Sanitize(exception.Message)}");
            }
        }

        private static void ObserveFault(Task task)
        {
            _ = task.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
