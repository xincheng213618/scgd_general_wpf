using System;

namespace MQTTMessageLib.Flow;

public class DeviceFlowStop(string deviceCode, string serialNumber) : DeviceCVBaseNoParamRequest<FlowRequestType>(deviceCode, serialNumber, FlowRequestType.Stop), IDevFlowRequest, IDeviceRequest
{
	public DateTime StartTime { get; set; } = DateTime.Now;
}
