using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Algorithms;

public enum AlgorithmResultStatus
{
    Succeeded,
    Failed,
    Cancelled,
    Superseded,
}

public sealed record AlgorithmFailure(string Code, string Message, string? Path = null, IReadOnlyDictionary<string, string>? Details = null);

public sealed record AlgorithmDiagnosticMessage(string Code, string Message, string Severity = "info", IReadOnlyDictionary<string, string>? Data = null);

public sealed class AlgorithmExecutionDiagnostics
{
    public string? ProviderId { get; init; }

    public AlgorithmProviderKind? ProviderKind { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<AlgorithmDiagnosticMessage> Messages { get; init; } = Array.Empty<AlgorithmDiagnosticMessage>();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AlgorithmImageArtifact), "image")]
[JsonDerivedType(typeof(AlgorithmMeasurementArtifact), "measurement")]
[JsonDerivedType(typeof(AlgorithmTableArtifact), "table")]
[JsonDerivedType(typeof(AlgorithmGeometryArtifact), "geometry")]
[JsonDerivedType(typeof(AlgorithmStructuredDataArtifact), "structuredData")]
[JsonDerivedType(typeof(AlgorithmOverlayArtifact), "overlay")]
public abstract record AlgorithmArtifact(string Name);

public sealed record AlgorithmImageArtifact(
    string Name,
    string Role,
    [property: JsonIgnore] AlgorithmImageBuffer Image,
    IReadOnlyDictionary<string, string>? Metadata = null) : AlgorithmArtifact(Name), IDisposable
{
    public void Dispose() => Image.Dispose();
}

public sealed record AlgorithmMeasurement(
    string Name,
    double Value,
    string? Unit = null,
    int? Channel = null,
    double? Confidence = null,
    IReadOnlyDictionary<string, string>? Qualifiers = null);

public sealed record AlgorithmMeasurementArtifact(string Name, IReadOnlyList<AlgorithmMeasurement> Measurements) : AlgorithmArtifact(Name);

public sealed record AlgorithmTableColumn(string Name, string ValueType, string? Unit = null);

public sealed record AlgorithmTableArtifact(
    string Name,
    IReadOnlyList<AlgorithmTableColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> Rows) : AlgorithmArtifact(Name);

public enum AlgorithmGeometryKind
{
    Point,
    Line,
    Circle,
    Rectangle,
    Polygon,
    Polyline,
    Transform,
}

public sealed record AlgorithmGeometry(
    string Id,
    AlgorithmGeometryKind Kind,
    IReadOnlyList<AlgorithmPoint> Points,
    double? Radius = null,
    IReadOnlyList<double>? Matrix = null,
    double? Residual = null,
    double? Confidence = null,
    string? FilterReason = null,
    IReadOnlyDictionary<string, double>? Measurements = null);

public sealed record AlgorithmGeometryArtifact(
    string Name,
    AlgorithmCoordinateSpace CoordinateSpace,
    IReadOnlyList<AlgorithmGeometry> Geometries) : AlgorithmArtifact(Name);

public sealed record AlgorithmStructuredDataArtifact(string Name, string Schema, JsonElement Data) : AlgorithmArtifact(Name);

public enum AlgorithmOverlayLifetime
{
    Transient,
    Persistent,
}

public sealed record AlgorithmOverlayStyle(string Stroke = "#FFFFA500", string? Fill = null, double StrokeWidth = 1, string? Label = null);

public sealed record AlgorithmOverlayItem(string GeometryId, AlgorithmOverlayStyle Style);

public sealed record AlgorithmOverlayArtifact(
    string Name,
    AlgorithmOverlayLifetime Lifetime,
    IReadOnlyList<AlgorithmOverlayItem> Items) : AlgorithmArtifact(Name);

/// <summary>Host-neutral lifetime store: transient overlays are invalidated by source changes; persistent overlays survive until explicitly cleared.</summary>
public sealed class AlgorithmOverlayStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AlgorithmOverlayArtifact> _transient = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AlgorithmOverlayArtifact> _persistent = new(StringComparer.Ordinal);

    public IReadOnlyList<AlgorithmOverlayArtifact> Snapshot()
    {
        lock (_sync) return _persistent.Values.Concat(_transient.Values).ToArray();
    }

    public void Apply(AlgorithmOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        lock (_sync)
        {
            Dictionary<string, AlgorithmOverlayArtifact> target = artifact.Lifetime == AlgorithmOverlayLifetime.Transient
                ? _transient
                : _persistent;
            Dictionary<string, AlgorithmOverlayArtifact> other = artifact.Lifetime == AlgorithmOverlayLifetime.Transient
                ? _persistent
                : _transient;
            other.Remove(artifact.Name);
            target[artifact.Name] = artifact;
        }
    }

    public void ClearTransient()
    {
        lock (_sync) _transient.Clear();
    }

    public void ClearPersistent()
    {
        lock (_sync) _persistent.Clear();
    }

    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync) return _transient.Remove(name) | _persistent.Remove(name);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _transient.Clear();
            _persistent.Clear();
        }
    }
}

public sealed class AlgorithmResult : IDisposable
{
    private int _disposed;

    public Guid InvocationId { get; init; }

    public AlgorithmId AlgorithmId { get; init; }

    public AlgorithmVersion AlgorithmVersion { get; init; }

    public AlgorithmResultStatus Status { get; init; }

    public IReadOnlyList<AlgorithmArtifact> Artifacts { get; init; } = Array.Empty<AlgorithmArtifact>();

    public IReadOnlyList<AlgorithmFailure> Failures { get; init; } = Array.Empty<AlgorithmFailure>();

    public AlgorithmExecutionDiagnostics Diagnostics { get; init; } = new();

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public TArtifact? GetArtifact<TArtifact>(string? name = null) where TArtifact : AlgorithmArtifact
        => Artifacts.OfType<TArtifact>().FirstOrDefault(artifact => name == null || string.Equals(artifact.Name, name, StringComparison.Ordinal));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (IDisposable disposable in Artifacts.OfType<IDisposable>()) disposable.Dispose();
    }
}
