using ColorVision.Engine.Services.Devices.Sensor.Local;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Services.Devices.Sensor;

// Both configuration stores keep their existing contracts; the window edits detached copies.
public sealed class SensorConfigurationDraft
{
    private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
    public ConfigSensor Config { get; }
    public DisplaySensorConfig DisplayConfig { get; }

    public SensorConfigurationDraft(ConfigSensor config, DisplaySensorConfig displayConfig)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(displayConfig);
        Config = JsonConvert.DeserializeObject<ConfigSensor>(JsonConvert.SerializeObject(config), CopySettings)!;
        DisplayConfig = JsonConvert.DeserializeObject<DisplaySensorConfig>(JsonConvert.SerializeObject(displayConfig), CopySettings)!;
    }

    internal bool TryApply(ConfigSensor target, DisplaySensorConfig displayTarget, out string error)
    {
        if (DisplayConfig.UseLocalSensor)
        {
            var connection = JsonConvert.DeserializeObject<LocalSensorConnectionConfig>(JsonConvert.SerializeObject(Config))!;
            connection.ConnectTimeout = DisplayConfig.ConnectTimeout;
            connection.DataBits = DisplayConfig.DataBits;
            connection.Parity = DisplayConfig.Parity;
            connection.StopBits = DisplayConfig.StopBits;
            try { connection.ValidateLocalConnection(); }
            catch (ArgumentException ex) { error = ex.Message; return false; }
        }

        JsonConvert.PopulateObject(JsonConvert.SerializeObject(Config), target, CopySettings);
        displayTarget.ConnectTimeout = DisplayConfig.ConnectTimeout;
        displayTarget.DataBits = DisplayConfig.DataBits;
        displayTarget.Parity = DisplayConfig.Parity;
        displayTarget.StopBits = DisplayConfig.StopBits;
        displayTarget.DtrEnable = DisplayConfig.DtrEnable;
        displayTarget.RtsEnable = DisplayConfig.RtsEnable;
        // Apply routing last so observers see the complete set of preferences.
        displayTarget.UseLocalSensor = DisplayConfig.UseLocalSensor;
        error = string.Empty;
        return true;
    }
}
