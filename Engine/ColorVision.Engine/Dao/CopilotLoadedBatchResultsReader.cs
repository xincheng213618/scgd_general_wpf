using ColorVision.Engine.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.Engine;

public sealed record CopilotLoadedBatchResultRow(string ResultRef, string Kind, int InternalId, int TypeCode,
    string TemplateName, int? ResultCode, long? DurationMs, DateTime? CreatedAt);
public sealed record CopilotLoadedBatchSourceSnapshot(string SourceRef, int BatchId, IReadOnlyList<CopilotLoadedBatchResultRow> Results);
public sealed record CopilotLoadedBatchResultsSnapshot(string RegistryInstanceId, DateTimeOffset ObservedAtUtc,
    IReadOnlyList<CopilotLoadedBatchSourceSnapshot> Sources);
public sealed class CopilotLoadedBatchResultsSourceChangedException(string message) : InvalidOperationException(message);
public sealed class CopilotLoadedBatchResultsSourceInvalidException(string message) : InvalidOperationException(message);
public sealed class CopilotLoadedBatchResultsSourceUnavailableException(string message) : InvalidOperationException(message);

/// <summary>Reads only main records already held by registered batch detail pages, without selecting or loading any result.</summary>
public sealed class CopilotLoadedBatchResultsReader
{
    private static readonly object Sync = new();
    private static readonly List<Entry> Entries = new();
    private static readonly string RegistryInstance = Guid.NewGuid().ToString("N");
    private static long _generation;

    internal sealed record ScopeReference(string RegistryInstanceId, string SourceRef);

    internal sealed class Registration(Entry entry) : IDisposable
    {
        private Entry? _entry = entry;
        internal bool IsCurrent => _entry != null && IsRegistered(_entry);
        internal ScopeReference? ScopeReference
        {
            get
            {
                var current = Volatile.Read(ref _entry);
                if (current == null) return null;
                if (!current.Owner.TryGetTarget(out var page))
                {
                    Remove(current);
                    return null;
                }
                page.Dispatcher.VerifyAccess();
                return IsRegistered(current) ? new(RegistryInstance, current.SourceRef) : null;
            }
        }
        public void Dispose()
        {
            var previous = Interlocked.Exchange(ref _entry, null);
            if (previous != null) Remove(previous);
        }
    }

    internal sealed class Entry(MeasureBatchPage owner, MeasureBatchModel batch)
    {
        internal WeakReference<MeasureBatchPage> Owner { get; } = new(owner);
        internal WeakReference<MeasureBatchModel> Batch { get; } = new(batch);
        internal int BatchId { get; } = batch.Id;
        internal string SourceRef { get; } = Guid.NewGuid().ToString("N");
        internal ConditionalWeakTable<ViewResultImage, Identity> ImageRefs { get; } = new();
        internal ConditionalWeakTable<ViewResultAlg, Identity> AlgorithmRefs { get; } = new();
    }

    internal sealed class Identity
    {
        internal string Value { get; } = Guid.NewGuid().ToString("N");
    }

    private sealed record CapturedSource(Entry Entry, MeasureBatchPage Page,
        ObservableCollection<ViewResultImage> Images, ViewResultImage[] ImageRows,
        ObservableCollection<ViewResultAlg> Algorithms, ViewResultAlg[] AlgorithmRows,
        CopilotLoadedBatchSourceSnapshot Snapshot);

    internal static Registration Register(MeasureBatchPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Dispatcher.VerifyAccess();
        if (page.IsLoadedBatchResultSourceDisposed) throw new CopilotLoadedBatchResultsSourceInvalidException("The batch detail source is already disposed.");
        var batch = page.MeasureBatchModel
            ?? throw new CopilotLoadedBatchResultsSourceInvalidException("The batch detail source has no loaded batch model.");
        var entry = new Entry(page, batch);
        lock (Sync)
        {
            Entries.RemoveAll(existing => !existing.Owner.TryGetTarget(out var owner) || ReferenceEquals(owner, page));
            Entries.Add(entry);
            _generation++;
        }
        return new(entry);
    }

    public async Task<CopilotLoadedBatchResultsSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entry[] entries;
        long generation;
        lock (Sync)
        {
            if (Entries.RemoveAll(entry => !entry.Owner.TryGetTarget(out _)) > 0) _generation++;
            entries = Entries.ToArray();
            generation = _generation;
        }
        var pages = new List<MeasureBatchPage>(entries.Length);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.Owner.TryGetTarget(out var page))
            {
                Remove(entry);
                throw Changed();
            }
            pages.Add(page);
        }
        if (pages.Count == 0) return Complete(entries, generation, Array.Empty<CopilotLoadedBatchSourceSnapshot>(), cancellationToken);
        var dispatcher = pages[0].Dispatcher;
        if (pages.Any(page => !ReferenceEquals(page.Dispatcher, dispatcher)))
            throw new CopilotLoadedBatchResultsSourceInvalidException("Loaded batch detail sources must share the application's UI dispatcher.");
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            throw new CopilotLoadedBatchResultsSourceUnavailableException("A registered batch detail UI dispatcher is unavailable.");
        CopilotLoadedBatchResultsSnapshot CaptureAll()
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyRegistry(entries, generation);
            var captured = entries.Select((entry, index) => CaptureSource(entry, pages[index], cancellationToken)).ToArray();
            foreach (var source in captured) ValidateCapturedSource(source);
            return Complete(entries, generation, captured.Select(source => source.Snapshot).ToArray(), cancellationToken);
        }
        return dispatcher.CheckAccess() ? CaptureAll()
            : await dispatcher.InvokeAsync(CaptureAll, DispatcherPriority.Background, cancellationToken).Task.ConfigureAwait(false);
    }

    private static CopilotLoadedBatchResultsSnapshot Complete(Entry[] entries, long generation,
        IReadOnlyList<CopilotLoadedBatchSourceSnapshot> sources, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VerifyRegistry(entries, generation);
        return new(RegistryInstance, DateTimeOffset.UtcNow,
            Array.AsReadOnly(sources.OrderBy(source => source.SourceRef, StringComparer.Ordinal).ToArray()));
    }

    private static void VerifyRegistry(Entry[] entries, long generation)
    {
        lock (Sync)
        {
            if (_generation != generation || entries.Any(entry => !Entries.Contains(entry) || !entry.Owner.TryGetTarget(out _)))
                throw Changed();
        }
    }

    private static CapturedSource CaptureSource(Entry entry, MeasureBatchPage page, CancellationToken cancellationToken)
    {
        page.Dispatcher.VerifyAccess();
        if (!IsRegistered(entry)) throw Changed();
        var images = page.ViewResultImages ?? throw InvalidInventory();
        var algorithms = page.ViewResultAlgs ?? throw InvalidInventory();
        var imageRows = images.ToArray();
        var algorithmRows = algorithms.ToArray();
        ValidateRows(imageRows);
        ValidateRows(algorithmRows);
        var rows = new List<CopilotLoadedBatchResultRow>(imageRows.Length + algorithmRows.Length);
        foreach (var image in imageRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new(entry.ImageRefs.GetValue(image, _ => new Identity()).Value, "image", image.Id, (int)image.FileType,
                string.Empty, image.ResultCode, image.RawDurationMilliseconds, image.CreateTime));
        }
        foreach (var algorithm in algorithmRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new(entry.AlgorithmRefs.GetValue(algorithm, _ => new Identity()).Value, "algorithm", algorithm.Id, (int)algorithm.ResultType,
                algorithm.POITemplateName ?? string.Empty, algorithm.ResultCode, algorithm.TotalTime, algorithm.CreateTime));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var captured = new CapturedSource(entry, page, images, imageRows, algorithms, algorithmRows,
            new(entry.SourceRef, entry.BatchId, Array.AsReadOnly(rows.OrderBy(row => row.ResultRef, StringComparer.Ordinal).ToArray())));
        ValidateCapturedSource(captured);
        return captured;
    }

    private static void ValidateCapturedSource(CapturedSource captured)
    {
        if (!IsRegistered(captured.Entry) || !ReferenceEquals(captured.Images, captured.Page.ViewResultImages) || !ReferenceEquals(captured.Algorithms, captured.Page.ViewResultAlgs)
            || !SameMembers(captured.ImageRows, captured.Images.ToArray()) || !SameMembers(captured.AlgorithmRows, captured.Algorithms.ToArray()))
            throw Changed();
    }

    private static bool IsRegistered(Entry entry)
    {
        lock (Sync)
        {
            if (!Entries.Contains(entry)) return false;
            if (entry.Owner.TryGetTarget(out var page) && !page.IsLoadedBatchResultSourceDisposed && entry.Batch.TryGetTarget(out var batch)
                && ReferenceEquals(page.MeasureBatchModel, batch) && batch.Id == entry.BatchId) return true;
            Entries.Remove(entry);
            _generation++;
            return false;
        }
    }

    private static void Remove(Entry entry)
    {
        lock (Sync)
            if (Entries.Remove(entry)) _generation++;
    }

    private static void ValidateRows<T>(T[] rows) where T : class
    {
        var unique = new HashSet<T>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
            if (row == null || !unique.Add(row)) throw InvalidInventory();
    }

    private static bool SameMembers<T>(T[] before, T[] after) where T : class
    {
        ValidateRows(after);
        return before.Length == after.Length && new HashSet<T>(before, ReferenceEqualityComparer.Instance).SetEquals(after);
    }
    private static CopilotLoadedBatchResultsSourceChangedException Changed() => new("The registered batch detail source changed. Discover the loaded sources again.");
    private static CopilotLoadedBatchResultsSourceInvalidException InvalidInventory() => new("A loaded batch result collection contains a null or duplicate object reference, or is unavailable.");
}
