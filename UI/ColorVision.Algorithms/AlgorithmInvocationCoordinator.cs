using System.Threading;
using System.Runtime.ExceptionServices;

namespace ColorVision.Algorithms;

/// <summary>Identifies one immutable ImageView document revision for latest-wins arbitration.</summary>
public readonly record struct AlgorithmInvocationScope
{
    public AlgorithmInvocationScope(Guid documentInstanceId, long sourceRevision)
    {
        if (documentInstanceId == Guid.Empty)
            throw new ArgumentException("A document instance ID is required.", nameof(documentInstanceId));
        if (sourceRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRevision), sourceRevision, "Source revision cannot be negative.");

        DocumentInstanceId = documentInstanceId;
        SourceRevision = sourceRevision;
    }

    public Guid DocumentInstanceId { get; }

    public long SourceRevision { get; }
}

/// <summary>An immutable ticket proving which owner and invocation currently owns a scope.</summary>
public readonly record struct AlgorithmInvocationClaim(
    AlgorithmInvocationScope Scope,
    Guid OwnerId,
    Guid InvocationId,
    long Sequence);

/// <summary>
/// Coordinates latest-wins ownership without depending on WPF or a particular algorithm provider.
/// Cancellation sources remain caller-owned; the coordinator only requests cancellation while a run is attached.
/// </summary>
public sealed class AlgorithmInvocationCoordinator
{
    private sealed class Entry(AlgorithmInvocationClaim claim, CancellationTokenSource? cancellation)
    {
        public AlgorithmInvocationClaim Claim { get; } = claim;

        public CancellationTokenSource? Cancellation { get; set; } = cancellation;
    }

    private readonly object _sync = new();
    private readonly Dictionary<AlgorithmInvocationScope, Entry> _entries = [];
    private long _sequence;

    /// <summary>
    /// Atomically replaces the owner for an exact document/revision scope and requests cancellation of its old run.
    /// Other documents and revisions are unaffected.
    /// </summary>
    public AlgorithmInvocationClaim Claim(
        AlgorithmInvocationScope scope,
        Guid ownerId,
        Guid invocationId,
        CancellationTokenSource? cancellation = null)
    {
        if (!TryClaim(scope, ownerId, invocationId, cancellation, accept: null, out AlgorithmInvocationClaim claim))
            throw new InvalidOperationException("An unconditional algorithm claim was unexpectedly rejected.");
        return claim;
    }

    /// <summary>
    /// Atomically installs a claim and invokes the acceptance callback while the ownership lock
    /// is held. A rejected or throwing callback restores the prior claim without cancelling it.
    /// </summary>
    public bool TryClaim(
        AlgorithmInvocationScope scope,
        Guid ownerId,
        Guid invocationId,
        CancellationTokenSource? cancellation,
        Func<AlgorithmInvocationClaim, bool>? accept,
        out AlgorithmInvocationClaim claim)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("An owner ID is required.", nameof(ownerId));
        if (invocationId == Guid.Empty) throw new ArgumentException("An invocation ID is required.", nameof(invocationId));

        CancellationTokenSource? superseded = null;
        ExceptionDispatchInfo? acceptanceFailure = null;
        bool accepted = false;
        AlgorithmInvocationClaim candidate;
        lock (_sync)
        {
            candidate = new AlgorithmInvocationClaim(scope, ownerId, invocationId, checked(++_sequence));
            _entries.TryGetValue(scope, out Entry? previous);
            _entries[scope] = new Entry(candidate, cancellation);
            bool callbackAccepted = false;
            try
            {
                callbackAccepted = accept?.Invoke(candidate) ?? true;
            }
            catch (Exception exception)
            {
                acceptanceFailure = ExceptionDispatchInfo.Capture(exception);
            }

            bool candidateIsCurrent = IsCurrentNoLock(candidate);
            if (!callbackAccepted || acceptanceFailure != null)
            {
                if (candidateIsCurrent)
                {
                    RestorePrevious(scope, candidate, previous);
                    candidateIsCurrent = false;
                }
            }
            else
            {
                // An acceptance callback is re-entrant. It may install a newer owner or
                // invalidate this scope before it returns. Never report a stale candidate as a
                // successful claim.
                accepted = candidateIsCurrent;
            }

            if (previous?.Cancellation != null
                && !IsEntryCurrentNoLock(scope, previous)
                && !IsCancellationAttachedNoLock(previous.Cancellation))
            {
                superseded = previous.Cancellation;
            }
        }

