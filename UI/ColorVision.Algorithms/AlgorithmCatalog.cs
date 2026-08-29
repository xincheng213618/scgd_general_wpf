using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace ColorVision.Algorithms;

public enum AlgorithmResultSemantics
{
    /// <summary>The host expects exactly one Role=primary image and may commit it as the new source.</summary>
    ImageTransform,
    /// <summary>The host presents structured artifacts; image artifacts are optional visualizations unless one is explicitly primary.</summary>
    Analysis,
}

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
    string OutputFormatPolicy = "same-as-input")
{
    /// <summary>
    /// Catalog-owned presentation metadata. This is deliberately non-positional so the original
    /// 18-parameter constructor and Deconstruct metadata remain compiler-generated and unchanged.
    /// </summary>
    public AlgorithmPresentationMetadata? Presentation { get; init; }

    /// <summary>
    /// Strongly typed host application contract. Kept out of the positional shape so adding it
    /// does not change the published constructor/deconstruction contract.
    /// </summary>
    public AlgorithmResultSemantics ResultSemantics { get; init; } = AlgorithmResultSemantics.ImageTransform;
}

/// <summary>
/// Provider- and UI-framework-neutral presentation hints owned by the catalog.
/// Hosts project these hints into their own controls while retaining specialized
/// compatibility adapters for algorithms that need an existing editor.
/// </summary>
public sealed record AlgorithmPresentationMetadata(
    int? BatchImageProcessingOrder = null,
    IReadOnlyList<AlgorithmInteractivePresentation>? InteractiveEntries = null);

public sealed record AlgorithmInteractiveGroupPresentation(
    string Id,
    int Order,
    string? DisplayName = null,
    string? ResourceKey = null);

public sealed record AlgorithmInteractivePresentation(
    string CompatibilityId,
    int Order,
    string? DisplayName = null,
    string? ResourceKey = null)
{
    public AlgorithmInteractiveGroupPresentation? Group { get; init; }
}

