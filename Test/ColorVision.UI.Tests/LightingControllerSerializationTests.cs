using ColorVision.Engine.Services.Devices.LightingController;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace ColorVision.UI.Tests;

public class LightingControllerSerializationTests
{
    [Fact]
    public void ConfigSerialization_PreservesChannelObjectsAndIgnoresCustomCommands()
    {
        ConfigLightingController config = new();
        config.CHB.Enable = false;
        config.CustomCmd["PowerOn"] = "S{0}{1:D4}#";

        JObject json = JObject.Parse(JsonConvert.SerializeObject(config));

        JObject channelA = Assert.IsType<JObject>(json[nameof(ConfigLightingController.CHA)]);
        JObject channelB = Assert.IsType<JObject>(json[nameof(ConfigLightingController.CHB)]);
        Assert.Equal("default", json[nameof(ConfigLightingController.Category)]?.Value<string>());
        Assert.Equal("A", channelA[nameof(PMChannelConfig.Code)]?.Value<string>());
        Assert.Equal("B", channelB[nameof(PMChannelConfig.Code)]?.Value<string>());
        Assert.True(channelA[nameof(PMChannelConfig.Enable)]?.Value<bool>());
        Assert.False(channelB[nameof(PMChannelConfig.Enable)]?.Value<bool>());
        Assert.Null(json[nameof(ConfigLightingController.CustomCmd)]);
        Assert.Null(json[nameof(ConfigLightingController.EnabledChannels)]);
        Assert.False(TypeDescriptor.GetProperties(config)[nameof(ConfigLightingController.CustomCmd)]!.IsBrowsable);

        ConfigLightingController roundTrip = JsonConvert.DeserializeObject<ConfigLightingController>(json.ToString())!;
        Assert.Equal("default", roundTrip.Category);
        Assert.Equal("A", roundTrip.CHA.Code);
        Assert.True(roundTrip.CHA.Enable);
        Assert.Equal(255, roundTrip.CHA.OnValue);
        Assert.Equal("B", roundTrip.CHB.Code);
        Assert.False(roundTrip.CHB.Enable);
        Assert.Equal(0, roundTrip.CHB.OffValue);
        Assert.Equal([roundTrip.CHA], roundTrip.EnabledChannels);
    }

    [Fact]
    public void EnabledChannels_UpdatesWhenChannelEnableChanges()
    {
        ConfigLightingController config = new();
        List<string?> changedProperties = [];
        config.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        config.CHA.Enable = false;

        Assert.Equal([config.CHB], config.EnabledChannels);
        Assert.Contains(nameof(ConfigLightingController.EnabledChannels), changedProperties);
    }
}
