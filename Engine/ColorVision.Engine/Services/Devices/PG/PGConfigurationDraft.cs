using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net;
using ColorVision.Engine.Services.Devices.PG.Local;

namespace ColorVision.Engine.Services.Devices.PG;

/// <summary>Separate transport drafts; only the selected endpoint is stored in Addr / Port.</summary>
public sealed class PGConfigurationDraft : ViewModelBase
{
    private static readonly JsonSerializerSettings CopySettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
    private JObject appliedServiceSettings;
    private (bool UseLocal, int ConnectTimeout, int CommandTimeout) appliedDisplaySettings;
    public ConfigPG Config { get; }
    public DisplayPGConfig DisplayConfig { get; }
    internal bool ServiceSettingsChanged { get; private set; }
    internal bool DisplaySettingsChanged { get; private set; }

    public PGConfigurationDraft(ConfigPG source, DisplayPGConfig? display = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        Config = JsonConvert.DeserializeObject<ConfigPG>(JsonConvert.SerializeObject(source), CopySettings)!;
        DisplayConfig = JsonConvert.DeserializeObject<DisplayPGConfig>(JsonConvert.SerializeObject(display ?? new DisplayPGConfig()), CopySettings)!;
        if (IsI2C) { i2CDeviceIndex = source.Addr ?? string.Empty; i2CDeviceAddress = source.Port; }
        else if (source.IsNet) { networkAddress = source.Addr ?? string.Empty; networkPort = source.Port; }
        else { serialPortName = source.Addr ?? string.Empty; serialBaudRate = source.Port; }
        appliedServiceSettings = GetServiceSettings(source.Addr?.Trim() ?? string.Empty, source.Port);
        appliedDisplaySettings = (DisplayConfig.UseLocalPG, DisplayConfig.ConnectTimeout, DisplayConfig.CommandTimeout);
        Config.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(ConfigPG.IsNet) or nameof(ConfigPG.Category))) return;
            OnPropertyChanged(nameof(IsI2C));
            OnPropertyChanged(nameof(IsGecs));
            OnPropertyChanged(nameof(IsStandardConnection));
            OnPropertyChanged(nameof(IsNetworkConnection));
            OnPropertyChanged(nameof(IsSerialConnection));
            OnPropertyChanged(nameof(ShowRegisterAddress));
            OnPropertyChanged(nameof(RegisterAddressLabel));
        };
    }

    public bool IsI2C => string.Equals(Config.Category, "CH431.I2C", StringComparison.Ordinal);
    public bool IsGecs => string.Equals(Config.Category, "GECS.V2.4", StringComparison.Ordinal);
    public bool IsStandardConnection => !IsI2C;
    public bool IsNetworkConnection => !IsI2C && Config.IsNet;
    public bool IsSerialConnection => !IsI2C && !Config.IsNet;
    public bool ShowRegisterAddress => IsI2C || string.Equals(Config.Category, "GECS.V2.4", StringComparison.Ordinal);
    public string RegisterAddressLabel => string.Equals(Config.Category, "GECS.V2.4", StringComparison.Ordinal) ? Properties.Resources.Channel : Properties.Resources.RegisterAddress;

    public string NetworkAddress { get => networkAddress; set => SetProperty(ref networkAddress, value); }
    private string networkAddress = string.Empty;
    public int NetworkPort { get => networkPort; set => SetProperty(ref networkPort, value); }
    private int networkPort;
    public string SerialPortName { get => serialPortName; set => SetProperty(ref serialPortName, value); }
    private string serialPortName = string.Empty;
    public int SerialBaudRate { get => serialBaudRate; set => SetProperty(ref serialBaudRate, value); }
    private int serialBaudRate;
    public string I2CDeviceIndex { get => i2CDeviceIndex; set => SetProperty(ref i2CDeviceIndex, value); }
    private string i2CDeviceIndex = "0";
    public int I2CDeviceAddress { get => i2CDeviceAddress; set => SetProperty(ref i2CDeviceAddress, value); }
    private int i2CDeviceAddress = 0x08;

    internal bool TryApply(ConfigPG target, out string error, DisplayPGConfig? displayTarget = null)
    {
        ServiceSettingsChanged = false;
        DisplaySettingsChanged = false;
        string address = (IsI2C ? I2CDeviceIndex : Config.IsNet ? NetworkAddress : SerialPortName)?.Trim() ?? string.Empty;
        int port = IsI2C ? I2CDeviceAddress : Config.IsNet ? NetworkPort : SerialBaudRate;
        var serviceSettings = GetServiceSettings(address, port);
        var displaySettings = (UseLocal: DisplayConfig.UseLocalPG, DisplayConfig.ConnectTimeout, DisplayConfig.CommandTimeout);
        bool serviceEdited = displayTarget == null || !JToken.DeepEquals(appliedServiceSettings, serviceSettings);
        bool localEdited = displayTarget != null && appliedDisplaySettings != displaySettings;
        error = serviceEdited || (localEdited && DisplayConfig.UseLocalPG) ? Validate(address, port) : string.Empty;
        if (error.Length != 0) return false;

        if (serviceEdited)
        {
            Config.Addr = address;
            Config.Port = port;
            ServiceSettingsChanged = !JToken.DeepEquals(serviceSettings, JObject.FromObject(target));
            if (ServiceSettingsChanged)
            {
                // Preserve the live configuration object and its subscribers.
                JsonConvert.PopulateObject(serviceSettings.ToString(Formatting.None), target, CopySettings);
            }
            appliedServiceSettings = serviceSettings;
        }
        if (localEdited && displayTarget != null)
        {
            if (appliedDisplaySettings.ConnectTimeout != displaySettings.ConnectTimeout && displayTarget.ConnectTimeout != displaySettings.ConnectTimeout)
            {
                displayTarget.ConnectTimeout = displaySettings.ConnectTimeout;
                DisplaySettingsChanged = true;
            }
            if (appliedDisplaySettings.CommandTimeout != displaySettings.CommandTimeout && displayTarget.CommandTimeout != displaySettings.CommandTimeout)
            {
                displayTarget.CommandTimeout = displaySettings.CommandTimeout;
                DisplaySettingsChanged = true;
            }
            if (appliedDisplaySettings.UseLocal != displaySettings.UseLocal && displayTarget.UseLocalPG != displaySettings.UseLocal)
            {
                displayTarget.UseLocalPG = displaySettings.UseLocal;
                DisplaySettingsChanged = true;
            }
            appliedDisplaySettings = displaySettings;
        }
        return true;
    }

    private JObject GetServiceSettings(string address, int port)
    {
        var settings = JObject.FromObject(Config);
        settings[nameof(ConfigPG.Addr)] = address;
        settings[nameof(ConfigPG.Port)] = port;
        return settings;
    }

    private string Validate(string address, int port)
    {
        string error = string.Empty;
        if (string.IsNullOrWhiteSpace(Config.Category)) error = "请选择或填写 PG 类型。";
        else if (IsI2C)
        {
            if (!ulong.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out _)) error = "设备索引必须是非负整数，例如 0。";
            else if (port is < 0 or > 255) error = "I²C 设备地址必须在 0～255 之间。";
            else if (!Enum.IsDefined(Config.BaudRate)) error = "请选择有效的 I²C 通信速率。";
        }
        else if (address.Length == 0) error = Config.IsNet ? "请填写 PG 的 IP 地址或主机名。" : "请填写服务电脑上的串口名，例如 COM7。";
        else if (Config.IsNet)
        {
            if (!IPAddress.TryParse(address, out _) && Uri.CheckHostName(address) != UriHostNameType.Dns) error = "请填写有效的 IP 地址或主机名，不含协议和端口。";
            else if (port is < 1 or > 65535) error = "网络端口必须在 1～65535 之间。";
        }
        else if (port <= 0) error = "串口波特率必须大于 0。";
        if (error.Length == 0 && DisplayConfig.UseLocalPG)
        {
            try { LocalPGProtocol.Validate(Config, DisplayConfig); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { error = ex.Message; }
        }
        return error;
    }
}
