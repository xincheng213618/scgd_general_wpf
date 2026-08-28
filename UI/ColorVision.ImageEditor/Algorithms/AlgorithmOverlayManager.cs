using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    internal sealed record AlgorithmOverlayRegistrationSnapshot(
        string Name,
        AlgorithmOverlayLifetime Lifetime,
        Guid DocumentInstanceId,
        long SourceRevision,
        Visual Visual);

    internal interface IAlgorithmOverlayRegistration : IDisposable
    {
        void Remove();
    }

    /// <summary>
    /// Owns the WPF visual and host-neutral artifact as one registration. All mutations are
    /// serialized by the canvas dispatcher so a stale registration cannot remove its replacement.
    /// </summary>
    internal sealed class AlgorithmOverlayManager
    {
        private sealed class Entry(
            AlgorithmOverlayArtifact artifact,
            Visual visual,
            Guid documentInstanceId,
            long sourceRevision,
            Guid token,
            Guid storeEntryId)
        {
            public AlgorithmOverlayArtifact Artifact { get; } = artifact;

            public Visual Visual { get; } = visual;

            public Guid DocumentInstanceId { get; } = documentInstanceId;

            public long SourceRevision { get; set; } = sourceRevision;

            public Guid Token { get; } = token;

            public Guid StoreEntryId { get; } = storeEntryId;
        }

        private readonly DrawCanvas _canvas;
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly Guid _artifactMutationOrigin = Guid.NewGuid();
        private bool _disposed;

        public AlgorithmOverlayManager(DrawCanvas canvas)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            _canvas = canvas;
            Artifacts.Changed += Artifacts_Changed;
        }

        public AlgorithmOverlayStore Artifacts { get; } = new();

        public IAlgorithmOverlayRegistration Register(
            AlgorithmOverlayArtifact artifact,
            Visual visual,
            Guid documentInstanceId,
            long sourceRevision)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            ArgumentNullException.ThrowIfNull(visual);
            return Invoke(() => RegisterCore(artifact, visual, documentInstanceId, sourceRevision));
        }

        public void OnSourceRevisionChanged(Guid documentInstanceId, long sourceRevision)
        {
            Invoke(() =>
            {
                foreach (Entry transient in _entries.Values
                    .Where(entry => entry.DocumentInstanceId == documentInstanceId
                        && entry.SourceRevision < sourceRevision
                        && entry.Artifact.Lifetime == AlgorithmOverlayLifetime.Transient)
                    .ToArray())
                {
                    RemoveCore(transient, removeArtifact: true);
                }
                RemoveFacadeOnlyArtifacts(AlgorithmOverlayLifetime.Transient);

                foreach (Entry persistent in _entries.Values.Where(entry =>
                    entry.DocumentInstanceId == documentInstanceId
                    && entry.SourceRevision < sourceRevision
                    && entry.Artifact.Lifetime == AlgorithmOverlayLifetime.Persistent))
                {
                    persistent.SourceRevision = sourceRevision;
                }
            });
        }

        public void ClearDocumentBeforeRevision(Guid documentInstanceId, long revisionExclusive)
        {
            Invoke(() =>
            {
                foreach (Entry entry in _entries.Values
                    .Where(entry => entry.DocumentInstanceId == documentInstanceId
                        && entry.SourceRevision < revisionExclusive)
                    .ToArray())
                {
                    RemoveCore(entry, removeArtifact: true);
                }
                RemoveFacadeOnlyArtifacts(lifetime: null);
            });
        }

        public void ClearDocument(Guid documentInstanceId)
        {
            Invoke(() =>
            {
                foreach (Entry entry in _entries.Values
                    .Where(entry => entry.DocumentInstanceId == documentInstanceId)
                    .ToArray())
                {
                    RemoveCore(entry, removeArtifact: true);
                }
                ClearArtifacts(lifetime: null);
            });
        }

        internal IReadOnlyList<AlgorithmOverlayRegistrationSnapshot> SnapshotRegistrations()
        {
            return Invoke(() => _entries.Values
                .Select(entry => new AlgorithmOverlayRegistrationSnapshot(
                    entry.Artifact.Name,
                    entry.Artifact.Lifetime,
                    entry.DocumentInstanceId,
                    entry.SourceRevision,
                    entry.Visual))
                .ToArray());
        }

        public void Dispose()
        {
            Invoke(() =>
            {
                if (_disposed) return;
                _disposed = true;
                Artifacts.Changed -= Artifacts_Changed;
                foreach (Entry entry in _entries.Values.ToArray())
                    RemoveCore(entry, removeArtifact: true);
                ClearArtifacts(lifetime: null);
            });
        }

        private Registration RegisterCore(
            AlgorithmOverlayArtifact artifact,
            Visual visual,
            Guid documentInstanceId,
            long sourceRevision)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_entries.TryGetValue(artifact.Name, out Entry? existing))
                RemoveCore(existing, removeArtifact: true);

            Guid token = Guid.NewGuid();
            _canvas.AddOverlayVisual(visual);
            AlgorithmOverlayStoreEntry? stored = null;
            try
            {
                stored = Artifacts.Apply(artifact, _artifactMutationOrigin);
                Entry entry = new(artifact, visual, documentInstanceId, sourceRevision, token, stored.EntryId);
                _entries.Add(artifact.Name, entry);
            }
            catch
            {
                _canvas.RemoveOverlayVisual(visual);
                if (stored != null) Artifacts.Remove(artifact.Name, stored.EntryId, _artifactMutationOrigin);
                throw;
            }
            return new Registration(this, artifact.Name, token, artifact.Lifetime);
        }

        private void Release(string name, Guid token, bool includePersistent)
        {
            Invoke(() =>
            {
                if (_disposed
                    || !_entries.TryGetValue(name, out Entry? entry)
                    || entry.Token != token
                    || (!includePersistent && entry.Artifact.Lifetime == AlgorithmOverlayLifetime.Persistent))
                {
                    return;
                }
                RemoveCore(entry, removeArtifact: true);
            });
        }

        private void RemoveCore(Entry entry, bool removeArtifact)
        {
            if (!_entries.TryGetValue(entry.Artifact.Name, out Entry? current) || current.Token != entry.Token)
                return;

            _entries.Remove(entry.Artifact.Name);
            if (_canvas.ContainsVisual(entry.Visual)) _canvas.RemoveOverlayVisual(entry.Visual);
            if (removeArtifact) Artifacts.Remove(entry.Artifact.Name, entry.StoreEntryId, _artifactMutationOrigin);
        }

        private void ClearArtifacts(AlgorithmOverlayLifetime? lifetime)
        {
            if (lifetime == AlgorithmOverlayLifetime.Transient)
                Artifacts.ClearTransient(_artifactMutationOrigin);
            else if (lifetime == AlgorithmOverlayLifetime.Persistent)
                Artifacts.ClearPersistent(_artifactMutationOrigin);
            else
                Artifacts.Clear(_artifactMutationOrigin);
        }

        private void RemoveFacadeOnlyArtifacts(AlgorithmOverlayLifetime? lifetime)
        {
            HashSet<string> managedNames = _entries.Keys.ToHashSet(StringComparer.Ordinal);
            foreach (AlgorithmOverlayArtifact artifact in Artifacts.Snapshot()
                .Where(artifact => (!lifetime.HasValue || artifact.Lifetime == lifetime.Value)
                    && !managedNames.Contains(artifact.Name))
                .ToArray())
            {
                Artifacts.Remove(artifact.Name);
            }
        }

        private void Artifacts_Changed(object? sender, AlgorithmOverlayStoreChangedEventArgs e)
        {
            if (e.OriginId == _artifactMutationOrigin || _disposed) return;
            AlgorithmOverlayStoreChange[] detached = e.Changes
                .Where(change => change.DetachedArtifact != null)
                .ToArray();
            if (detached.Length == 0) return;
            void RemoveDetachedVisuals()
            {
                if (_disposed) return;
                foreach (AlgorithmOverlayStoreChange change in detached)
                {
                    if (!_entries.TryGetValue(change.Name, out Entry? entry)
                        || change.DetachedEntryId != entry.StoreEntryId)
                    {
                        continue;
                    }
                    RemoveCore(entry, removeArtifact: false);
                }
            }

            if (_canvas.Dispatcher.CheckAccess())
                RemoveDetachedVisuals();
            else
                _canvas.Dispatcher.BeginInvoke(RemoveDetachedVisuals, DispatcherPriority.Send);
        }

        private T Invoke<T>(Func<T> action)
        {
            return _canvas.Dispatcher.CheckAccess() ? action() : _canvas.Dispatcher.Invoke(action);
        }

        private void Invoke(Action action)
        {
            if (_canvas.Dispatcher.CheckAccess())
                action();
            else
                _canvas.Dispatcher.Invoke(action);
        }

        private sealed class Registration(
            AlgorithmOverlayManager manager,
            string name,
            Guid token,
            AlgorithmOverlayLifetime lifetime) : IAlgorithmOverlayRegistration
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                manager.Release(name, token, includePersistent: lifetime == AlgorithmOverlayLifetime.Transient);
            }

            public void Remove()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                manager.Release(name, token, includePersistent: true);
            }
        }
    }
}
