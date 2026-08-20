using ColorVision.Engine.Services.Devices.LightingController;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public class LightingControllerSerializationTests
{
    [Fact]
    public void ConfigSerialization_PreservesChannelObjectsAndIgnoresCustomCommands()
    {
        ConfigLightingController config = new();
        config.CustomCmd["PowerOn"] = "S{0}{1:D4}#";

        JObject json = JObject.Parse(JsonConvert.SerializeObject(config));

        JObject channelA = Assert.IsType<JObject>(json[nameof(ConfigLightingController.CHA)]);
        JObject channelB = Assert.IsType<JObject>(json[nameof(ConfigLightingController.CHB)]);
        Assert.Equal("A", channelA[nameof(PMChannelConfig.Code)]?.Value<string>());
        Assert.Equal("B", channelB[nameof(PMChannelConfig.Code)]?.Value<string>());
        Assert.Null(json[nameof(ConfigLightingController.CustomCmd)]);

        ConfigLightingController roundTrip = JsonConvert.DeserializeObject<ConfigLightingController>(json.ToString())!;
        Assert.Equal("A", roundTrip.CHA.Code);
        Assert.Equal(255, roundTrip.CHA.OnValue);
        Assert.Equal("B", roundTrip.CHB.Code);
        Assert.Equal(0, roundTrip.CHB.OffValue);
    }
}
