namespace ColorVision.Engine.Services.Devices.CfwPort;

public sealed class DisplayCfwPortConfig : IDisplayConfigBase
{
    public int? PortNum { get => portNum; set { if (portNum == value) return; portNum = value; OnPropertyChanged(); } }
    private int? portNum;
}
