using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ColorVision
{
    /// <summary>
    /// Owns the process-lifetime APPConfig subscription. Reloads replace the config object, so the
    /// application must detach C1 before observing C2 and must ignore work that completes for a
    /// retired generation.
    /// </summary>
    internal sealed class AppConfigRuntimeOwner : IConfigReloadParticipant, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly Func<Task<int?>> _enforceSingleInstanceAsync;
        private readonly Action<APPConfig> _persistConfig;
        private readonly Action<int>? _enforcementSucceeded;
        private readonly Action<Exception>? _enforcementFailed;
        private readonly Func<bool> _suppressEnforcement;
        private APPConfig? _currentConfig;
        private long _currentGeneration;
        private bool _hasInitialBinding;
        private bool _enforcementRequested;
        private bool _enforcementWorkerRunning;
        private Task _currentEnforcementTask = Task.CompletedTask;
        private bool _disposed;

        public AppConfigRuntimeOwner(
            Func<Task<int?>> enforceSingleInstanceAsync,
            Action<APPConfig> persistConfig,
            Action<int>? enforcementSucceeded = null,
            Action<Exception>? enforcementFailed = null,
            Func<bool>? suppressEnforcement = null)
        {
            _enforceSingleInstanceAsync = enforceSingleInstanceAsync
                ?? throw new ArgumentNullException(nameof(enforceSingleInstanceAsync));
            _persistConfig = persistConfig ?? throw new ArgumentNullException(nameof(persistConfig));
            _enforcementSucceeded = enforcementSucceeded;
            _enforcementFailed = enforcementFailed;
            _suppressEnforcement = suppressEnforcement ?? (() => false);
        }

        public string ConfigReloadName => nameof(AppConfigRuntimeOwner);

        public int ConfigReloadOrder => 50;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);
            APPConfig nextConfig = currentConfig.GetRequiredService<APPConfig>();
            bool isInitialBinding;

            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (ReferenceEquals(_currentConfig, nextConfig))
                    return;

                if (_currentConfig != null)
                    _currentConfig.PropertyChanged -= CurrentConfig_PropertyChanged;

                _currentConfig = nextConfig;
                _currentGeneration++;
                nextConfig.PropertyChanged += CurrentConfig_PropertyChanged;
                isInitialBinding = !_hasInitialBinding;
                _hasInitialBinding = true;
            }

            // Startup has already applied the initial value before participants are registered.
            // A later C2=false was deserialized before this subscription existed, so apply it now.
            if (!isInitialBinding && !nextConfig.IsMute)
                RequestSingleInstanceEnforcement(nextConfig);
        }

        internal async Task WaitForEnforcementIdleAsync()
        {
            while (true)
            {
                Task currentTask;
                lock (_syncRoot)
                {
                    if (!_enforcementWorkerRunning)
                        return;
                    currentTask = _currentEnforcementTask;
                }

                await currentTask.ConfigureAwait(false);
            }
        }

        internal bool OwnsCurrentConfig(APPConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            lock (_syncRoot)
                return !_disposed && ReferenceEquals(_currentConfig, config);
        }

        private void CurrentConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(APPConfig.IsMute)
                || sender is not APPConfig config
                || config.IsMute)
            {
                return;
            }

            RequestSingleInstanceEnforcement(config);
        }

        private void RequestSingleInstanceEnforcement(APPConfig config)
        {
            if (_suppressEnforcement())
                return;

            TaskCompletionSource? completion = null;
            lock (_syncRoot)
            {
                if (_disposed
                    || !ReferenceEquals(_currentConfig, config)
                    || config.IsMute)
                {
                    return;
                }

                _enforcementRequested = true;
                if (_enforcementWorkerRunning)
                    return;

                _enforcementWorkerRunning = true;
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _currentEnforcementTask = completion.Task;
            }

            _ = RunEnforcementWorkerAsync(completion);
        }

        private async Task RunEnforcementWorkerAsync(TaskCompletionSource completion)
        {
            try
            {
                while (TryTakeEnforcementRequest(out APPConfig config, out long generation))
                {
                    try
                    {
                        if (!PersistIfCurrentAndDisabled(config, generation))
                            continue;

                        int? closedInstanceCount = await _enforceSingleInstanceAsync();
                        if (!PersistIfCurrentAndDisabled(config, generation))
                            continue;

                        ClearCurrentGenerationRequest(config, generation);
                        if (closedInstanceCount.HasValue)
                            InvokeSafely(_enforcementSucceeded, closedInstanceCount.Value);
                    }
                    catch (Exception ex)
                    {
                        if (!TryRollbackCurrentGeneration(config, generation))
                            continue;

                        try
                        {
                            PersistIfCurrent(config, generation);
                        }
                        catch (Exception persistException)
                        {
                            ex = new AggregateException(ex, persistException);
                        }

                        ClearCurrentGenerationRequest(config, generation);
                        InvokeSafely(_enforcementFailed, ex);
                    }
                }
            }
            finally
            {
                completion.TrySetResult();
            }
        }

        private bool TryTakeEnforcementRequest(out APPConfig config, out long generation)
        {
            lock (_syncRoot)
            {
                if (_disposed
                    || !_enforcementRequested
                    || _currentConfig == null
                    || _currentConfig.IsMute)
                {
                    _enforcementRequested = false;
                    _enforcementWorkerRunning = false;
                    config = null!;
                    generation = 0;
                    return false;
                }

                _enforcementRequested = false;
                config = _currentConfig;
                generation = _currentGeneration;
                return true;
            }
        }

        private bool PersistIfCurrentAndDisabled(APPConfig config, long generation)
        {
            if (!IsCurrent(config, generation, requireDisabled: true))
                return false;

            _persistConfig(config);
            return IsCurrent(config, generation, requireDisabled: true);
        }

        private void PersistIfCurrent(APPConfig config, long generation)
        {
            if (IsCurrent(config, generation, requireDisabled: false))
                _persistConfig(config);
        }

        private bool TryRollbackCurrentGeneration(APPConfig config, long generation)
        {
            lock (_syncRoot)
            {
                if (_disposed
                    || !ReferenceEquals(_currentConfig, config)
                    || _currentGeneration != generation)
                {
                    return false;
                }

                if (!config.IsMute)
                    config.IsMute = true;
                return true;
            }
        }

        private bool IsCurrent(APPConfig config, long generation, bool requireDisabled)
        {
            lock (_syncRoot)
            {
                return !_disposed
                    && ReferenceEquals(_currentConfig, config)
                    && _currentGeneration == generation
                    && (!requireDisabled || !config.IsMute);
            }
        }

        private void ClearCurrentGenerationRequest(APPConfig config, long generation)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_currentConfig, config) && _currentGeneration == generation)
                    _enforcementRequested = false;
            }
        }

        private static void InvokeSafely<T>(Action<T>? callback, T value)
        {
            try
            {
                callback?.Invoke(value);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _enforcementRequested = false;
                _currentGeneration++;
                if (_currentConfig != null)
                    _currentConfig.PropertyChanged -= CurrentConfig_PropertyChanged;
                _currentConfig = null;
            }
        }
    }
}
