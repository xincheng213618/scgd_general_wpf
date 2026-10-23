using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.CfwPort;

public sealed class DisplayCfwPortConfig : IDisplayConfigBase
{
    [DisplayName("使用本地滤色轮"), Description("由当前电脑通过串口直接连接。请先关闭当前连接再切换；本地设置仅保存在当前电脑。")]
    public bool UseLocalCfwPort { get => useLocalCfwPort; set => SetProperty(ref useLocalCfwPort, value); }
    private bool useLocalCfwPort;

    public int? PortNum { get => portNum; set { if (portNum == value) return; portNum = value; OnPropertyChanged(); } }
    private int? portNum;
}
