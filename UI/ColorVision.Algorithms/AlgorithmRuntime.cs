namespace ColorVision.Algorithms;

/// <summary>
/// Immutable provider registry used by one algorithm runtime. Provider selection order is
/// deterministic and provider IDs are unique within the runtime.
/// </summary>
public sealed class AlgorithmProviderRegistry
{
    public AlgorithmProviderRegistry(IEnumerable<IImageAlgorithmProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        IImageAlgorithmProvider[] values = providers
            .Select(provider => provider ?? throw new ArgumentException("Provider collections cannot contain null values.", nameof(providers)))
            .OrderByDescending(provider => provider.Metadata.Priority)
            .ToArray();
        string? duplicate = values
            .GroupBy(provider => provider.Metadata.ProviderId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate != null)
            throw new InvalidOperationException($"Algorithm provider ID '{duplicate}' is registered more than once.");
        Providers = Array.AsReadOnly(values);
    }

    public IReadOnlyList<IImageAlgorithmProvider> Providers { get; }

    public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, AlgorithmHostCapabilities requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if ((descriptor.Capabilities & requiredCapabilities) != requiredCapabilities) return false;

        foreach (IImageAlgorithmProvider provider in Providers)
        {
            if (!AlgorithmExecutionPlanePolicy.Matches(provider.Metadata.ExecutionPlane, requiredCapabilities)
                || (provider.Metadata.Capabilities & requiredCapabilities) != requiredCapabilities)
            {
                continue;
            }

            if (!descriptor.SupportedFormats.Any(provider.Metadata.SupportedFormats.Contains)) continue;
            if (provider is not IAlgorithmDescriptorSupport descriptorSupport) continue;
            if (!descriptorSupport.CanExecuteDescriptor(descriptor, out _)) continue;
            if (provider is IAlgorithmProviderAvailability availability
                && !availability.IsAvailable(descriptor, out _))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reports whether an explicit invocation can be attempted. Unlike descriptor-only host
    /// projection, this preserves legacy providers: their final decision is deferred to the
    /// real input-aware <see cref="IImageAlgorithmProvider.CanExecute"/> call in the runner.
    /// </summary>
    public bool CanAttemptExecution(AlgorithmDescriptor descriptor, AlgorithmHostCapabilities requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if ((descriptor.Capabilities & requiredCapabilities) != requiredCapabilities) return false;

        foreach (IImageAlgorithmProvider provider in Providers)
        {
            if (!AlgorithmExecutionPlanePolicy.Matches(provider.Metadata.ExecutionPlane, requiredCapabilities)
                || (provider.Metadata.Capabilities & requiredCapabilities) != requiredCapabilities
                || !descriptor.SupportedFormats.Any(provider.Metadata.SupportedFormats.Contains))
            {
                continue;
            }

            if (provider is IAlgorithmDescriptorSupport descriptorSupport
                && !descriptorSupport.CanExecuteDescriptor(descriptor, out _))
            {
                continue;
            }
            if (provider is IAlgorithmProviderAvailability availability
                && !availability.IsAvailable(descriptor, out _))
            {
                continue;
            }
            return true;
        }
        return false;
    }
}

public static class AlgorithmExecutionPlanePolicy
{
    public static bool Matches(AlgorithmExecutionPlane plane, AlgorithmHostCapabilities requiredCapabilities)
    {
        bool local = (requiredCapabilities & AlgorithmHostCapabilities.Local) != 0;
        bool remote = (requiredCapabilities & AlgorithmHostCapabilities.RemoteDevice) != 0;
        if (local && remote) return false;
        if (local) return plane == AlgorithmExecutionPlane.Local;
        if (remote) return plane == AlgorithmExecutionPlane.RemoteDevice;
        return true;
    }
}

/// <summary>
/// One injectable, internally consistent algorithm control plane. Catalog projection and
/// execution must use the same runtime so a descriptor can never be displayed with an unrelated
/// global provider set or parameter-migration graph.
/// </summary>
public sealed class AlgorithmRuntime
{
    public AlgorithmRuntime(
        IAlgorithmCatalog catalog,
        IEnumerable<IImageAlgorithmProvider> providers,
        IAlgorithmExecutionScheduler scheduler,
        IEnumerable<IAlgorithmParameterMigrator>? parameterMigrators = null,
        AlgorithmInvocationCoordinator? invocationCoordinator = null)
        : this(
            catalog,
            new AlgorithmProviderRegistry(providers),
            scheduler,
            parameterMigrators,
            invocationCoordinator)
    {
    }

    public AlgorithmRuntime(
        IAlgorithmCatalog catalog,
        AlgorithmProviderRegistry providerRegistry,
        IAlgorithmExecutionScheduler scheduler,
        IEnumerable<IAlgorithmParameterMigrator>? parameterMigrators = null,
        AlgorithmInvocationCoordinator? invocationCoordinator = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ProviderRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        ParameterMigrators = Array.AsReadOnly((parameterMigrators ?? Array.Empty<IAlgorithmParameterMigrator>()).ToArray());
        InvocationCoordinator = invocationCoordinator ?? new AlgorithmInvocationCoordinator();
        Runner = AlgorithmRunner.CreateWithProviderRegistry(Catalog, ProviderRegistry, Scheduler, ParameterMigrators);
    }

    public IAlgorithmCatalog Catalog { get; }

    public AlgorithmProviderRegistry ProviderRegistry { get; }

    public IAlgorithmExecutionScheduler Scheduler { get; }

    public IReadOnlyList<IAlgorithmParameterMigrator> ParameterMigrators { get; }

    public AlgorithmInvocationCoordinator InvocationCoordinator { get; }

    public AlgorithmRunner Runner { get; }

    public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, AlgorithmHostCapabilities requiredCapabilities)
        => ProviderRegistry.CanExecuteDescriptor(descriptor, requiredCapabilities);

    public bool CanAttemptExecution(AlgorithmDescriptor descriptor, AlgorithmHostCapabilities requiredCapabilities)
        => ProviderRegistry.CanAttemptExecution(descriptor, requiredCapabilities);

    /// <summary>
    /// Creates a catalog view that deliberately retains this runtime's providers, scheduler,
    /// migrators and invocation coordinator. Prefer constructing a complete runtime when adding
    /// custom providers.
    /// </summary>
    public AlgorithmRuntime WithCatalog(IAlgorithmCatalog catalog)
        => ReferenceEquals(catalog, Catalog)
            ? this
            : new AlgorithmRuntime(catalog, ProviderRegistry, Scheduler, ParameterMigrators, InvocationCoordinator);
}
