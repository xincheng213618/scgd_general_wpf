using Newtonsoft.Json;
using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using System;
using System.Threading;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin.ServiceManager
{
    internal sealed class ServiceConfigurationSnapshot
    {
        public ServiceConfigurationSnapshot(
            long generation,
            ServiceManagerConfig serviceManager,
            MySqlServiceConfig mySql,
            MqttServiceConfig mqtt,
            RCSetting rcSetting,
            CVWinSMSConfig cvWinSms,
            MySqlLocalConfig mySqlLocal,
            MySqlSetting mySqlSetting,
            MQTTSetting mqttSetting)
        {
            Generation = generation;
            ServiceManager = serviceManager;
            MySql = mySql;
            Mqtt = mqtt;
            RCSetting = rcSetting;
            CVWinSMS = cvWinSms;
            MySqlLocal = mySqlLocal;
            MySqlSetting = mySqlSetting;
            MQTTSetting = mqttSetting;
            MySqlManager = new MySqlServiceManager(ServiceManager, MySql, MySqlLocal, MySqlSetting);
            MqttManager = new MqttServiceManager(Mqtt, MQTTSetting);
        }

        public long Generation { get; }
        public ServiceManagerConfig ServiceManager { get; }
        public MySqlServiceConfig MySql { get; }
        public MqttServiceConfig Mqtt { get; }
        public RCSetting RCSetting { get; }
        public CVWinSMSConfig CVWinSMS { get; }
        public MySqlLocalConfig MySqlLocal { get; }
        public MySqlSetting MySqlSetting { get; }
        public MQTTSetting MQTTSetting { get; }
        public MySqlServiceManager MySqlManager { get; }
        public MqttServiceManager MqttManager { get; }
    }

    internal sealed class ServiceConfigurationGeneration
    {
        public ServiceConfigurationGeneration(
            long generation,
            ServiceManagerConfig serviceManager,
            MySqlServiceConfig mySql,
            MqttServiceConfig mqtt,
            RCSetting rcSetting,
            CVWinSMSConfig cvWinSms,
            MySqlLocalConfig mySqlLocal,
            MySqlSetting mySqlSetting,
            MQTTSetting mqttSetting)
        {
            Generation = generation;
            ServiceManager = serviceManager;
            MySql = mySql;
            Mqtt = mqtt;
            RCSetting = rcSetting;
            CVWinSMS = cvWinSms;
            MySqlLocal = mySqlLocal;
            MySqlSetting = mySqlSetting;
            MQTTSetting = mqttSetting;
        }

        public long Generation { get; }
        public ServiceManagerConfig ServiceManager { get; }
        public MySqlServiceConfig MySql { get; }
        public MqttServiceConfig Mqtt { get; }
        public RCSetting RCSetting { get; }
        public CVWinSMSConfig CVWinSMS { get; }
        public MySqlLocalConfig MySqlLocal { get; }
        public MySqlSetting MySqlSetting { get; }
        public MQTTSetting MQTTSetting { get; }

        public static ServiceConfigurationGeneration Capture(IConfigService currentConfig, long generation)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);
            return new ServiceConfigurationGeneration(
                generation,
                currentConfig.GetRequiredService<ServiceManagerConfig>(),
                currentConfig.GetRequiredService<MySqlServiceConfig>(),
                currentConfig.GetRequiredService<MqttServiceConfig>(),
                currentConfig.GetRequiredService<RCSetting>(),
                currentConfig.GetRequiredService<CVWinSMSConfig>(),
                currentConfig.GetRequiredService<MySqlLocalConfig>(),
                currentConfig.GetRequiredService<MySqlSetting>(),
                currentConfig.GetRequiredService<MQTTSetting>());
        }

        public ServiceConfigurationSnapshot CreateOperationSnapshot() => new(
            Generation,
            Clone(ServiceManager),
            Clone(MySql),
            Clone(Mqtt),
            Clone(RCSetting),
            Clone(CVWinSMS),
            Clone(MySqlLocal),
            Clone(MySqlSetting),
            Clone(MQTTSetting));

        private static T Clone<T>(T config) where T : class
        {
            string json = JsonConvert.SerializeObject(config);
            return JsonConvert.DeserializeObject<T>(json)
                ?? throw new InvalidOperationException($"Could not create a {typeof(T).Name} operation snapshot.");
        }
    }

    internal sealed class ServiceConfigurationLeaseGate
    {
        private readonly object locker = new();
        private ServiceConfigurationGeneration active;
        private ServiceConfigurationGeneration? pending;
        private int operationCount;
        private bool transitionInProgress;
        private long transitionGeneration = -1;

        public ServiceConfigurationLeaseGate(ServiceConfigurationGeneration initial)
        {
            active = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public ServiceConfigurationGeneration Active
        {
            get
            {
                lock (locker)
                    return active;
            }
        }

        public ServiceConfigurationSnapshot BeginOperation()
        {
            lock (locker)
            {
                while (transitionInProgress)
                    Monitor.Wait(locker);

                operationCount++;
                return active.CreateOperationSnapshot();
            }
        }

        public ServiceConfigurationGeneration? QueueOrBeginTransition(ServiceConfigurationGeneration candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            lock (locker)
            {
                long newestGeneration = Math.Max(active.Generation, Math.Max(pending?.Generation ?? -1, transitionGeneration));
                if (candidate.Generation <= newestGeneration)
                    return null;

                if (operationCount > 0 || transitionInProgress)
                {
                    pending = candidate;
                    return null;
                }

                transitionInProgress = true;
                transitionGeneration = candidate.Generation;
                return candidate;
            }
        }

        public ServiceConfigurationGeneration? CompleteTransition(ServiceConfigurationGeneration candidate, bool applied)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            lock (locker)
            {
                if (!transitionInProgress || transitionGeneration != candidate.Generation)
                    throw new InvalidOperationException("The service configuration transition is not active.");

                if (applied)
                    active = candidate;

                transitionInProgress = false;
                transitionGeneration = -1;

                ServiceConfigurationGeneration? next = TakePendingTransitionIfReady();
                Monitor.PulseAll(locker);
                return next;
            }
        }

        public ServiceConfigurationGeneration? ReleaseOperation()
        {
            lock (locker)
            {
                if (operationCount <= 0)
                    throw new InvalidOperationException("Service operation lease count is already zero.");

                operationCount--;
                return TakePendingTransitionIfReady();
            }
        }

        private ServiceConfigurationGeneration? TakePendingTransitionIfReady()
        {
            if (operationCount != 0 || transitionInProgress || pending == null)
                return null;

            ServiceConfigurationGeneration next = pending;
            pending = null;
            transitionInProgress = true;
            transitionGeneration = next.Generation;
            return next;
        }
    }

    internal sealed class ServiceManagerOperationLease : IDisposable
    {
        private ServiceManagerViewModel? owner;

        internal ServiceManagerOperationLease(ServiceManagerViewModel owner, ServiceConfigurationSnapshot snapshot)
        {
            this.owner = owner;
            Snapshot = snapshot;
        }

        public ServiceConfigurationSnapshot Snapshot { get; }

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.ReleaseOperation();
        }
    }
}