        TryCancel(superseded);
        if (accepted)
        {
            // Cancelling the superseded run is synchronous and user callbacks may re-enter this
            // coordinator to install a newer claim. The successful return is linearized only
            // after those callbacks finish; never hand a caller a ticket that they already lost.
            lock (_sync)
            {
                accepted = IsCurrentNoLock(candidate);
            }
        }
        claim = accepted ? candidate : default;
        acceptanceFailure?.Throw();
        return accepted;
    }

    private void RestorePrevious(
        AlgorithmInvocationScope scope,
        AlgorithmInvocationClaim rejected,
        Entry? previous)
    {
        // An acceptance callback may re-enter invalidation or install a newer claim. Never
        // overwrite that state with the rejected candidate's predecessor.
        if (!_entries.TryGetValue(scope, out Entry? current) || current.Claim != rejected) return;
        if (previous == null) _entries.Remove(scope);
        else _entries[scope] = previous;
    }

    private bool IsCurrentNoLock(AlgorithmInvocationClaim claim)
        => _entries.TryGetValue(claim.Scope, out Entry? current) && current.Claim == claim;

    private bool IsEntryCurrentNoLock(AlgorithmInvocationScope scope, Entry entry)
        => _entries.TryGetValue(scope, out Entry? current) && ReferenceEquals(current, entry);

    private bool IsCancellationAttachedNoLock(CancellationTokenSource cancellation)
        => _entries.Values.Any(entry => ReferenceEquals(entry.Cancellation, cancellation));

    public bool IsCurrent(AlgorithmInvocationClaim claim)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(claim.Scope, out Entry? entry)
                && entry.Claim == claim;
        }
    }

    public bool TryGetCurrent(AlgorithmInvocationScope scope, out AlgorithmInvocationClaim claim)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(scope, out Entry? entry))
            {
                claim = entry.Claim;
                return true;
            }
        }

        claim = default;
        return false;
    }

    /// <summary>Detaches a completed run's cancellation source while retaining its claim for presentation or commit.</summary>
    public bool CompleteRun(AlgorithmInvocationClaim claim, CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        lock (_sync)
        {
            if (!_entries.TryGetValue(claim.Scope, out Entry? entry)
                || entry.Claim != claim
                || !ReferenceEquals(entry.Cancellation, cancellation))
            {
                return false;
            }

            entry.Cancellation = null;
            return true;
        }
    }

    /// <summary>Runs a publication only if the claim is still current, without consuming it.</summary>
    public bool TryMutateCurrent(AlgorithmInvocationClaim claim, Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        lock (_sync)
        {
            if (!_entries.TryGetValue(claim.Scope, out Entry? entry) || entry.Claim != claim)
                return false;
            mutation();
            return _entries.TryGetValue(claim.Scope, out Entry? current) && current.Claim == claim;
        }
    }

    /// <summary>Releases a claim only if it is still current. A stale owner cannot release its replacement.</summary>
    public bool TryRelease(AlgorithmInvocationClaim claim)
        => TryRelease(claim, mutation: null);

    /// <summary>
    /// Consumes a current claim and performs its host publication before another thread can claim
    /// the same scope. The mutation is intentionally inside the ownership transaction.
    /// </summary>
    public bool TryRelease(AlgorithmInvocationClaim claim, Action? mutation)
    {
        CancellationTokenSource? cancellation = null;
        lock (_sync)
        {
            if (!_entries.TryGetValue(claim.Scope, out Entry? entry) || entry.Claim != claim)
                return false;

            // Publication is part of the ownership transaction. Do not consume the claim until
            // the publication has completed: a throwing callback leaves the claim and its
            // cancellation attachment intact so the caller can retry or explicitly cancel.
            mutation?.Invoke();
            if (!_entries.TryGetValue(claim.Scope, out Entry? current) || current.Claim != claim)
                return false;

            _entries.Remove(claim.Scope);
            cancellation = current.Cancellation;
        }

        TryCancel(cancellation);
        return true;
    }

    public bool InvalidateScope(AlgorithmInvocationScope scope)
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (!_entries.Remove(scope, out Entry? entry)) return false;
            cancellation = entry.Cancellation;
        }

        TryCancel(cancellation);
        return true;
    }

    /// <summary>Invalidates every retained revision for one document without affecting another document.</summary>
    public int InvalidateDocument(Guid documentInstanceId)
        => InvalidateDocumentWhere(documentInstanceId, static _ => true);

    /// <summary>
    /// Invalidates only generations older than <paramref name="sourceRevision"/>. A caller that
    /// advances a document revision before dispatcher cleanup can therefore never remove a claim
    /// already installed for the new generation.
    /// </summary>
    public int InvalidateDocumentRevisionsBefore(Guid documentInstanceId, long sourceRevision)
    {
        if (sourceRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRevision), sourceRevision, "Source revision cannot be negative.");
        return InvalidateDocumentWhere(documentInstanceId, scope => scope.SourceRevision < sourceRevision);
    }

    private int InvalidateDocumentWhere(
        Guid documentInstanceId,
        Func<AlgorithmInvocationScope, bool> predicate)
    {
        if (documentInstanceId == Guid.Empty)
            throw new ArgumentException("A document instance ID is required.", nameof(documentInstanceId));
        ArgumentNullException.ThrowIfNull(predicate);

        List<CancellationTokenSource> cancellations = [];
        int removed = 0;
        lock (_sync)
        {
            AlgorithmInvocationScope[] scopes = _entries.Keys
                .Where(scope => scope.DocumentInstanceId == documentInstanceId && predicate(scope))
                .ToArray();
            foreach (AlgorithmInvocationScope scope in scopes)
            {
                Entry entry = _entries[scope];
                _entries.Remove(scope);
                removed++;
                if (entry.Cancellation != null && !cancellations.Contains(entry.Cancellation))
                    cancellations.Add(entry.Cancellation);
            }
        }

        foreach (CancellationTokenSource cancellation in cancellations) TryCancel(cancellation);
        return removed;
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        if (cancellation == null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { }
    }
}
