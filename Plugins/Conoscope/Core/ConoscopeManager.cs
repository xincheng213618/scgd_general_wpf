using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Json;
using log4net;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Windows;

namespace Conoscope.Core
{
    public sealed class ConoscopeRuntimeSnapshot : IDisposable
    {
        private ConoscopeManager? owner;
        private readonly ConoscopeManager.RuntimeState state;
        private readonly ConoscopeConfig config;

        internal ConoscopeRuntimeSnapshot(ConoscopeManager owner, ConoscopeManager.RuntimeState state, ConoscopeConfig config)
        {
            this.owner = owner;
            this.state = state;
            this.config = config;
        }

        public long Generation => state.Generation;
        public ConoscopeConfig Config => config;
        public ConoscopeGlobalReferenceStore GlobalReferences => state.GlobalReferences;

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Release(state);
        }
    }

    public class ConoscopeManager : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeManager));
        private static readonly JsonSerializerSettings SnapshotSerializerSettings = CreateSnapshotSerializerSettings();
        private static ConoscopeManager _instance;
        private static readonly object _locker = new();
        public static ConoscopeManager GetInstance() { lock (_locker) { return _instance ??= new ConoscopeManager(); } }

        private readonly object stateLocker = new();
        private readonly RuntimeConfigOwner<ConoscopeConfig> configOwner;
        private readonly Func<ConoscopeConfig, ConoscopeGlobalReferenceStore> globalReferenceFactory;
        private RuntimeState state;
        private bool isDisposed;

        public ConoscopeConfig Config => Volatile.Read(ref state).LiveConfig;
        public ConoscopeGlobalReferenceStore GlobalReferences => Volatile.Read(ref state).GlobalReferences;
        public RelayCommand EditConoscopeConfigCommand { get; }

        public event EventHandler<RuntimeConfigChangedEventArgs<ConoscopeConfig>>? ConfigurationChanged;

        public ConoscopeManager()
            : this(
                () => ConfigService.Instance.GetRequiredService<ConoscopeConfig>(),
                ConfigService.Instance as IConfigReloadNotifier,
                config => new ConoscopeGlobalReferenceStore(config))
        {
        }

        internal ConoscopeManager(
            Func<ConoscopeConfig> configFactory,
            IConfigReloadNotifier? reloadNotifier,
            Func<ConoscopeConfig, ConoscopeGlobalReferenceStore> globalReferenceFactory)
        {
            this.globalReferenceFactory = globalReferenceFactory ?? throw new ArgumentNullException(nameof(globalReferenceFactory));
            configOwner = new RuntimeConfigOwner<ConoscopeConfig>(
                configFactory,
                reloadNotifier,
                ex => log.Error("重新加载 Conoscope 配置失败", ex),
                CloneConfig);
            state = new RuntimeState(configOwner.Current, this.globalReferenceFactory(configOwner.Current), configOwner.Generation);
            configOwner.ConfigurationChanged += ConfigOwner_ConfigurationChanged;
            EditConoscopeConfigCommand = new RelayCommand(a => EditConoscopeConfig());
        }

        public ConoscopeRuntimeSnapshot CaptureRuntimeSnapshot()
        {
            lock (stateLocker)
            {
                ObjectDisposedException.ThrowIf(isDisposed, this);
                state.LeaseCount++;
                return new ConoscopeRuntimeSnapshot(this, state, configOwner.CreateSnapshot(state.LiveConfig));
            }
        }

        public void EditConoscopeConfig()
        {
            ConoscopeConfig config = Config;
            new ConoscopeConfigWindow(config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConoscopeModuleService.RefreshAllConoscopeConfiguration();
        }

        private void ConfigOwner_ConfigurationChanged(object? sender, RuntimeConfigChangedEventArgs<ConoscopeConfig> e)
        {
            ConoscopeGlobalReferenceStore nextReferences;
            try
            {
                nextReferences = globalReferenceFactory(e.Current);
            }
            catch (Exception ex)
            {
                log.Error("切换 Conoscope 全局参考矩阵失败，保留旧运行态", ex);
                return;
            }

            RuntimeState previous;
            bool disposePrevious;
            lock (stateLocker)
            {
                if (isDisposed)
                {
                    nextReferences.Dispose();
                    return;
                }

                if (e.Generation <= state.Generation)
                {
                    nextReferences.Dispose();
                    return;
                }

                previous = state;
                state = new RuntimeState(e.Current, nextReferences, e.Generation);
                previous.IsRetired = true;
                disposePrevious = previous.LeaseCount == 0;
            }

            if (disposePrevious)
                previous.GlobalReferences.Dispose();

            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(GlobalReferences));
            NotifyConfigurationChanged(e);
            ConoscopeModuleService.RefreshAllConoscopeConfiguration();
            ConoscopeModuleService.RefreshAllReferenceState();
        }

        private void NotifyConfigurationChanged(RuntimeConfigChangedEventArgs<ConoscopeConfig> args)
        {
            Delegate[] subscribers = ConfigurationChanged?.GetInvocationList() ?? [];
            foreach (Delegate subscriber in subscribers)
            {
                try
                {
                    ((EventHandler<RuntimeConfigChangedEventArgs<ConoscopeConfig>>)subscriber)(this, args);
                }
                catch (Exception ex)
                {
                    log.Error("通知 Conoscope 配置切换失败", ex);
                }
            }
        }

        internal void Release(RuntimeState releasedState)
        {
            bool dispose;
            lock (stateLocker)
            {
                releasedState.LeaseCount--;
                dispose = releasedState.IsRetired && releasedState.LeaseCount == 0;
            }

            if (dispose)
                releasedState.GlobalReferences.Dispose();
        }

        public void Dispose()
        {
            RuntimeState current;
            bool disposeCurrent;
            lock (stateLocker)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                current = state;
                current.IsRetired = true;
                disposeCurrent = current.LeaseCount == 0;
            }

            configOwner.ConfigurationChanged -= ConfigOwner_ConfigurationChanged;
            configOwner.Dispose();
            if (disposeCurrent)
                current.GlobalReferences.Dispose();
            GC.SuppressFinalize(this);
        }

        private static JsonSerializerSettings CreateSnapshotSerializerSettings()
        {
            var settings = new JsonSerializerSettings { ContractResolver = new WpfContractResolver() };
            settings.Converters.Add(new BrushJsonConverter());
            return settings;
        }

        private static ConoscopeConfig CloneConfig(ConoscopeConfig config)
        {
            string json = JsonConvert.SerializeObject(config, SnapshotSerializerSettings);
            return JsonConvert.DeserializeObject<ConoscopeConfig>(json, SnapshotSerializerSettings)
                ?? throw new InvalidOperationException("Could not create a detached ConoscopeConfig snapshot.");
        }

        internal sealed class RuntimeState
        {
            public RuntimeState(ConoscopeConfig liveConfig, ConoscopeGlobalReferenceStore globalReferences, long generation)
            {
                LiveConfig = liveConfig;
                GlobalReferences = globalReferences;
                Generation = generation;
            }

            public ConoscopeConfig LiveConfig { get; }
            public ConoscopeGlobalReferenceStore GlobalReferences { get; }
            public long Generation { get; }
            public int LeaseCount { get; set; }
            public bool IsRetired { get; set; }
        }
    }
}
