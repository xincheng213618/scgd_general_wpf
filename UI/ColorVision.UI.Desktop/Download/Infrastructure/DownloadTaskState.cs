namespace ColorVision.UI.Desktop.Download
{
    // Requests invalidate old work immediately; queued commands finish in order without blocking the UI.
    internal sealed class DownloadTaskState
    {
        private readonly object _sync = new();
        private Task _worker = Task.CompletedTask;
        private long _version;
        private DownloadStatus _desiredStatus;
        private bool _deleted;
        private readonly List<CancellationTokenSource> _sources = new();
        private CancellationTokenSource? _current;
        private readonly Dictionary<string, Newtonsoft.Json.Linq.JObject?> _statuses = new();

        public int DaemonGeneration { get; set; }
        public long Version { get { lock (_sync) return _version; } }
        public bool IsDeleted { get { lock (_sync) return _deleted; } }
        public Task Idle { get { lock (_sync) return _worker; } }

        public CancellationToken GetCancellationToken(long version)
        {
            lock (_sync) return IsRunning(version) && _current != null ? _current.Token : new CancellationToken(canceled: true);
        }

        public (long Version, CancellationToken Token) Start(CancellationToken lifetime)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_deleted, this);
                _current?.Cancel();
                _current = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                _sources.Add(_current);
                _desiredStatus = DownloadStatus.Waiting;
                return (++_version, _current.Token);
            }
        }

        public long Stop(bool delete)
        {
            lock (_sync)
            {
                _current?.Cancel();
                _deleted |= delete;
                _desiredStatus = DownloadStatus.Paused;
                return ++_version;
            }
        }

        public bool IsCurrent(long version)
        {
            lock (_sync) return !_deleted && _version == version;
        }

        public bool WithCurrent(long version, Action action)
        {
            lock (_sync)
            {
                if (!IsCurrent(version)) return false;
                action();
                return true;
            }
        }

        public bool IsRunning(long version)
        {
            lock (_sync) return !_deleted && _version > 0 && _version == version && _desiredStatus is DownloadStatus.Waiting or DownloadStatus.Downloading;
        }

        public bool Finish(long version, DownloadStatus status)
        {
            lock (_sync)
            {
                if (!IsRunning(version)) return false;
                _desiredStatus = status;
                return true;
            }
        }

        public Task Enqueue(Func<Task> action)
        {
            lock (_sync)
            {
                _worker = _worker.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
                return _worker;
            }
        }

        public void SetGids(IEnumerable<string> gids, int generation)
        {
            lock (_sync)
            {
                _statuses.Clear();
                foreach (string gid in gids) _statuses[gid] = null;
                DaemonGeneration = generation;
            }
        }

        public string[] GetGids() { lock (_sync) return _statuses.Keys.ToArray(); }

        public Newtonsoft.Json.Linq.JObject[] ApplyStatus(string gid, Newtonsoft.Json.Linq.JObject status)
        {
            lock (_sync)
            {
                if (_statuses.ContainsKey(gid)) _statuses[gid] = status;
                return _statuses.Values.Where(value => value != null).Select(value => value!).ToArray();
            }
        }

        public void Follow(string parent, IEnumerable<string> children)
        {
            lock (_sync)
            {
                _statuses.Remove(parent);
                foreach (string gid in children) _statuses.TryAdd(gid, null);
            }
        }

        public void ReleaseCancellationSources()
        {
            lock (_sync)
            {
                foreach (var source in _sources) source.Dispose();
                _sources.Clear();
                _current = null;
            }
        }
    }
}
