using System;
using System.Threading;

namespace ColorVision.UI
{
    /// <summary>
    /// Owns the current runtime configuration instance. A caller captures one
    /// instance at the start of a task, while a reload only changes later captures.
    /// </summary>
    public interface IRuntimeConfigOwner<out TConfig> where TConfig : class, IConfig
    {
        TConfig Current { get; }

        TConfig Capture();

        void Reload();
    }

    public sealed class RuntimeConfigChangedEventArgs<TConfig> : EventArgs where TConfig : class, IConfig
    {
        public RuntimeConfigChangedEventArgs(TConfig previous, TConfig current)
        {
            Previous = previous;
            Current = current;
        }

        public TConfig Previous { get; }
        public TConfig Current { get; }
    }

    /// <summary>
    /// Adapts the existing reload notification to a small injectable owner. It does
    /// not participate in loading or saving configuration files.
    /// </summary>
    public sealed class RuntimeConfigOwner<TConfig> : IRuntimeConfigOwner<TConfig>, IDisposable where TConfig : class, IConfig
    {
        private readonly Func<TConfig> _configFactory;
        private readonly IConfigReloadNotifier? _reloadNotifier;
        private readonly Action<Exception>? _reloadErrorHandler;
        private TConfig _current;
        private bool _isDisposed;

        public RuntimeConfigOwner(
            Func<TConfig> configFactory,
            IConfigReloadNotifier? reloadNotifier = null,
            Action<Exception>? reloadErrorHandler = null)
        {
            _configFactory = configFactory ?? throw new ArgumentNullException(nameof(configFactory));
            _reloadNotifier = reloadNotifier;
            _reloadErrorHandler = reloadErrorHandler;
            _current = _configFactory();

            if (_reloadNotifier != null)
                _reloadNotifier.ConfigsReloaded += ReloadNotifier_ConfigsReloaded;
        }

        public TConfig Current => Volatile.Read(ref _current);

        public event EventHandler<RuntimeConfigChangedEventArgs<TConfig>>? ConfigurationChanged;

        public TConfig Capture() => Current;

        private void ReloadNotifier_ConfigsReloaded(object? sender, EventArgs e)
        {
            Reload();
        }

        public void Reload()
        {
            TConfig next;
            try
            {
                next = _configFactory();
            }
            catch (Exception ex)
            {
                _reloadErrorHandler?.Invoke(ex);
                return;
            }

            TConfig previous = Interlocked.Exchange(ref _current, next);
            if (!ReferenceEquals(previous, next))
                ConfigurationChanged?.Invoke(this, new RuntimeConfigChangedEventArgs<TConfig>(previous, next));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_reloadNotifier != null)
                _reloadNotifier.ConfigsReloaded -= ReloadNotifier_ConfigsReloaded;
        }
    }
}
