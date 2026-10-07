using ColorVision.Engine.Services;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

/// <summary>Resource selection shared by local camera and calibration nodes.</summary>
public abstract class LocalDeviceFlowNodeBase : LocalFlowNodeBase, IFlowDeviceNode
{
    private string deviceCode = string.Empty;

    [Display(Order = -200)]
    [PropertyEditorType(typeof(FlowDeviceNameEditor))]
    [STNodeProperty("设备代码", "设备代码", false, false)]
    public string DeviceCode
    {
        get => deviceCode;
        set { deviceCode = value; OnPropertyChanged(); }
    }

    protected LocalDeviceFlowNodeBase(string title, string nodeType, string operatorName, params string[] inputNames)
        : base(title, nodeType, operatorName, inputNames) { }

    protected void SelectFirstAvailableDevice<TDevice>() where TDevice : DeviceService
        => DeviceCode = GetFirstAvailableDeviceCode<TDevice>();

    protected static string GetFirstAvailableDeviceCode<TDevice>() where TDevice : DeviceService
        => ServiceManager.Current?.DeviceServices.OfType<TDevice>().FirstOrDefault()?.Code ?? string.Empty;
}
