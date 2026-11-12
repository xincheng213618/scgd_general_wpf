using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public sealed class SpectrumConfigurationDraft : ViewModelBase
    {
        private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
        private JObject appliedServiceSettings;
        private (bool UseLocal, bool FluxMode) appliedDisplaySettings;
        public ConfigSpectrum Config { get; }
        public DisplaySpectrumConfig DisplayConfig { get; }
        internal bool ServiceSettingsChanged { get; private set; }
        internal bool DisplaySettingsChanged { get; private set; }

        public SpectrumConfigurationDraft(ConfigSpectrum source, DisplaySpectrumConfig? displaySource = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            // Collections and nested settings must belong to the draft, including calibration groups and ND lists.
            Config = JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(source), CopySettings)!;
            DisplayConfig = new DisplaySpectrumConfig();
            if (displaySource != null)
            {
                DisplayConfig.UseLocalSpectrum = displaySource.UseLocalSpectrum;
                DisplayConfig.IsLuminousFluxMode = displaySource.IsLuminousFluxMode;
            }
            ReloadConnection();
            appliedServiceSettings = GetServiceSettings();
            appliedDisplaySettings = (DisplayConfig.UseLocalSpectrum, DisplayConfig.IsLuminousFluxMode);
        }

        public bool IsSerialConnection { get => isSerialConnection; set { isSerialConnection = value; OnPropertyChanged(); } }
        private bool isSerialConnection;

        [DisplayName("SpectrumComPort"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SerialPortName { get => serialPortName; set { serialPortName = value; OnPropertyChanged(); } }
        private string serialPortName = string.Empty;

        internal void ReloadConnection()
        {
            IsSerialConnection = !string.IsNullOrWhiteSpace(Config.ComPort) && Config.ComPort.Trim() != "0";
            SerialPortName = IsSerialConnection ? Config.ComPortView : string.Empty;
        }

        internal bool TryGetPort(out int port)
        {
            port = 0;
            if (!IsSerialConnection) return true;
            string value = SerialPortName?.Trim() ?? string.Empty;
            if (value.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) value = value[3..];
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 256;
        }

        internal bool Prepare(out string error)
        {
            if (!TryGetPort(out int port))
            {
                error = Properties.Resources.SpectrumEditorInvalidPort;
                return false;
            }
            if (IsSerialConnection && Config.BaudRate <= 0)
            {
                error = Properties.Resources.SpectrumEditorInvalidBaud;
                return false;
            }
            Config.ComPort = port.ToString(CultureInfo.InvariantCulture);
            try { cvColorVision.SpectrumSmoothing.Validate(Config.GetDataConfig.SmoothingMethod, Config.GetDataConfig.FilterBW); }
            catch (InvalidOperationException ex) { error = ex.Message; return false; }
            if (Config.IsShutterEnable && Config.ShutterCfg.UseFilterWheel)
            {
                if (string.IsNullOrWhiteSpace(Config.ShutterCfg.FilterWheelDeviceCode))
                {
                    error = "请选择用于校零的滤色轮设备。";
                    return false;
                }
                if (Config.ShutterCfg.DarkPort < 0 || Config.ShutterCfg.MeasurementPort < 0 || Config.ShutterCfg.DarkPort == Config.ShutterCfg.MeasurementPort)
                {
                    error = "滤色轮测量位和校零位必须是不同的非负孔位索引。";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        internal bool TryApply(ConfigSpectrum target, out string error, DisplaySpectrumConfig? displayTarget = null)
        {
            ServiceSettingsChanged = false;
            DisplaySettingsChanged = false;
            error = string.Empty;
            if (displayTarget == null || !JToken.DeepEquals(appliedServiceSettings, GetServiceSettings()))
            {
                if (!Prepare(out error)) return false;
                if (Config.SN != null) Config.SN = Config.SN.Trim();
                if (Config.NDConfig.IsBingNDDevice) Config.NDConfig.SzComName = string.Empty;
                else Config.NDConfig.NDBindDeviceCode = string.Empty;
                var serviceSettings = JObject.FromObject(Config);
                ServiceSettingsChanged = !JToken.DeepEquals(serviceSettings, JObject.FromObject(target));
                if (ServiceSettingsChanged)
                {
                    // Keep the live config identity and its subscribers; never copy event fields from the draft.
                    JsonConvert.PopulateObject(serviceSettings.ToString(Formatting.None), target, CopySettings);
                }
                appliedServiceSettings = GetServiceSettings();
            }
            if (displayTarget != null)
            {
                if (appliedDisplaySettings.FluxMode != DisplayConfig.IsLuminousFluxMode
                    && displayTarget.IsLuminousFluxMode != DisplayConfig.IsLuminousFluxMode)
                {
                    displayTarget.IsLuminousFluxMode = DisplayConfig.IsLuminousFluxMode;
                    DisplaySettingsChanged = true;
                }
                if (appliedDisplaySettings.UseLocal != DisplayConfig.UseLocalSpectrum
                    && displayTarget.UseLocalSpectrum != DisplayConfig.UseLocalSpectrum)
                {
                    displayTarget.UseLocalSpectrum = DisplayConfig.UseLocalSpectrum;
                    DisplaySettingsChanged = true;
                }
                appliedDisplaySettings = (DisplayConfig.UseLocalSpectrum, DisplayConfig.IsLuminousFluxMode);
            }
            return true;
        }

        private JObject GetServiceSettings()
        {
            var settings = JObject.FromObject(Config);
            // The editor normalizes these inputs when loading and validating a draft.
            settings[nameof(ConfigSpectrum.SN)] = Config.SN?.Trim() ?? string.Empty;
            settings.Remove(nameof(ConfigSpectrum.ComPortView));
            settings[nameof(ConfigSpectrum.ComPort)] = TryGetPort(out int port)
                ? new JValue(port)
                : new JValue(SerialPortName?.Trim() ?? string.Empty);
            return settings;
        }
    }
}
