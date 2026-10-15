using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public sealed class SmuConfigurationDraft : ViewModelBase
    {
        private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
        internal const string DefaultIpAddress = "192.168.100.100";
        public ConfigSMU Config { get; }
        public DisplaySMUConfig DisplayConfig { get; }

        public SmuConfigurationDraft(ConfigSMU source, DisplaySMUConfig displayConfig)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(displayConfig);
            Config = JsonConvert.DeserializeObject<ConfigSMU>(JsonConvert.SerializeObject(source), CopySettings)!;
            DisplayConfig = JsonConvert.DeserializeObject<DisplaySMUConfig>(JsonConvert.SerializeObject(displayConfig), CopySettings)!;
            // Remember each input while switching modes, but persist only the selected address as DevName.
            serialPortName = source.IsNet ? string.Empty : source.DevName ?? string.Empty;
            ipAddress = source.IsNet && !string.IsNullOrWhiteSpace(source.DevName) ? source.DevName : DefaultIpAddress;
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
            string address = (IsNet ? IpAddress : SerialPortName)?.Trim() ?? string.Empty;
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

            Config.DevName = address;
            // Preserve the live instance and subscribers, as in the Spectrum editor.
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(Config), target, CopySettings);
            // Preserve live readings and per-channel measurement values while applying the editable preferences.
            displayTarget.UseLocalSmu = DisplayConfig.UseLocalSmu;
            displayTarget.LocalBaudRate = DisplayConfig.LocalBaudRate;
            displayTarget.IsUseLimitSigned = DisplayConfig.IsUseLimitSigned;
            displayTarget.IsSourceV = DisplayConfig.IsSourceV;
            displayTarget.Channel = DisplayConfig.Channel;
            error = string.Empty;
            return true;
        }
    }
}