public sealed record AlgorithmInteractiveCatalogEntry(
    AlgorithmDescriptor Descriptor,
    AlgorithmInteractivePresentation Presentation);

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
    private readonly object _sync = new();
    private readonly Dictionary<AlgorithmId, AlgorithmDescriptor> _descriptors = new();
    private readonly Dictionary<string, AlgorithmId> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlgorithmId> _interactivePresentationIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlgorithmInteractiveGroupPresentation> _interactiveGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, AlgorithmId> _batchPresentationOrders = new();

    public IReadOnlyCollection<AlgorithmDescriptor> Descriptors
    {
        get
        {
            lock (_sync)
            {
                return _descriptors.Values
                    .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
                    .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    public void Register(AlgorithmDescriptor descriptor, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(aliases);
        AlgorithmDescriptor snapshot = FreezeDescriptor(descriptor);
        ValidatePresentation(snapshot);
        string[] normalizedAliases = aliases
            .Prepend(snapshot.Id.Value)
            .Select(alias => NormalizeAlias(alias, nameof(aliases)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        AlgorithmInteractivePresentation[] interactiveEntries = (snapshot.Presentation?.InteractiveEntries
            ?? Array.Empty<AlgorithmInteractivePresentation>()).ToArray();
        AlgorithmInteractiveGroupPresentation[] interactiveGroups = interactiveEntries
            .Select(entry => entry.Group)
            .OfType<AlgorithmInteractiveGroupPresentation>()
            .DistinctBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        lock (_sync)
        {
            if (_descriptors.ContainsKey(snapshot.Id))
                throw new InvalidOperationException($"Algorithm ID '{snapshot.Id}' is already registered.");

            foreach (string alias in normalizedAliases)
            {
                if (_aliases.TryGetValue(alias, out AlgorithmId existing) && existing != snapshot.Id)
                    throw new InvalidOperationException($"Algorithm alias '{alias}' is already registered for '{existing}'.");
            }

            foreach (AlgorithmInteractivePresentation entry in interactiveEntries)
            {
                if (_interactivePresentationIds.TryGetValue(entry.CompatibilityId, out AlgorithmId existing) && existing != snapshot.Id)
                    throw new InvalidOperationException($"Interactive presentation ID '{entry.CompatibilityId}' is already registered for '{existing}'.");
                if (_interactiveGroups.ContainsKey(entry.CompatibilityId))
                    throw new InvalidOperationException($"Interactive presentation ID '{entry.CompatibilityId}' conflicts with an interactive group ID.");
            }

            foreach (AlgorithmInteractiveGroupPresentation group in interactiveGroups)
            {
                if (_interactivePresentationIds.ContainsKey(group.Id))
                    throw new InvalidOperationException($"Interactive group ID '{group.Id}' conflicts with an interactive presentation ID.");
                if (_interactiveGroups.TryGetValue(group.Id, out AlgorithmInteractiveGroupPresentation? existingGroup)
                    && existingGroup != group)
                {
                    throw new InvalidOperationException($"Interactive group ID '{group.Id}' is registered with different presentation metadata.");
                }
            }

            int? batchOrder = snapshot.Presentation?.BatchImageProcessingOrder;
            if (batchOrder.HasValue
                && _batchPresentationOrders.TryGetValue(batchOrder.Value, out AlgorithmId existingBatch)
                && existingBatch != snapshot.Id)
            {
                throw new InvalidOperationException($"Batch presentation order '{batchOrder.Value}' is already registered for '{existingBatch}'.");
            }

            // Every possible conflict has been checked while holding the catalog lock. The
            // following assignments cannot expose a partially registered descriptor.
            _descriptors.Add(snapshot.Id, snapshot);
            foreach (string alias in normalizedAliases) _aliases[alias] = snapshot.Id;
            foreach (AlgorithmInteractivePresentation entry in interactiveEntries)
                _interactivePresentationIds.Add(entry.CompatibilityId, snapshot.Id);
            foreach (AlgorithmInteractiveGroupPresentation group in interactiveGroups)
                _interactiveGroups.TryAdd(group.Id, group);
            if (batchOrder.HasValue) _batchPresentationOrders.Add(batchOrder.Value, snapshot.Id);
        }
    }

    private static AlgorithmDescriptor FreezeDescriptor(AlgorithmDescriptor descriptor)
    {
        AlgorithmDescriptor first = FreezeDescriptorCore(descriptor);
        AlgorithmDescriptor second = FreezeDescriptorCore(descriptor);
        if (AlgorithmDescriptorContract.Equals(first, second)) return second;
        throw new InvalidOperationException($"Algorithm descriptor '{descriptor.Id}' changed while it was being registered.");
    }

    private static AlgorithmDescriptor FreezeDescriptorCore(AlgorithmDescriptor descriptor)
    {
        AlgorithmParameterField[] fields = descriptor.ParameterSchema.Fields
            .Select(field => field with
            {
                DefaultValue = field.DefaultValue.Clone(),
                AllowedValues = field.AllowedValues == null
                    ? null
                    : Array.AsReadOnly(field.AllowedValues.ToArray()),
            })
            .ToArray();
        AlgorithmParameterSchema schema = descriptor.ParameterSchema with
        {
            Fields = Array.AsReadOnly(fields),
            Defaults = descriptor.ParameterSchema.Defaults.Clone(),
        };
        AlgorithmPresentationMetadata? presentation = descriptor.Presentation == null
            ? null
            : descriptor.Presentation with
            {
                InteractiveEntries = descriptor.Presentation.InteractiveEntries == null
                    ? null
                    : Array.AsReadOnly(descriptor.Presentation.InteractiveEntries
                        .Select(entry => entry with { Group = entry.Group is null ? null : entry.Group with { } })
                        .ToArray()),
            };
        return descriptor with
        {
            ParameterSchema = schema,
            SupportedFormats = descriptor.SupportedFormats.ToFrozenSet(),
            OutputFormats = descriptor.OutputFormats?.ToFrozenSet(),
            Presentation = presentation,
        };
    }

    private static string NormalizeAlias(string alias, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("Algorithm aliases cannot be empty.", parameterName);
        return alias.Trim();
    }

    public void RegisterAlias(string alias, AlgorithmId id)
    {
        string normalized = NormalizeAlias(alias, nameof(alias));
        lock (_sync)
        {
            if (!_descriptors.ContainsKey(id)) throw new KeyNotFoundException($"Algorithm '{id}' is not registered.");
            if (_aliases.TryGetValue(normalized, out AlgorithmId existing) && existing != id)
                throw new InvalidOperationException($"Algorithm alias '{normalized}' is already registered for '{existing}'.");
            _aliases[normalized] = id;
        }
    }

    public bool TryResolve(AlgorithmId id, out AlgorithmDescriptor? descriptor)
    {
        lock (_sync) return _descriptors.TryGetValue(id, out descriptor);
    }

    public bool TryResolveAlias(string idOrAlias, out AlgorithmDescriptor? descriptor)
    {
        lock (_sync)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(idOrAlias) || !_aliases.TryGetValue(idOrAlias.Trim(), out AlgorithmId id)) return false;
            return _descriptors.TryGetValue(id, out descriptor);
        }
    }

    private static void ValidatePresentation(AlgorithmDescriptor descriptor)
    {
        AlgorithmPresentationMetadata? presentation = descriptor.Presentation;
        if (presentation?.BatchImageProcessingOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Batch image-processing order cannot be negative.");

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AlgorithmInteractiveGroupPresentation> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (AlgorithmInteractivePresentation entry in presentation?.InteractiveEntries ?? Array.Empty<AlgorithmInteractivePresentation>())
        {
            if (string.IsNullOrWhiteSpace(entry.CompatibilityId))
                throw new ArgumentException("Interactive presentation compatibility ID cannot be empty.", nameof(descriptor));
            if (entry.Order < 0)
                throw new ArgumentOutOfRangeException(nameof(descriptor), "Interactive presentation order cannot be negative.");
            if (!ids.Add(entry.CompatibilityId))
                throw new InvalidOperationException($"Interactive presentation ID '{entry.CompatibilityId}' is duplicated for '{descriptor.Id}'.");
            if (entry.Group is not AlgorithmInteractiveGroupPresentation group) continue;
            if (string.IsNullOrWhiteSpace(group.Id))
                throw new ArgumentException("Interactive group ID cannot be empty.", nameof(descriptor));
            if (group.Order < 0)
                throw new ArgumentOutOfRangeException(nameof(descriptor), "Interactive group order cannot be negative.");
            if (groups.TryGetValue(group.Id, out AlgorithmInteractiveGroupPresentation? existingGroup)
                && existingGroup != group)
            {
                throw new InvalidOperationException($"Interactive group ID '{group.Id}' has inconsistent presentation metadata for '{descriptor.Id}'.");
            }
            groups[group.Id] = group;
        }
        if (ids.Overlaps(groups.Keys))
            throw new InvalidOperationException($"An interactive presentation ID conflicts with an interactive group ID for '{descriptor.Id}'.");
    }
}

/// <summary>Stable host projections over catalog descriptors; contains no WPF or provider implementation types.</summary>
public static class AlgorithmCatalogProjection
{
    private const AlgorithmHostCapabilities BatchRequirements = AlgorithmHostCapabilities.Batch
        | AlgorithmHostCapabilities.Headless
        | AlgorithmHostCapabilities.Local;
    private const AlgorithmHostCapabilities InteractiveRequirements = AlgorithmHostCapabilities.Interactive
        | AlgorithmHostCapabilities.Local;

    public static IReadOnlyList<AlgorithmDescriptor> ForBatchImageProcessing(IAlgorithmCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Descriptors
            .Where(descriptor => HasCapabilities(descriptor, BatchRequirements)
                && descriptor.ResultSemantics == AlgorithmResultSemantics.ImageTransform
                && descriptor.Presentation?.BatchImageProcessingOrder is not null)
            .OrderBy(descriptor => descriptor.Presentation!.BatchImageProcessingOrder!.Value)
            .ThenBy(descriptor => descriptor.Category, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<AlgorithmInteractiveCatalogEntry> ForInteractiveMenu(IAlgorithmCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Descriptors
            .Where(descriptor => HasCapabilities(descriptor, InteractiveRequirements))
            .SelectMany(descriptor => (descriptor.Presentation?.InteractiveEntries ?? Array.Empty<AlgorithmInteractivePresentation>())
                .Select(presentation => new AlgorithmInteractiveCatalogEntry(descriptor, presentation)))
            .OrderBy(entry => entry.Presentation.Order)
            .ThenBy(entry => entry.Descriptor.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Descriptor.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Presentation.CompatibilityId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasCapabilities(AlgorithmDescriptor descriptor, AlgorithmHostCapabilities required)
        => (descriptor.Capabilities & required) == required;
}

/// <summary>Complete execution-contract comparison used by compatibility façades.</summary>
public static class AlgorithmDescriptorContract
{
    public static bool ParameterContractEquals(AlgorithmDescriptor left, AlgorithmDescriptor right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.ParameterType == right.ParameterType
            && SchemaEquals(left.ParameterSchema, right.ParameterSchema);
    }

    public static bool Equals(AlgorithmDescriptor left, AlgorithmDescriptor right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Id == right.Id
            && left.Version == right.Version
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.Category, right.Category, StringComparison.Ordinal)
            && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            && ParameterContractEquals(left, right)
            && left.SupportedFormats.SetEquals(right.SupportedFormats)
            && left.Capabilities == right.Capabilities
            && left.MinimumInputCount == right.MinimumInputCount
            && left.MaximumInputCount == right.MaximumInputCount
            && left.SupportsRectangleRoi == right.SupportsRectangleRoi
            && left.SupportsCircleRoi == right.SupportsCircleRoi
            && left.SupportsPolygonRoi == right.SupportsPolygonRoi
            && left.SupportsPolylineRoi == right.SupportsPolylineRoi
            && string.Equals(left.OutputSuffix, right.OutputSuffix, StringComparison.Ordinal)
            && SetEquals(left.OutputFormats, right.OutputFormats)
            && string.Equals(left.OutputFormatPolicy, right.OutputFormatPolicy, StringComparison.Ordinal)
            && left.ResultSemantics == right.ResultSemantics
            && PresentationEquals(left.Presentation, right.Presentation);
    }

    /// <summary>
    /// Compares the complete provider/host execution shape while deliberately allowing identity
    /// presentation text and compatible semantic-version changes to vary.
    /// </summary>
    public static bool ExecutionShapeEquals(AlgorithmDescriptor left, AlgorithmDescriptor right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Id == right.Id
            && ParameterContractEquals(left, right)
            && left.SupportedFormats.SetEquals(right.SupportedFormats)
            && left.Capabilities == right.Capabilities
            && left.MinimumInputCount == right.MinimumInputCount
            && left.MaximumInputCount == right.MaximumInputCount
            && left.SupportsRectangleRoi == right.SupportsRectangleRoi
            && left.SupportsCircleRoi == right.SupportsCircleRoi
            && left.SupportsPolygonRoi == right.SupportsPolygonRoi
            && left.SupportsPolylineRoi == right.SupportsPolylineRoi
            && string.Equals(left.OutputSuffix, right.OutputSuffix, StringComparison.Ordinal)
            && SetEquals(left.OutputFormats, right.OutputFormats)
            && string.Equals(left.OutputFormatPolicy, right.OutputFormatPolicy, StringComparison.Ordinal)
            && left.ResultSemantics == right.ResultSemantics;
    }

    private static bool SchemaEquals(AlgorithmParameterSchema left, AlgorithmParameterSchema right)
    {
        if (left.Version != right.Version
            || !JsonEquals(left.Defaults, right.Defaults)
            || left.Fields.Count != right.Fields.Count)
        {
            return false;
        }

        // Field order is intentionally significant in V1: it is the stable editor/presentation
        // order of the parameter contract. JSON object member order has no such meaning.
        for (int index = 0; index < left.Fields.Count; index++)
        {
            AlgorithmParameterField a = left.Fields[index];
            AlgorithmParameterField b = right.Fields[index];
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                || !string.Equals(a.ValueType, b.ValueType, StringComparison.Ordinal)
                || !JsonEquals(a.DefaultValue, b.DefaultValue)
                || a.Required != b.Required
                || a.Minimum != b.Minimum
                || a.Maximum != b.Maximum
                || !SequenceEquals(a.AllowedValues, b.AllowedValues)
                || !string.Equals(a.Unit, b.Unit, StringComparison.Ordinal)
                || !string.Equals(a.Description, b.Description, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool PresentationEquals(AlgorithmPresentationMetadata? left, AlgorithmPresentationMetadata? right)
    {
        if (left == null || right == null) return left == right;
        if (left.BatchImageProcessingOrder != right.BatchImageProcessingOrder) return false;
        IReadOnlyList<AlgorithmInteractivePresentation> a = left.InteractiveEntries ?? Array.Empty<AlgorithmInteractivePresentation>();
        IReadOnlyList<AlgorithmInteractivePresentation> b = right.InteractiveEntries ?? Array.Empty<AlgorithmInteractivePresentation>();
        return a.Count == b.Count && a.SequenceEqual(b);
    }

    private static bool SetEquals<T>(IReadOnlySet<T>? left, IReadOnlySet<T>? right)
        => left == null || right == null ? left == null && right == null : left.SetEquals(right);

    private static bool SequenceEquals<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        => left == null || right == null ? left == null && right == null : left.SequenceEqual(right);

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False => true,
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => JsonNumberEquals(left.GetRawText(), right.GetRawText()),
            JsonValueKind.Array => JsonArrayEquals(left, right),
            JsonValueKind.Object => JsonObjectEquals(left, right),
            _ => false,
        };
    }

    private static bool JsonArrayEquals(JsonElement left, JsonElement right)
    {
        JsonElement.ArrayEnumerator leftItems = left.EnumerateArray();
        JsonElement.ArrayEnumerator rightItems = right.EnumerateArray();
        while (leftItems.MoveNext())
        {
            if (!rightItems.MoveNext() || !JsonEquals(leftItems.Current, rightItems.Current))
            {
                return false;
            }
        }
        return !rightItems.MoveNext();
    }

    private static bool JsonObjectEquals(JsonElement left, JsonElement right)
    {
        Dictionary<string, JsonElement> rightProperties = new(StringComparer.Ordinal);
        foreach (JsonProperty property in right.EnumerateObject())
        {
            if (!rightProperties.TryAdd(property.Name, property.Value))
            {
                return false;
            }
        }

        HashSet<string> leftNames = new(StringComparer.Ordinal);
        foreach (JsonProperty property in left.EnumerateObject())
        {
            if (!leftNames.Add(property.Name)
                || !rightProperties.TryGetValue(property.Name, out JsonElement rightValue)
                || !JsonEquals(property.Value, rightValue))
            {
                return false;
            }
        }
        return leftNames.Count == rightProperties.Count;
    }

    private static bool JsonNumberEquals(string left, string right)
        => TryNormalizeJsonNumber(left, out BigInteger leftSignificand, out BigInteger leftExponent)
            && TryNormalizeJsonNumber(right, out BigInteger rightSignificand, out BigInteger rightExponent)
            && leftSignificand == rightSignificand
            && leftExponent == rightExponent;

    private static bool TryNormalizeJsonNumber(
        string value,
        out BigInteger significand,
        out BigInteger decimalExponent)
    {
        significand = BigInteger.Zero;
        decimalExponent = BigInteger.Zero;
        int position = 0;
        bool negative = value.Length > 0 && value[0] == '-';
        if (negative)
        {
            position++;
        }

        int exponentIndex = value.IndexOfAny(['e', 'E'], position);
        string mantissa = exponentIndex >= 0 ? value[position..exponentIndex] : value[position..];
        if (exponentIndex >= 0
            && !BigInteger.TryParse(
                value[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimalExponent))
        {
            return false;
        }

        int decimalPoint = mantissa.IndexOf('.');
        int fractionalDigits = decimalPoint >= 0 ? mantissa.Length - decimalPoint - 1 : 0;
        string digits = decimalPoint >= 0 ? mantissa.Remove(decimalPoint, 1) : mantissa;
        if (!BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out significand))
        {
            return false;
        }

        if (significand.IsZero)
        {
            decimalExponent = BigInteger.Zero;
            return true;
        }

        if (negative)
        {
            significand = -significand;
        }
        decimalExponent -= fractionalDigits;
        while (significand % 10 == 0)
        {
            significand /= 10;
            decimalExponent++;
        }
        return true;
    }
}
