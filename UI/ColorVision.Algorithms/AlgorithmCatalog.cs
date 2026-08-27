namespace ColorVision.Algorithms;

public sealed record AlgorithmDescriptor(
    AlgorithmId Id,
    AlgorithmVersion Version,
    string Name,
    string Category,
    string Description,
    Type ParameterType,
    AlgorithmParameterSchema ParameterSchema,
    IReadOnlySet<AlgorithmImageFormat> SupportedFormats,
    AlgorithmHostCapabilities Capabilities,
    int MinimumInputCount = 1,
    int MaximumInputCount = 1,
    bool SupportsRectangleRoi = false,
    bool SupportsCircleRoi = false,
    bool SupportsPolygonRoi = false,
    bool SupportsPolylineRoi = false,
    string OutputSuffix = "",
    IReadOnlySet<AlgorithmImageFormat>? OutputFormats = null,
    string OutputFormatPolicy = "same-as-input");

public sealed record AlgorithmProviderMetadata(
    string ProviderId,
    string Name,
    AlgorithmProviderKind Kind,
    AlgorithmExecutionPlane ExecutionPlane,
    int Priority,
    AlgorithmHostCapabilities Capabilities,
    IReadOnlySet<AlgorithmImageFormat> SupportedFormats,
    string? ImplementationVersion = null,
    string? DeviceId = null);

public interface IAlgorithmCatalog
{
    IReadOnlyCollection<AlgorithmDescriptor> Descriptors { get; }

    bool TryResolve(AlgorithmId id, out AlgorithmDescriptor? descriptor);

    bool TryResolveAlias(string idOrAlias, out AlgorithmDescriptor? descriptor);
}

public sealed class AlgorithmCatalog : IAlgorithmCatalog
{
    private readonly Dictionary<AlgorithmId, AlgorithmDescriptor> _descriptors = new();
    private readonly Dictionary<string, AlgorithmId> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AlgorithmDescriptor> Descriptors => _descriptors.Values
        .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
        .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)
        .ToArray();

    public void Register(AlgorithmDescriptor descriptor, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_descriptors.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"Algorithm ID '{descriptor.Id}' is already registered.");

        RegisterAlias(descriptor.Id.Value, descriptor.Id);
        foreach (string alias in aliases) RegisterAlias(alias, descriptor.Id);
    }

    public void RegisterAlias(string alias, AlgorithmId id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        if (!_descriptors.ContainsKey(id)) throw new KeyNotFoundException($"Algorithm '{id}' is not registered.");
        if (_aliases.TryGetValue(alias.Trim(), out AlgorithmId existing) && existing != id)
            throw new InvalidOperationException($"Algorithm alias '{alias}' is already registered for '{existing}'.");
        _aliases[alias.Trim()] = id;
    }

    public bool TryResolve(AlgorithmId id, out AlgorithmDescriptor? descriptor) => _descriptors.TryGetValue(id, out descriptor);

    public bool TryResolveAlias(string idOrAlias, out AlgorithmDescriptor? descriptor)
    {
        descriptor = null;
        if (string.IsNullOrWhiteSpace(idOrAlias) || !_aliases.TryGetValue(idOrAlias.Trim(), out AlgorithmId id)) return false;
        return TryResolve(id, out descriptor);
    }
}
