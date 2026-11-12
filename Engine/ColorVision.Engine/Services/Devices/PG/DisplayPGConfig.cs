using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.PG;

public sealed class DisplayPGConfig : IDisplayConfigBase
{
    [Category("DeviceConnection"), DisplayName("使用本地 PG")]
    [Description("下次打开时由本机直接连接 PG；已打开的连接需先关闭。关闭后继续使用 PG 服务。")]
    public bool UseLocalPG { get => useLocal; set { if (useLocal == value) return; useLocal = value; OnPropertyChanged(); } }
    private bool useLocal;

    [Category("DeviceConnection"), DisplayName("连接超时(ms)")]
    public int ConnectTimeout { get; set; } = 3000;
    [Category("DeviceConnection"), DisplayName("指令超时(ms)")]
    public int CommandTimeout { get; set; } = 10000;
}
