using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum;

internal sealed record SpectrumCorrectionFeatureProviderRegistration(
    ISpectrometerCorrectionFeatureProvider Provider,
    SpectrumCorrectionFeatureMetadata Metadata);

internal static class SpectrumCorrectionFeatureProviderRegistry
{
    private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumCorrectionFeatureProviderRegistry));
    private static readonly Lazy<List<SpectrumCorrectionFeatureProviderRegistration>> registrations =
        new(DiscoverProviders, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<SpectrumCorrectionFeatureProviderRegistration> Registrations => registrations.Value;

    private static List<SpectrumCorrectionFeatureProviderRegistration> DiscoverProviders()
    {
        var candidates = new List<SpectrumCorrectionFeatureProviderRegistration>();
        foreach (ISpectrometerCorrectionFeatureProvider provider in AssemblyHandler.GetInstance().LoadImplementations<ISpectrometerCorrectionFeatureProvider>())
        {
            try
            {
                SpectrumCorrectionFeatureMetadata metadata = provider.Metadata;
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.DisplayName))
                {
                    log.Warn($"Skip spectrum correction provider {provider.GetType().FullName}: metadata Id and DisplayName are required.");
                    continue;
                }

                candidates.Add(new SpectrumCorrectionFeatureProviderRegistration(provider, metadata));
            }
            catch (Exception ex)
            {
                log.Error($"Failed to read spectrum correction metadata from {provider.GetType().FullName}.", ex);
            }
        }

        var selected = new List<SpectrumCorrectionFeatureProviderRegistration>();
        var featureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SpectrumCorrectionFeatureProviderRegistration candidate in candidates
                     .OrderBy(item => item.Metadata.Order)
                     .ThenBy(item => item.Metadata.Id, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Provider.GetType().AssemblyQualifiedName, StringComparer.Ordinal))
        {
            if (featureIds.Add(candidate.Metadata.Id))
            {
                selected.Add(candidate);
            }
            else
            {
                log.Warn($"Ignore duplicate spectrum correction provider Id '{candidate.Metadata.Id}' from {candidate.Provider.GetType().FullName}.");
            }
        }

        return selected;
    }
}
