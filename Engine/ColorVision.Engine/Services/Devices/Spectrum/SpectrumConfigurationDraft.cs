using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public sealed class SpectrumConfigurationDraft : ViewModelBase
    {
        private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
        public ConfigSpectrum Config { get; }

        public SpectrumConfigurationDraft(ConfigSpectrum source)
        {
            ArgumentNullException.ThrowIfNull(source);
            // Collections and nested settings must belong to the draft, including calibration groups and ND lists.
            Config = JsonConvert.DeserializeObject<ConfigSpectrum>(JsonConvert.SerializeObject(source), CopySettings)!;
            ReloadConnection();
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
            error = string.Empty;
            return true;
        }

        internal bool TryApply(ConfigSpectrum target, out string error)
        {
            if (!Prepare(out error)) return false;
            if (Config.SN != null) Config.SN = Config.SN.Trim();
            if (Config.NDConfig.IsBingNDDevice) Config.NDConfig.SzComName = string.Empty;
            else Config.NDConfig.NDBindDeviceCode = string.Empty;
            // Keep the live config identity and its subscribers; never copy event fields from the draft.
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(Config), target, CopySettings);
            return true;
        }
    }
}
