using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public sealed class SmuConfigurationDraft : ViewModelBase
    {
        private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
        private JObject appliedServiceSettings;
        private string appliedAddress;
        private (bool UseLocalSmu, SMUSerialBaudRate LocalBaudRate, bool IsUseLimitSigned, bool IsSourceV, SMUChannelType Channel) appliedDisplaySettings;
        internal const string DefaultIpAddress = "192.168.100.100";
        public ConfigSMU Config { get; }
        public DisplaySMUConfig DisplayConfig { get; }
        internal bool ServiceSettingsChanged { get; private set; }
        internal bool DisplaySettingsChanged { get; private set; }

        public SmuConfigurationDraft(ConfigSMU source, DisplaySMUConfig displayConfig)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(displayConfig);
            Config = JsonConvert.DeserializeObject<ConfigSMU>(JsonConvert.SerializeObject(source), CopySettings)!;
            DisplayConfig = JsonConvert.DeserializeObject<DisplaySMUConfig>(JsonConvert.SerializeObject(displayConfig), CopySettings)!;
            // Remember each input while switching modes, but persist only the selected address as DevName.
            serialPortName = source.IsNet ? string.Empty : source.DevName ?? string.Empty;
            ipAddress = source.IsNet && !string.IsNullOrWhiteSpace(source.DevName) ? source.DevName : DefaultIpAddress;
            appliedServiceSettings = JObject.FromObject(Config);
            appliedAddress = SelectedAddress;
            appliedDisplaySettings = GetDisplaySettings(DisplayConfig);
        }

        public bool IsNet
        {
            get => Config.IsNet;
            set
            {
                if (Config.IsNet == value) return;
                Config.IsNet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSerialConnection));
            }
        }

        public bool IsSerialConnection { get => !IsNet; set => IsNet = !value; }

        [DisplayName("串口"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SerialPortName { get => serialPortName; set => SetProperty(ref serialPortName, value); }
        private string serialPortName;

        [DisplayName("IP 地址"), Description("填写仪器的 IPv4 地址，例如 192.168.100.100，不含协议或端口。")]
        [PropertyEditorType(typeof(IPAddressPropertiesEditor))]
        public string IpAddress { get => ipAddress; set => SetProperty(ref ipAddress, value); }
        private string ipAddress;

        internal bool TryApply(ConfigSMU target, DisplaySMUConfig displayTarget, out string error)
        {
            ServiceSettingsChanged = false;
            DisplaySettingsChanged = false;
            error = string.Empty;
            var displaySettings = GetDisplaySettings(DisplayConfig);
            bool displayEdited = displaySettings != appliedDisplaySettings;
            var serviceSettings = JObject.FromObject(Config);
            bool serviceEdited = appliedAddress != SelectedAddress || !JToken.DeepEquals(appliedServiceSettings, serviceSettings);
            bool enablingLocal = displaySettings.UseLocalSmu && !appliedDisplaySettings.UseLocalSmu;
            if (serviceEdited || !displayEdited || enablingLocal)
            {
                if (!TryPrepare(out string address, out error)) return false;
                // An ordinary device confirmation still normalizes its legacy address.
                if (serviceEdited || (!displayEdited && address != Config.DevName))
                {
                    Config.DevName = address;
                    serviceSettings = JObject.FromObject(Config);
                    ServiceSettingsChanged = !JToken.DeepEquals(serviceSettings, JObject.FromObject(target));
                    if (ServiceSettingsChanged)
                        JsonConvert.PopulateObject(serviceSettings.ToString(Formatting.None), target, CopySettings);
                    appliedServiceSettings = serviceSettings;
                    appliedAddress = SelectedAddress;
                }
            }

            // Only apply fields changed in this draft. Keep live readings and per-channel measurement values.
            var previousDisplaySettings = GetDisplaySettings(displayTarget);
            if (displaySettings.LocalBaudRate != appliedDisplaySettings.LocalBaudRate) displayTarget.LocalBaudRate = displaySettings.LocalBaudRate;
            if (displaySettings.IsUseLimitSigned != appliedDisplaySettings.IsUseLimitSigned) displayTarget.IsUseLimitSigned = displaySettings.IsUseLimitSigned;
            if (displaySettings.IsSourceV != appliedDisplaySettings.IsSourceV) displayTarget.IsSourceV = displaySettings.IsSourceV;
            if (displaySettings.Channel != appliedDisplaySettings.Channel) displayTarget.Channel = displaySettings.Channel;
            if (displaySettings.UseLocalSmu != appliedDisplaySettings.UseLocalSmu) displayTarget.UseLocalSmu = displaySettings.UseLocalSmu;
            DisplaySettingsChanged = previousDisplaySettings != GetDisplaySettings(displayTarget);
            appliedDisplaySettings = displaySettings;
            return true;
        }

        private string SelectedAddress => (IsNet ? IpAddress : SerialPortName)?.Trim() ?? string.Empty;

        private static (bool UseLocalSmu, SMUSerialBaudRate LocalBaudRate, bool IsUseLimitSigned, bool IsSourceV, SMUChannelType Channel) GetDisplaySettings(DisplaySMUConfig config)
            => (config.UseLocalSmu, config.LocalBaudRate, config.IsUseLimitSigned, config.IsSourceV, config.Channel);

        private bool TryPrepare(out string address, out string error)
        {
            address = SelectedAddress;
            if (DisplayConfig.UseLocalSmu)
            {
                try { ConfigSMU.ValidateNplc(Config.DevType, Config.Nplc); }
                catch (ArgumentException ex) { error = ex.Message; return false; }
            }
            if (IsNet)
            {
                string[] parts = address.Split('.');
                if (parts.Length != 4 || Array.Exists(parts, part => !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                {
                    error = "请输入有效的源表 IP 地址，例如 192.168.100.100。";
                    return false;
                }
            }
            else if (Config.DevType is Pss_Type.Keithley_2400 or Pss_Type.Keithley_2600 or Pss_Type.Precise_S100)
            {
                if (!address.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(address[3..], NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 256)
                {
                    error = "请选择或输入有效的串口，例如 COM3；网口设备请切换到网口模式。";
                    return false;
                }
                address = $"COM{port}";
            }
            else if (address.Length == 0)
            {
                error = "请填写源表连接地址。";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
