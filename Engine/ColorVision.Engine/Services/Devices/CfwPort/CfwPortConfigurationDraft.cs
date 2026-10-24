using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.CfwPort;

[DisplayName("滤色轮配置")]
public sealed class CfwPortConfigurationDraft : ConfigCfwPort
{
    [DisplayName("使用本地滤色轮"), Description("当前电脑直连串口。关闭当前连接后切换生效。本地模式不使用 SN 授权或服务连接。")]
    public bool UseLocalCfwPort { get => useLocalCfwPort; set { useLocalCfwPort = value; OnPropertyChanged(); } }
    private bool useLocalCfwPort;

    internal static CfwPortConfigurationDraft From(ConfigCfwPort config, DisplayCfwPortConfig display)
    {
        var draft = JsonConvert.DeserializeObject<CfwPortConfigurationDraft>(JsonConvert.SerializeObject(config))!;
        draft.UseLocalCfwPort = display.UseLocalCfwPort;
        return draft;
    }

    internal void Apply(ConfigCfwPort config, DisplayCfwPortConfig display)
    {
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(this), config);
        display.UseLocalCfwPort = UseLocalCfwPort;
    }
}
