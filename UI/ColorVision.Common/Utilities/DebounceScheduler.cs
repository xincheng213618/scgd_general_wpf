using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Common.Utilities
{
    internal interface IDebounceTimer : IDisposable
    {
        // Start schedules asynchronously; it must not invoke the user callback inline.
        void Start();
    }

    internal sealed class DebounceScheduler
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        internal void AddOrReset(string key, Func<Action<Action>, IDebounceTimer> createTimer, Action<Action, Action>? dispatch = null)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(createTimer);
            Entry entry = new();
            entry.Timer = createTimer(action => OnElapsed(key, entry, action, dispatch));
            Entry? previous;
            try
            {
                lock (_gate)
                {
                    _entries.TryGetValue(key, out previous);
                    _entries[key] = entry;
                    try { entry.Timer.Start(); }
                    catch
                    {
                        if (previous == null) _entries.Remove(key);
                        else _entries[key] = previous;
                        throw;
                    }
                }
            }
            catch
            {
                entry.Dispose();
                throw;
            }
            previous?.Dispose();
        }

        internal void Cancel(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            Entry? entry;
            lock (_gate) _entries.Remove(key, out entry);
            entry?.Dispose();
        }

        internal void DisposeAll()
        {
            List<Entry> entries;
            lock (_gate)
            {
                entries = new List<Entry>(_entries.Values);
                _entries.Clear();
            }
            foreach (Entry entry in entries) entry.Dispose();
        }

        private void OnElapsed(string key, Entry entry, Action action, Action<Action, Action>? dispatch)
        {
            lock (_gate)
            {
                if (!IsCurrent(key, entry) || entry.Triggered) return;
                entry.Triggered = true;
            }

            if (dispatch == null) Execute(key, entry, action);
            else
            {
                // Retain ownership until the queued operation actually starts so Cancel/Reset can invalidate it.
                try { dispatch(() => Execute(key, entry, action), () => Abandon(key, entry)); }
                catch
                {
                    Abandon(key, entry);
                    throw;
                }
            }
        }

        private void Execute(string key, Entry entry, Action action)
        {
            lock (_gate)
            {
                if (!IsCurrent(key, entry)) return;
                // Claim before user code; its reentrant reset belongs to a new entry.
                _entries.Remove(key);
            }
            try { action(); }
            finally { entry.Dispose(); }
        }

        private void Abandon(string key, Entry entry)
        {
            lock (_gate)
            {
                if (IsCurrent(key, entry)) _entries.Remove(key);
            }
            entry.Dispose();
        }

        private bool IsCurrent(string key, Entry entry) => _entries.TryGetValue(key, out Entry? current) && ReferenceEquals(current, entry);

        private sealed class Entry : IDisposable
        {
            private int _disposed;
            internal IDebounceTimer Timer { get; set; } = null!;
            internal bool Triggered { get; set; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) Timer.Dispose();
            }
        }
    }
}
