using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Templates;

namespace ColorVision.UI.Tests;

public class FlowInitializerTests
{
    [Fact]
    public void RunsBeforeMqttConnectionAndFlowTemplateMaterialization()
    {
        var flowInitializer = new FlowInitializer();

        Assert.True(flowInitializer.Order < new MqttInitializer().Order);
        Assert.True(flowInitializer.Order < new TemplateInitializer().Order);
    }
}
