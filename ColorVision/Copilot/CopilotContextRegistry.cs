using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotContextRegistry
    {
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
            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!provider.CanProvide(request.Scope))
                        continue;

                    var item = await provider.CaptureAsync(request, cancellationToken);
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }

            return items;
        }
    }
}
