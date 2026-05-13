using System;

namespace MQTTMessageLib.Flow;

public class DeviceFlowStopCombined(string deviceCode, string serialNumber) : DeviceCVBaseNoParamRequest<FlowRequestType>(deviceCode, serialNumber, FlowRequestType.StopCombined), IDevFlowRequest, IDeviceRequest
{
	public DateTime StartTime { get; set; } = DateTime.Now;
}
