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

public enum AlgorithmPrimaryImageSelectionStatus
{
    None,
    Selected,
    Missing,
    Ambiguous,
}

public readonly record struct AlgorithmPrimaryImageSelection(
    AlgorithmPrimaryImageSelectionStatus Status,
    AlgorithmImageArtifact? Artifact,
    int ImageArtifactCount,
    int PrimaryArtifactCount);

/// <summary>Host-neutral image-result selection shared by interactive and batch adapters.</summary>
public static class AlgorithmArtifactSelection
{
    public static AlgorithmPrimaryImageSelection SelectPrimaryImage(IEnumerable<AlgorithmArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        AlgorithmImageArtifact[] images = artifacts.OfType<AlgorithmImageArtifact>().ToArray();
        if (images.Length == 0)
            return new AlgorithmPrimaryImageSelection(AlgorithmPrimaryImageSelectionStatus.None, null, 0, 0);

        AlgorithmImageArtifact[] primary = images
            .Where(artifact => string.Equals(artifact.Role, "primary", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return primary.Length switch
        {
            1 => new AlgorithmPrimaryImageSelection(AlgorithmPrimaryImageSelectionStatus.Selected, primary[0], images.Length, 1),
            0 => new AlgorithmPrimaryImageSelection(AlgorithmPrimaryImageSelectionStatus.Missing, null, images.Length, 0),
            _ => new AlgorithmPrimaryImageSelection(AlgorithmPrimaryImageSelectionStatus.Ambiguous, null, images.Length, primary.Length),
        };
    }
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

public enum AlgorithmOverlayStoreChangeKind
{
    Applied,
    Removed,
    Cleared,
}

public sealed record AlgorithmOverlayStoreChange(
    string Name,
    AlgorithmOverlayArtifact? DetachedArtifact = null)
{
    public Guid? DetachedEntryId { get; init; }
}

public sealed record AlgorithmOverlayStoreEntry(
    Guid EntryId,
    AlgorithmOverlayArtifact Artifact);

public sealed class AlgorithmOverlayStoreChangedEventArgs(
    AlgorithmOverlayStoreChangeKind changeKind,
    IReadOnlyList<AlgorithmOverlayStoreChange> changes) : EventArgs
{
    public AlgorithmOverlayStoreChangeKind ChangeKind { get; } = changeKind;

    public IReadOnlyList<AlgorithmOverlayStoreChange> Changes { get; } = changes;

    public IReadOnlyList<string> Names { get; } = changes.Select(change => change.Name).ToArray();

    public Guid? OriginId { get; private set; }

    public AlgorithmOverlayStoreChangedEventArgs(
        AlgorithmOverlayStoreChangeKind changeKind,
        IReadOnlyList<AlgorithmOverlayStoreChange> changes,
        Guid? originId)
        : this(changeKind, changes)
    {
        OriginId = originId;
    }
}

/// <summary>Host-neutral lifetime store: transient overlays are invalidated by source changes; persistent overlays survive until explicitly cleared.</summary>
public sealed class AlgorithmOverlayStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AlgorithmOverlayStoreEntry> _transient = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AlgorithmOverlayStoreEntry> _persistent = new(StringComparer.Ordinal);

    public event EventHandler<AlgorithmOverlayStoreChangedEventArgs>? Changed;

    public IReadOnlyList<AlgorithmOverlayArtifact> Snapshot()
    {
        lock (_sync) return _persistent.Values.Concat(_transient.Values).Select(entry => entry.Artifact).ToArray();
    }

    public void Apply(AlgorithmOverlayArtifact artifact)
        => Apply(artifact, originId: null);

    public AlgorithmOverlayStoreEntry Apply(AlgorithmOverlayArtifact artifact, Guid originId)
        => Apply(artifact, (Guid?)originId);

    private AlgorithmOverlayStoreEntry Apply(AlgorithmOverlayArtifact artifact, Guid? originId)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        List<AlgorithmOverlayStoreChange> changes = [];
        AlgorithmOverlayStoreEntry applied = new(Guid.NewGuid(), artifact);
        AlgorithmOverlayStoreEntry? previousTarget = null;
        AlgorithmOverlayStoreEntry? previousOther = null;
        Dictionary<string, AlgorithmOverlayStoreEntry> target;
        Dictionary<string, AlgorithmOverlayStoreEntry> other;
        lock (_sync)
        {
            target = artifact.Lifetime == AlgorithmOverlayLifetime.Transient
                ? _transient
                : _persistent;
            other = artifact.Lifetime == AlgorithmOverlayLifetime.Transient
                ? _persistent
                : _transient;
            if (other.Remove(artifact.Name, out previousOther)) changes.Add(Detached(previousOther));
            if (target.TryGetValue(artifact.Name, out previousTarget)) changes.Add(Detached(previousTarget));
            target[artifact.Name] = applied;
        }
        if (changes.Count == 0) changes.Add(new AlgorithmOverlayStoreChange(artifact.Name));
        try
        {
            Changed?.Invoke(this, new AlgorithmOverlayStoreChangedEventArgs(
                AlgorithmOverlayStoreChangeKind.Applied,
                changes,
                originId));
        }
        catch
        {
            lock (_sync)
            {
                if (target.TryGetValue(artifact.Name, out AlgorithmOverlayStoreEntry? current)
                    && current.EntryId == applied.EntryId)
                {
                    target.Remove(artifact.Name);
                    if (previousTarget != null) target[artifact.Name] = previousTarget;
                    if (previousOther != null) other[artifact.Name] = previousOther;
                }
            }
            throw;
        }
        return applied;
    }

    public void ClearTransient()
        => ClearTransient(originId: null);

    public void ClearTransient(Guid originId)
        => ClearTransient((Guid?)originId);

    private void ClearTransient(Guid? originId)
    {
        AlgorithmOverlayStoreChange[] changes;
        lock (_sync)
        {
            changes = _transient.Values.Select(Detached).ToArray();
            _transient.Clear();
        }
        RaiseCleared(changes, originId);
    }

    public void ClearPersistent()
        => ClearPersistent(originId: null);

    public void ClearPersistent(Guid originId)
        => ClearPersistent((Guid?)originId);

    private void ClearPersistent(Guid? originId)
    {
        AlgorithmOverlayStoreChange[] changes;
        lock (_sync)
        {
            changes = _persistent.Values.Select(Detached).ToArray();
            _persistent.Clear();
        }
        RaiseCleared(changes, originId);
    }

    public bool Remove(string name)
        => Remove(name, expectedEntryId: null, originId: null);

    public bool Remove(string name, Guid expectedEntryId, Guid originId)
        => Remove(name, (Guid?)expectedEntryId, originId);

    private bool Remove(string name, Guid? expectedEntryId, Guid? originId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        List<AlgorithmOverlayStoreChange> changes = [];
        lock (_sync)
        {
            if (_transient.TryGetValue(name, out AlgorithmOverlayStoreEntry? transient)
                && (!expectedEntryId.HasValue || transient.EntryId == expectedEntryId.Value))
            {
                _transient.Remove(name);
                changes.Add(Detached(transient));
            }
            if (_persistent.TryGetValue(name, out AlgorithmOverlayStoreEntry? persistent)
                && (!expectedEntryId.HasValue || persistent.EntryId == expectedEntryId.Value))
            {
                _persistent.Remove(name);
                changes.Add(Detached(persistent));
            }
        }
        if (changes.Count > 0)
        {
            try
            {
                Changed?.Invoke(this, new AlgorithmOverlayStoreChangedEventArgs(
                    AlgorithmOverlayStoreChangeKind.Removed,
                    changes,
                    originId));
            }
            catch
            {
                lock (_sync)
                {
                    foreach (AlgorithmOverlayStoreChange change in changes)
                    {
                        if (change.DetachedArtifact == null || !change.DetachedEntryId.HasValue) continue;
                        Dictionary<string, AlgorithmOverlayStoreEntry> target = change.DetachedArtifact.Lifetime == AlgorithmOverlayLifetime.Transient
                            ? _transient
                            : _persistent;
                        if (!target.ContainsKey(change.Name))
                            target[change.Name] = new AlgorithmOverlayStoreEntry(change.DetachedEntryId.Value, change.DetachedArtifact);
                    }
                }
                throw;
            }
        }
        return changes.Count > 0;
    }

    public void Clear()
        => Clear(originId: null);

    public void Clear(Guid originId)
        => Clear((Guid?)originId);

    private void Clear(Guid? originId)
    {
        AlgorithmOverlayStoreChange[] changes;
        lock (_sync)
        {
            changes = _persistent.Values.Concat(_transient.Values)
                .Select(Detached)
                .ToArray();
            _transient.Clear();
            _persistent.Clear();
        }
        RaiseCleared(changes, originId);
    }

    private void RaiseCleared(AlgorithmOverlayStoreChange[] changes, Guid? originId)
    {
        if (changes.Length == 0) return;
        Changed?.Invoke(this, new AlgorithmOverlayStoreChangedEventArgs(
            AlgorithmOverlayStoreChangeKind.Cleared,
            changes,
            originId));
    }

    private static AlgorithmOverlayStoreChange Detached(AlgorithmOverlayStoreEntry entry)
        => new(entry.Artifact.Name, entry.Artifact) { DetachedEntryId = entry.EntryId };
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
