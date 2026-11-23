using ColorVision.ToolPlugins.ThirdPartyApps;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class SscomSensorPresetsTests
{
    [Fact]
    public void PowerOnKeepsTheSensorTemplatePayloadAndItsSingleLineFeed()
    {
        var powerOn = Assert.Single(SscomSensorPresets.BuildDefaults(), command => command.Name == "POWER_ON");
        Assert.Equal("PG,POWER,ON\n", Encoding.ASCII.GetString(powerOn.Payload));
    }

    [Fact]
    public void MultiCommandTemplateKeepsOrderAndExpandsTextEscapesLikeTheSensor()
    {
        var commands = SscomSensorPresets.Build("Drive Board", ["A\\r\\n,,Ascii,1000/0,0", "FF 00,,Hex,1000/0,0"]);
        Assert.Equal("Drive Board #1", commands[0].Name);
        Assert.Equal("A\r\n", Encoding.ASCII.GetString(commands[0].Payload));
        Assert.Equal("Drive Board #2", commands[1].Name);
        Assert.Equal(new byte[] { 255, 0 }, commands[1].Payload);
    }
}
