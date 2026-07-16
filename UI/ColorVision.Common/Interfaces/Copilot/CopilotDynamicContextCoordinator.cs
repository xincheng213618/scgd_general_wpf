using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    /// <summary>
    /// Coordinates short-lived UI context sources that may have multiple simultaneous page or
    /// window instances. Only the most recently activated session is captured, and the module
    /// extension exists only while at least one session is alive.
    /// </summary>
    public sealed class CopilotDynamicContextCoordinator<TSnapshot> where TSnapshot : class
    {
        private readonly CopilotAgentExtensionRegistry _registry;
        private readonly Func<Func<CancellationToken, Task<TSnapshot?>>, Func<bool>, ICopilotContextProvider> _contextProviderFactory;
        private readonly Func<CopilotAgentExtensionRegistry, ICopilotContextProvider, string?, IDisposable> _extensionRegistrar;
        private readonly List<SessionEntry> _sessions = new();
        private readonly object _syncRoot = new();
        private SessionEntry? _current;
        private IDisposable? _extensionRegistration;

        public CopilotDynamicContextCoordinator(
            CopilotAgentExtensionRegistry registry,
            Func<Func<CancellationToken, Task<TSnapshot?>>, Func<bool>, ICopilotContextProvider> contextProviderFactory,
            Func<CopilotAgentExtensionRegistry, ICopilotContextProvider, string?, IDisposable> extensionRegistrar)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _contextProviderFactory = contextProviderFactory ?? throw new ArgumentNullException(nameof(contextProviderFactory));
            _extensionRegistrar = extensionRegistrar ?? throw new ArgumentNullException(nameof(extensionRegistrar));
        }

        public CopilotDynamicContextSession Register(
            Func<CancellationToken, Task<TSnapshot?>> snapshotProvider,
            string? sourceVersion = null)
        {
            ArgumentNullException.ThrowIfNull(snapshotProvider);
            var entry = new SessionEntry(snapshotProvider);
            lock (_syncRoot)
            {
                _sessions.Add(entry);
                _current = entry;
                try
                {
                    if (_extensionRegistration == null)
                    {
                        var contextProvider = _contextProviderFactory(CaptureCurrentAsync, HasSessions);
                        _extensionRegistration = _extensionRegistrar(_registry, contextProvider, sourceVersion);
                    }
                }
                catch
                {
                    _sessions.Remove(entry);
                    _current = _sessions.LastOrDefault();
                    throw;
                }
            }

            return new CopilotDynamicContextSession(
                () => IsCurrent(entry),
                () => Activate(entry),
                () => Unregister(entry));
        }

        private async Task<TSnapshot?> CaptureCurrentAsync(CancellationToken cancellationToken)
        {
            SessionEntry? entry;
            lock (_syncRoot)
            {
                entry = _current;
            }

            if (entry == null)
                return null;

            var snapshot = await entry.SnapshotProvider(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                return ReferenceEquals(_current, entry) && _sessions.Contains(entry) ? snapshot : null;
            }
        }

        private bool HasSessions()
        {
            lock (_syncRoot)
            {
                return _sessions.Count > 0;
            }
        }

        private void Activate(SessionEntry entry)
        {
            lock (_syncRoot)
            {
                if (_sessions.Contains(entry))
                    _current = entry;
            }
        }

        private bool IsCurrent(SessionEntry entry)
        {
            lock (_syncRoot)
            {
                return ReferenceEquals(_current, entry) && _sessions.Contains(entry);
            }
        }

        private void Unregister(SessionEntry entry)
        {
            IDisposable? extensionRegistration = null;
            lock (_syncRoot)
            {
                if (!_sessions.Remove(entry))
                    return;

                if (ReferenceEquals(_current, entry))
                    _current = _sessions.LastOrDefault();
                if (_sessions.Count == 0)
                {
                    extensionRegistration = _extensionRegistration;
                    _extensionRegistration = null;
                }
            }

            extensionRegistration?.Dispose();
        }

        private sealed class SessionEntry
        {
            public SessionEntry(Func<CancellationToken, Task<TSnapshot?>> snapshotProvider)
            {
                SnapshotProvider = snapshotProvider;
            }

            public Func<CancellationToken, Task<TSnapshot?>> SnapshotProvider { get; }
        }
    }

    public sealed class CopilotDynamicContextSession : IDisposable
    {
        private readonly Func<bool> _isCurrent;
        private readonly Action _activate;
        private Action? _dispose;

        internal CopilotDynamicContextSession(Func<bool> isCurrent, Action activate, Action dispose)
        {
            _isCurrent = isCurrent;
            _activate = activate;
            _dispose = dispose;
        }

        public bool IsCurrent => Volatile.Read(ref _dispose) != null && _isCurrent();

        public void Activate()
        {
            if (Volatile.Read(ref _dispose) != null)
                _activate();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
