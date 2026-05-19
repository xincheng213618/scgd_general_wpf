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

        public CopilotContextRegistry(IEnumerable<ICopilotContextProvider> providers)
        {
            _providers = (providers ?? Array.Empty<ICopilotContextProvider>())
                .Where(provider => provider != null)
                .OrderBy(provider => provider.Order)
                .ToArray();
        }

        public static CopilotContextRegistry CreateDefault()
        {
            var providers = AssemblyHandler.GetInstance().LoadImplementations<ICopilotContextProvider>();
            if (providers.All(provider => provider is not CopilotWorkspaceContextProvider))
                providers.Add(new CopilotWorkspaceContextProvider());

            return new CopilotContextRegistry(providers);
        }

        public async Task<IReadOnlyList<CopilotContextItem>> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var items = new List<CopilotContextItem>();
            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!provider.CanProvide(request.Scope))
                    continue;

                try
                {
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