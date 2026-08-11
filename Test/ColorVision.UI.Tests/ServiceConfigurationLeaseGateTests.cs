using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using WindowsServicePlugin.CVWinSMS;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class ServiceConfigurationLeaseGateTests
{
    [Fact]
    public void CaptureMaterializesEveryInstallPrimitiveFromOneConfigService()
    {
        ServiceConfigurationGeneration source = CreateGeneration(7, "B");
        var service = new CompleteConfigService(source);

        ServiceConfigurationGeneration captured = ServiceConfigurationGeneration.Capture(service, 7);
        ServiceConfigurationSnapshot operation = captured.CreateOperationSnapshot();

        source.ServiceManager.BaseLocation = "C";
        source.MySql.Database = "C";
        source.Mqtt.Host = "C";
        source.RCSetting.Config.RCName = "C";
        source.CVWinSMS.CVWinSMSPath = "C";
        source.MySqlLocal.ServiceName = "C";
        source.MySqlSetting.MySqlConfig.Host = "C";
        source.MQTTSetting.MQTTConfig.Host = "C";

        Assert.Equal(8, service.RequestedTypes.Count);
        Assert.Equal(8, service.RequestedTypes.Distinct().Count());
        Assert.Equal("B", operation.ServiceManager.BaseLocation);
        Assert.Equal("B", operation.MySql.Database);
        Assert.Equal("B", operation.Mqtt.Host);
        Assert.Equal("B", operation.RCSetting.Config.RCName);
        Assert.Equal("B", operation.CVWinSMS.CVWinSMSPath);
        Assert.Equal("B", operation.MySqlLocal.ServiceName);
        Assert.Equal("B", operation.MySqlSetting.MySqlConfig.Host);
        Assert.Equal("B", operation.MQTTSetting.MQTTConfig.Host);
        Assert.Equal("B", operation.MySqlManager.Helper.ServiceName);
    }

    [Fact]
    public void SharedLeasesDeferReloadAndKeepOnlyNewestCompleteGeneration()
    {
        ServiceConfigurationGeneration generationA = CreateGeneration(0, "A");
        var gate = new ServiceConfigurationLeaseGate(generationA);

        ServiceConfigurationSnapshot mainWindow = gate.BeginOperation();
        ServiceConfigurationSnapshot installWindow = gate.BeginOperation();
        generationA.ServiceManager.BaseLocation = "A-mutated";
        generationA.MySql.Database = "A-mutated";
        generationA.Mqtt.Host = "A-mutated";
        generationA.RCSetting.Config.RCName = "A-mutated";
        generationA.CVWinSMS.CVWinSMSPath = "A-mutated";
        generationA.MySqlLocal.MysqlPath = "A-mutated";
        generationA.MySqlSetting.MySqlConfig.Host = "A-mutated";
        generationA.MQTTSetting.MQTTConfig.Host = "A-mutated";

        Assert.Equal("A", mainWindow.ServiceManager.BaseLocation);
        Assert.Equal("A", mainWindow.MySql.Database);
        Assert.Equal("A", mainWindow.Mqtt.Host);
        Assert.Equal("A", installWindow.ServiceManager.BaseLocation);
        Assert.Equal("A", installWindow.RCSetting.Config.RCName);
        Assert.Equal("A", installWindow.CVWinSMS.CVWinSMSPath);
        Assert.Equal("A", installWindow.MySqlLocal.MysqlPath);
        Assert.Equal("A", installWindow.MySqlSetting.MySqlConfig.Host);
        Assert.Equal("A", installWindow.MQTTSetting.MQTTConfig.Host);

        ServiceConfigurationGeneration generationB = CreateGeneration(1, "B");
        ServiceConfigurationGeneration generationC = CreateGeneration(2, "C");
        Assert.Null(gate.QueueOrBeginTransition(generationB));
        Assert.Null(gate.QueueOrBeginTransition(generationC));

        Assert.Null(gate.ReleaseOperation());
        ServiceConfigurationGeneration? ready = gate.ReleaseOperation();

        Assert.Same(generationC, ready);
        Assert.Null(gate.CompleteTransition(ready!, applied: true));
        ServiceConfigurationSnapshot nextOperation = gate.BeginOperation();
        Assert.Equal(2, nextOperation.Generation);
        Assert.Equal("C", nextOperation.ServiceManager.BaseLocation);
        Assert.Equal("C", nextOperation.MySql.Database);
        Assert.Equal("C", nextOperation.Mqtt.Host);
        Assert.Equal("C", nextOperation.RCSetting.Config.RCName);
        Assert.Equal("C", nextOperation.CVWinSMS.CVWinSMSPath);
        Assert.Equal("C", nextOperation.MySqlLocal.MysqlPath);
        Assert.Equal("C", nextOperation.MySqlManager.Helper.ServiceName);
        Assert.Equal("C", nextOperation.MySqlSetting.MySqlConfig.Host);
        Assert.Equal("C", nextOperation.MQTTSetting.MQTTConfig.Host);
        Assert.Null(gate.ReleaseOperation());
    }

    [Fact]
    public void FailedTransitionKeepsPreviousCompleteGeneration()
    {
        ServiceConfigurationGeneration generationA = CreateGeneration(0, "A");
        ServiceConfigurationGeneration generationB = CreateGeneration(1, "B");
        var gate = new ServiceConfigurationLeaseGate(generationA);

        ServiceConfigurationGeneration? ready = gate.QueueOrBeginTransition(generationB);
        Assert.Same(generationB, ready);
        Assert.Null(gate.CompleteTransition(ready!, applied: false));

        ServiceConfigurationSnapshot operation = gate.BeginOperation();
        Assert.Equal(0, operation.Generation);
        Assert.Equal("A", operation.ServiceManager.BaseLocation);
        Assert.Equal("A", operation.MySql.Database);
        Assert.Equal("A", operation.Mqtt.Host);
        Assert.Null(gate.ReleaseOperation());
    }

    private static ServiceConfigurationGeneration CreateGeneration(long generation, string value)
    {
        return new ServiceConfigurationGeneration(
            generation,
            new ServiceManagerConfig { BaseLocation = value },
            new MySqlServiceConfig { Database = value },
            new MqttServiceConfig { Host = value },
            new RCSetting { Config = new RCServiceConfig { RCName = value } },
            new CVWinSMSConfig { CVWinSMSPath = value },
            new MySqlLocalConfig { ServiceName = value, MysqlPath = value, MysqldumpPath = value },
            new MySqlSetting { MySqlConfig = new MySqlConfig { Host = value } },
            new MQTTSetting { MQTTConfig = new MQTTConfig { Host = value } });
    }

    private sealed class CompleteConfigService : IConfigService
    {
        private readonly Dictionary<Type, IConfig> configs;

        public CompleteConfigService(ServiceConfigurationGeneration generation)
        {
            configs = new Dictionary<Type, IConfig>
            {
                [typeof(ServiceManagerConfig)] = generation.ServiceManager,
                [typeof(MySqlServiceConfig)] = generation.MySql,
                [typeof(MqttServiceConfig)] = generation.Mqtt,
                [typeof(RCSetting)] = generation.RCSetting,
                [typeof(CVWinSMSConfig)] = generation.CVWinSMS,
                [typeof(MySqlLocalConfig)] = generation.MySqlLocal,
                [typeof(MySqlSetting)] = generation.MySqlSetting,
                [typeof(MQTTSetting)] = generation.MQTTSetting,
            };
        }

        public List<Type> RequestedTypes { get; } = [];

        public IConfig GetRequiredService(Type type)
        {
            RequestedTypes.Add(type);
            return configs[type];
        }

        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));

        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
