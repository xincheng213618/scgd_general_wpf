using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.CfwPort;

[DisplayName("滤色轮配置")]
public sealed class CfwPortConfigurationDraft : ConfigCfwPort
{
    private JObject initialServiceSettings = null!;
    private bool initialUseLocalCfwPort;

    [JsonIgnore]
    [DisplayName("使用本地滤色轮"), Description("当前电脑直连串口。关闭当前连接后切换生效。本地模式不使用 SN 授权或服务连接。")]
    public bool UseLocalCfwPort { get => useLocalCfwPort; set { useLocalCfwPort = value; OnPropertyChanged(); } }
    private bool useLocalCfwPort;

    internal static CfwPortConfigurationDraft From(ConfigCfwPort config, DisplayCfwPortConfig display)
    {
        var draft = JsonConvert.DeserializeObject<CfwPortConfigurationDraft>(JsonConvert.SerializeObject(config))!;
        draft.initialServiceSettings = JObject.FromObject(draft);
        draft.UseLocalCfwPort = draft.initialUseLocalCfwPort = display.UseLocalCfwPort;
        return draft;
    }

    internal (bool ServiceChanged, bool DisplayChanged) Apply(ConfigCfwPort config, DisplayCfwPortConfig display)
    {
        var serviceSettings = JObject.FromObject(this);
        bool serviceChanged = !JToken.DeepEquals(initialServiceSettings, serviceSettings);
        bool displayChanged = UseLocalCfwPort != initialUseLocalCfwPort && display.UseLocalCfwPort != UseLocalCfwPort;
        if (serviceChanged) JsonConvert.PopulateObject(serviceSettings.ToString(Formatting.None), config);
        if (displayChanged) display.UseLocalCfwPort = UseLocalCfwPort;
        return (serviceChanged, displayChanged);
    }
}
