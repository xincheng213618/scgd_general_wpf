using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace ColorVision.UI
{
    public sealed class RuntimeConfigSnapshot<TConfig> where TConfig : class, IConfig
    {
        internal RuntimeConfigSnapshot(long generation, TConfig config)
        {
            Generation = generation;
            Config = config;
        }

        public long Generation { get; }
        public TConfig Config { get; }
    }

    /// <summary>
    /// Owns the current runtime configuration generation. Capture creates a
    /// detached task snapshot; later edits or reloads cannot mutate that task.
    /// </summary>
    public interface IRuntimeConfigOwner<TConfig> where TConfig : class, IConfig
    {
        TConfig Current { get; }
        long Generation { get; }

        TConfig Capture();
        RuntimeConfigSnapshot<TConfig> CaptureSnapshot();

        bool Reload();
    }

    public sealed class RuntimeConfigChangedEventArgs<TConfig> : EventArgs where TConfig : class, IConfig
    {
        public RuntimeConfigChangedEventArgs(TConfig previous, TConfig current, long generation)
        {
            Previous = previous;
            Current = current;
            Generation = generation;
        }

        public TConfig Previous { get; }
        public TConfig Current { get; }
        public long Generation { get; }
    }

    /// <summary>
    /// Adapts the existing reload notification to a small injectable owner. A
    /// reload first resolves and validates a detachable snapshot, then commits
    /// only if no newer generation has already won. Subscriber failures are
    /// isolated so this owner cannot interrupt the process-wide reload event.
    /// </summary>
    public sealed class RuntimeConfigOwner<TConfig> : IRuntimeConfigOwner<TConfig>, IDisposable where TConfig : class, IConfig
    {
        private static readonly JsonSerializerOptions DefaultSnapshotOptions = new()
        {
            IgnoreReadOnlyProperties = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly object _sync = new();
        private readonly Func<TConfig> _configFactory;
        private readonly Func<TConfig, TConfig> _snapshotFactory;
        private readonly IConfigReloadNotifier? _reloadNotifier;
        private readonly Action<Exception>? _reloadErrorHandler;
        private readonly Dictionary<int, int> _reloadThreads = [];
        private RuntimeState _state;
        private long _nextGeneration;
        private int _activeReloads;
        private bool _isDisposed;

        public RuntimeConfigOwner(
            Func<TConfig> configFactory,
            IConfigReloadNotifier? reloadNotifier = null,
            Action<Exception>? reloadErrorHandler = null,
            Func<TConfig, TConfig>? snapshotFactory = null)
        {
            _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
            _reloadNotifier = reloadNotifier;
            _reloadErrorHandler = reloadErrorHandler;
            _snapshotFactory = snapshotFactory ?? CreateDefaultSnapshot;

            TConfig initial = _configFactory() ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} factory returned null.");
            _ = CreateSnapshot(initial);
            _state = new RuntimeState(initial, 0);

            if (_reloadNotifier != null)
                _reloadNotifier.ConfigsReloaded += ReloadNotifier_ConfigsReloaded;
        }

        public TConfig Current => Volatile.Read(ref _state).Config;
        public long Generation => Volatile.Read(ref _state).Generation;

        public event EventHandler<RuntimeConfigChangedEventArgs<TConfig>>? ConfigurationChanged;

        public TConfig Capture() => CaptureSnapshot().Config;

        public RuntimeConfigSnapshot<TConfig> CaptureSnapshot()
        {
            RuntimeState state = Volatile.Read(ref _state);
            TConfig snapshot = _snapshotFactory(state.Config)
                ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} snapshot factory returned null.");
            return new RuntimeConfigSnapshot<TConfig>(state.Generation, snapshot);
        }

        public TConfig CreateSnapshot(TConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            return _snapshotFactory(config)
                ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} snapshot factory returned null.");
        }

        private void ReloadNotifier_ConfigsReloaded(object? sender, EventArgs e) => Reload();

        public bool Reload()
        {
            int threadId = Environment.CurrentManagedThreadId;
            long requestedGeneration;
            lock (_sync)
            {
                if (_isDisposed)
                    return false;

                requestedGeneration = ++_nextGeneration;
                _activeReloads++;
                _reloadThreads.TryGetValue(threadId, out int threadReloads);
                _reloadThreads[threadId] = threadReloads + 1;
            }

            try
            {
                PreparedReload prepared;
                try
                {
                    prepared = PrepareReload(requestedGeneration);
                }
                catch (Exception ex)
                {
                    ReportReloadError(ex);
                    return false;
                }
                return CommitReload(prepared);
            }
            finally
            {
                EndReload(threadId);
            }
        }

        private PreparedReload PrepareReload(long requestedGeneration)
        {
            TConfig candidate = _configFactory()
                ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} factory returned null.");
            _ = CreateSnapshot(candidate);
            return new PreparedReload(requestedGeneration, candidate);
        }

        private bool CommitReload(PreparedReload prepared)
        {
            RuntimeState previous;
            lock (_sync)
            {
                if (_isDisposed || prepared.Generation <= _state.Generation)
                    return false;

                previous = _state;
                Volatile.Write(ref _state, new RuntimeState(prepared.Config, prepared.Generation));
            }

            NotifySubscribers(new RuntimeConfigChangedEventArgs<TConfig>(previous.Config, prepared.Config, prepared.Generation));
            return true;
        }

        private void NotifySubscribers(RuntimeConfigChangedEventArgs<TConfig> args)
        {
            Delegate[] subscribers = ConfigurationChanged?.GetInvocationList() ?? [];
            foreach (Delegate subscriber in subscribers)
            {
                lock (_sync)
                {
                    if (_isDisposed)
                        return;
                }

                try
                {
                    ((EventHandler<RuntimeConfigChangedEventArgs<TConfig>>)subscriber)(this, args);
                }
                catch (Exception ex)
                {
                    ReportReloadError(ex);
                }
            }
        }

        private void ReportReloadError(Exception exception)
        {
            Action<Exception>? handler;
            lock (_sync)
            {
                if (_isDisposed)
                    return;
                handler = _reloadErrorHandler;
            }

            try
            {
                handler?.Invoke(exception);
            }
            catch
            {
                // Error reporting must never escape into the process-wide reload.
            }
        }

        private void EndReload(int threadId)
        {
            lock (_sync)
            {
                _activeReloads--;
                int threadReloads = _reloadThreads[threadId] - 1;
                if (threadReloads == 0)
                    _reloadThreads.Remove(threadId);
                else
                    _reloadThreads[threadId] = threadReloads;

                if (_activeReloads == 0)
                    Monitor.PulseAll(_sync);
            }
        }

        public void Dispose()
        {
            bool calledFromReload;
            lock (_sync)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                calledFromReload = _reloadThreads.ContainsKey(Environment.CurrentManagedThreadId);
            }

            if (_reloadNotifier != null)
                _reloadNotifier.ConfigsReloaded -= ReloadNotifier_ConfigsReloaded;

            if (calledFromReload)
                return;

            lock (_sync)
            {
                while (_activeReloads > 0)
                    Monitor.Wait(_sync);
            }
        }

        private static TConfig CreateDefaultSnapshot(TConfig source)
        {
            Type runtimeType = source.GetType();
            string json = JsonSerializer.Serialize(source, runtimeType, DefaultSnapshotOptions);
            return (TConfig)(JsonSerializer.Deserialize(json, runtimeType, DefaultSnapshotOptions)
                ?? throw new InvalidOperationException($"Could not create a detached {runtimeType.Name} snapshot."));
        }

        private sealed class RuntimeState
        {
            public RuntimeState(TConfig config, long generation)
            {
                Config = config;
                Generation = generation;
            }

            public TConfig Config { get; }
            public long Generation { get; }
        }

        private sealed class PreparedReload
        {
            public PreparedReload(long generation, TConfig config)
            {
                Generation = generation;
                Config = config;
            }

            public long Generation { get; }
            public TConfig Config { get; }
        }
    }
}
