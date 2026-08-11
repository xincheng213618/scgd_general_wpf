using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class ServiceConfigurationLeaseGateTests
{
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

        Assert.Equal("A", mainWindow.ServiceManager.BaseLocation);
        Assert.Equal("A", mainWindow.MySql.Database);
        Assert.Equal("A", mainWindow.Mqtt.Host);
        Assert.Equal("A", installWindow.ServiceManager.BaseLocation);

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
            new MqttServiceConfig { Host = value });
    }
}
