using FlowEngineLib;
using MQTTnet;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class MQTTClientPoolTests
{
    [Fact]
    public void ReleaseKeepsActiveEndpointUntilConfigurationChanges()
    {
        string server = $"mqtt-pool-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var client = DispatchProxy.Create<IMqttClient, ConnectedMqttClientProxy>();

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        MQTTClientPool.Register(client, server, port, userName);
        MQTTClientPool.Release(client);

        Assert.Same(client, MQTTClientPool.Acquire(server, port, userName));

        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        MQTTClientPool.Release(client);

        Assert.Null(MQTTClientPool.Acquire(server, port, userName));
    }

    public class ConnectedMqttClientProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_IsConnected")
            {
                return true;
            }
            if (targetMethod?.ReturnType == typeof(Task))
            {
                return Task.CompletedTask;
            }
            if (targetMethod?.ReturnType.IsGenericType == true &&
                targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = targetMethod.ReturnType.GetGenericArguments()[0];
                object? result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, new[] { result });
            }
            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
