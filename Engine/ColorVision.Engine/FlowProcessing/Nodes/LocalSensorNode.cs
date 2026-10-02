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

public enum LocalSensorOperation { ExecuteTemplate, Open, Close, Reopen }

[STNode("Flow_CustomNodes", "本地通用传感器", CategoryOrder = 9900)]
public sealed class LocalSensorNode : LocalDeviceFlowNodeBase
{
    private LocalSensorOperation operation;
    private string templateName = string.Empty;
    [STNodeProperty("操作", "执行模板、打开、关闭或重新打开；执行模板不会自动连接", true)]
    public LocalSensorOperation Operation { get => operation; set { operation = value; OnPropertyChanged(); } }
    [STNodeProperty("参数模板", "指令超时、延时和回包判定均取模板配置", true)]
    [PropertyEditorType(typeof(SensorTemplatePropertiesEditor))]
    public string TemplateName { get => templateName; set { templateName = value; OnPropertyChanged(); } }

    public LocalSensorNode() : base("本地通用传感器", "Sensor", "ExecCmd") => SelectFirstAvailableDevice<DeviceSensor>();
    private string EventName => Operation switch
    {
        LocalSensorOperation.Open => "Open", LocalSensorOperation.Close => "Close",
        LocalSensorOperation.Reopen => "Reopen", LocalSensorOperation.ExecuteTemplate => "ExecCmd",
        _ => throw new ArgumentException("无效的传感器操作。")
    };
    protected override string GetCompactSummaryValue() => Operation == LocalSensorOperation.ExecuteTemplate ? CompactValueOrDash(TemplateName) : Operation.ToString();
    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var device = ServiceManager.Current?.DeviceServices.OfType<DeviceSensor>().FirstOrDefault(device => device.Code == DeviceCode)
            ?? throw new InvalidOperationException($"找不到传感器设备：{DeviceCode}");
        using var execution = new LocalSensorFlowExecution(device, EventName, new { TemplateParam = new { ID = -1, Name = TemplateName } });
        execution.Bind(action);
        execution.Execute();
        return new LocalNodeExecutionResult { Data = execution.Complete(action) };
    }
    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new { DeviceCode, Operation, TemplateName });
}
