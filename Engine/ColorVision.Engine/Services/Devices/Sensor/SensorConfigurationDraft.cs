using ColorVision.Engine.Services.Devices.Sensor.Local;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ColorVision.Engine.Services.Devices.Sensor;

// Both configuration stores keep their existing contracts; the window edits detached copies.
public sealed class SensorConfigurationDraft
{
    private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
    private readonly JObject initialServiceSettings;
    private readonly JObject initialDisplaySettings;
    public ConfigSensor Config { get; }
    public DisplaySensorConfig DisplayConfig { get; }
    internal bool ServiceSettingsChanged { get; private set; }
    internal bool DisplaySettingsChanged { get; private set; }

    public SensorConfigurationDraft(ConfigSensor config, DisplaySensorConfig displayConfig)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(displayConfig);
        Config = JsonConvert.DeserializeObject<ConfigSensor>(JsonConvert.SerializeObject(config), CopySettings)!;
        DisplayConfig = JsonConvert.DeserializeObject<DisplaySensorConfig>(JsonConvert.SerializeObject(displayConfig), CopySettings)!;
        initialServiceSettings = JObject.FromObject(Config);
        initialDisplaySettings = JObject.FromObject(DisplayConfig);
    }

    internal bool TryApply(ConfigSensor target, DisplaySensorConfig displayTarget, out string error)
    {
        ServiceSettingsChanged = false;
        DisplaySettingsChanged = false;
        var serviceSettings = JObject.FromObject(Config);
        bool serviceEdited = !JToken.DeepEquals(initialServiceSettings, serviceSettings);
        bool displayEdited = !JToken.DeepEquals(initialDisplaySettings, JObject.FromObject(DisplayConfig));
        var localSettings = displayEdited ? DisplayConfig : displayTarget;
        if ((serviceEdited || displayEdited) && localSettings.UseLocalSensor)
        {
            var connection = (serviceEdited ? serviceSettings : JObject.FromObject(target)).ToObject<LocalSensorConnectionConfig>()!;
            connection.ConnectTimeout = localSettings.ConnectTimeout;
            connection.DataBits = localSettings.DataBits;
            connection.Parity = localSettings.Parity;
            connection.StopBits = localSettings.StopBits;
            try { connection.ValidateLocalConnection(); }
            catch (ArgumentException ex) { error = ex.Message; return false; }
        }

        if (serviceEdited) JsonConvert.PopulateObject(serviceSettings.ToString(Formatting.None), target, CopySettings);
        if (displayEdited)
        {
            displayTarget.ConnectTimeout = DisplayConfig.ConnectTimeout;
            displayTarget.DataBits = DisplayConfig.DataBits;
            displayTarget.Parity = DisplayConfig.Parity;
            displayTarget.StopBits = DisplayConfig.StopBits;
            displayTarget.DtrEnable = DisplayConfig.DtrEnable;
            displayTarget.RtsEnable = DisplayConfig.RtsEnable;
            // Apply routing last so observers see the complete set of preferences.
            displayTarget.UseLocalSensor = DisplayConfig.UseLocalSensor;
        }
        ServiceSettingsChanged = serviceEdited;
        DisplaySettingsChanged = displayEdited;
        error = string.Empty;
        return true;
    }
}
