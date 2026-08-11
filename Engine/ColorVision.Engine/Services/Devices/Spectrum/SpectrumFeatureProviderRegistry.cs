using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    internal sealed record SpectrometerFeatureProviderRegistration(
        ISpectrometerFeatureProvider Provider,
        SpectrometerFeatureMetadata Metadata);

    internal static class SpectrumFeatureProviderRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumFeatureProviderRegistry));
        private static readonly Lazy<List<SpectrometerFeatureProviderRegistration>> registrations =
            new(DiscoverProviders, LazyThreadSafetyMode.ExecutionAndPublication);

        public static IReadOnlyList<SpectrometerFeatureProviderRegistration> Registrations => registrations.Value;

        private static List<SpectrometerFeatureProviderRegistration> DiscoverProviders()
        {
            var candidates = new List<SpectrometerFeatureProviderRegistration>();
            foreach (ISpectrometerFeatureProvider provider in AssemblyHandler.GetInstance().LoadImplementations<ISpectrometerFeatureProvider>())
            {
                try
                {
                    SpectrometerFeatureMetadata metadata = provider.Metadata;
                    if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.DisplayName))
                    {
                        log.Warn($"Skip spectrometer feature provider {provider.GetType().FullName}: metadata Id and DisplayName are required.");
                        continue;
                    }

                    candidates.Add(new SpectrometerFeatureProviderRegistration(provider, metadata));
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to read spectrometer feature metadata from {provider.GetType().FullName}.", ex);
                }
            }

            var selected = new List<SpectrometerFeatureProviderRegistration>();
            var featureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SpectrometerFeatureProviderRegistration candidate in candidates
                         .OrderBy(item => item.Metadata.Order)
                         .ThenBy(item => item.Metadata.Id, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Provider.GetType().AssemblyQualifiedName, StringComparer.Ordinal))
            {
                if (featureIds.Add(candidate.Metadata.Id))
                {
                    selected.Add(candidate);
                    continue;
                }

                log.Warn($"Ignore duplicate spectrometer feature provider Id '{candidate.Metadata.Id}' from {candidate.Provider.GetType().FullName}.");
            }

            return selected;
        }
    }
}
