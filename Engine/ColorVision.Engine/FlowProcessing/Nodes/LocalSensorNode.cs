using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

[STNode("Flow_CustomNodes/Flow_LocalDevices", "本地通用传感器", CategoryOrder = 9900, NodeOrder = 100)]
public sealed class LocalSensorNode : LocalDeviceFlowNodeBase
{
    private string templateName = string.Empty;
    [STNodeProperty("参数模板", "未连接时先尝试连接，再执行模板；指令超时、延时和回包判定均取模板配置", true)]
    [PropertyEditorType(typeof(SensorTemplatePropertiesEditor))]
    public string TemplateName { get => templateName; set { templateName = value; OnPropertyChanged(); } }

    public LocalSensorNode() : base("本地通用传感器", "Sensor", "ExecCmd") => SelectFirstAvailableDevice<DeviceSensor>();
    protected override string GetCompactSummaryValue() => CompactValueOrDash(TemplateName);
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var device = ServiceManager.Current?.DeviceServices.OfType<DeviceSensor>().FirstOrDefault(device => device.Code == DeviceCode)
            ?? throw new InvalidOperationException($"找不到传感器设备：{DeviceCode}");
        using var execution = new LocalSensorFlowExecution(device, "ExecCmd", new { TemplateParam = new { ID = -1, Name = TemplateName } }, autoConnect: true);
        execution.Bind(action);
        execution.Execute();
        return new LocalNodeExecutionResult { Data = execution.Complete(action) };
    }
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new { DeviceCode, EventName = "ExecCmd", TemplateName });
}
