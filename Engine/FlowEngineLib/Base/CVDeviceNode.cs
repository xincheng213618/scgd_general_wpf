using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public interface IFlowDeviceNode
{
    string DeviceCode { get; set; }
}

/// <summary>Device identity for existing service and control nodes.</summary>
public class CVDeviceNode : CVCommonNode, IFlowDeviceNode
{
    protected string m_deviceCode;

    [STNodeProperty("设备代码", "设备代码", false, true)]
    public string DeviceCode
    {
        get => m_deviceCode;
        set { m_deviceCode = value; OnPropertyChanged(); }
    }

    public CVDeviceNode(string title, string nodeType, string nodeName, string deviceCode)
        : base(title, nodeType, nodeName)
    {
        m_deviceCode = deviceCode;
    }
}
