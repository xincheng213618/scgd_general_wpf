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
    /// A validated candidate generation. Disposing an uncommitted candidate releases
    /// the owner's prepare lease without publishing it.
    /// </summary>
    public sealed class PreparedRuntimeConfig<TConfig> : IDisposable where TConfig : class, IConfig
    {
        private RuntimeConfigOwner<TConfig>? _owner;

        internal PreparedRuntimeConfig(
            RuntimeConfigOwner<TConfig> owner,
            long generation,
            TConfig config,
            int prepareThreadId)
        {
            _owner = owner;
            Generation = generation;
            Config = config;
            PrepareThreadId = prepareThreadId;
        }

        public long Generation { get; }
        public TConfig Config { get; }
        internal int PrepareThreadId { get; }

        internal bool IsOwnedBy(RuntimeConfigOwner<TConfig> owner) => ReferenceEquals(_owner, owner);

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ReleasePrepared(this);
        }
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
    /// Owns a process-lifetime configuration binding. A reload first resolves and
    /// validates a detachable snapshot, then commits only if no newer generation
    /// has already won. It does not subscribe to the legacy reload event; the
    /// process coordinator is its only automatic binding source.
    /// </summary>
    public sealed class RuntimeConfigOwner<TConfig> : IRuntimeConfigOwner<TConfig>, IConfigReloadParticipant, IDisposable where TConfig : class, IConfig
    {
        private static readonly JsonSerializerOptions DefaultSnapshotOptions = new()
        {
            IgnoreReadOnlyProperties = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly object _sync = new();
        private readonly Func<TConfig> _configFactory;
        private readonly Func<TConfig, TConfig> _snapshotFactory;
        private readonly Action<Exception>? _reloadErrorHandler;
        private readonly Dictionary<int, int> _reloadThreads = [];
        private RuntimeState _state;
        private long _nextGeneration;
        private int _activeReloads;
        private bool _isDisposed;

        public RuntimeConfigOwner(
            Func<TConfig> configFactory,
            Action<Exception>? reloadErrorHandler = null,
            Func<TConfig, TConfig>? snapshotFactory = null,
            string? configReloadName = null,
            int configReloadOrder = 0)
        {
            _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
            _reloadErrorHandler = reloadErrorHandler;
            _snapshotFactory = snapshotFactory ?? CreateDefaultSnapshot;
            ConfigReloadName = string.IsNullOrWhiteSpace(configReloadName)
                ? $"{typeof(TConfig).Name} runtime owner"
                : configReloadName;
            ConfigReloadOrder = configReloadOrder;

            TConfig initial = _configFactory() ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} factory returned null.");
            _ = CreateSnapshot(initial);
            _state = new RuntimeState(initial, 0);
        }

        public TConfig Current => Volatile.Read(ref _state).Config;
        public long Generation => Volatile.Read(ref _state).Generation;

        public event EventHandler<RuntimeConfigChangedEventArgs<TConfig>>? ConfigurationChanged;

        public string ConfigReloadName { get; }

        public int ConfigReloadOrder { get; }

        public TConfig Capture() => CaptureSnapshot().Config;

        public RuntimeConfigSnapshot<TConfig> CaptureSnapshot()
        {
            RuntimeState state;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                state = _state;
            }
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

        public bool Reload()
        {
            try
            {
                using PreparedRuntimeConfig<TConfig> prepared = Prepare(_configFactory);
                return Commit(prepared);
            }
            catch (Exception ex)
            {
                ReportReloadError(ex);
                return false;
            }
        }

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);
            using PreparedRuntimeConfig<TConfig> prepared = PrepareCurrentConfig(currentConfig);
            _ = Commit(prepared);
        }

        public PreparedRuntimeConfig<TConfig> PrepareCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);
            return Prepare(() => currentConfig.GetRequiredService<TConfig>());
        }

        public PreparedRuntimeConfig<TConfig> Prepare(TConfig candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            return Prepare(() => candidate);
        }

        private PreparedRuntimeConfig<TConfig> Prepare(Func<TConfig> candidateFactory)
        {
            ArgumentNullException.ThrowIfNull(candidateFactory);
            int threadId = Environment.CurrentManagedThreadId;
            long requestedGeneration;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);

                requestedGeneration = ++_nextGeneration;
                _activeReloads++;
                _reloadThreads.TryGetValue(threadId, out int threadReloads);
                _reloadThreads[threadId] = threadReloads + 1;
            }

            try
            {
                TConfig candidate = candidateFactory()
                    ?? throw new InvalidOperationException($"The {typeof(TConfig).Name} factory returned null.");
                _ = CreateSnapshot(candidate);
                return new PreparedRuntimeConfig<TConfig>(this, requestedGeneration, candidate, threadId);
            }
            catch
            {
                EndReload(threadId);
                throw;
            }
        }

        public bool Commit(PreparedRuntimeConfig<TConfig> prepared)
        {
            return Commit(prepared, null);
        }

        public bool Commit(
            PreparedRuntimeConfig<TConfig> prepared,
            Action<RuntimeConfigChangedEventArgs<TConfig>>? commitAction)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            if (!prepared.IsOwnedBy(this))
                throw new InvalidOperationException("The prepared configuration does not belong to this owner or was already released.");

            RuntimeState previous;
            RuntimeConfigChangedEventArgs<TConfig> args;
            lock (_sync)
            {
                if (_isDisposed || prepared.Generation <= _state.Generation)
                    return false;

                previous = _state;
                args = new RuntimeConfigChangedEventArgs<TConfig>(previous.Config, prepared.Config, prepared.Generation);
                commitAction?.Invoke(args);
                Volatile.Write(ref _state, new RuntimeState(prepared.Config, prepared.Generation));
            }

            NotifySubscribers(args);
            return true;
        }

        private void NotifySubscribers(RuntimeConfigChangedEventArgs<TConfig> args)
        {
            Delegate[] subscribers = ConfigurationChanged?.GetInvocationList() ?? [];
            foreach (Delegate subscriber in subscribers)
            {
                lock (_sync)
                {
                    if (_isDisposed || args.Generation != _state.Generation)
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

        internal void ReleasePrepared(PreparedRuntimeConfig<TConfig> prepared)
        {
            EndReload(prepared.PrepareThreadId);
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

    }
}
