using System;

namespace MQTTMessageLib.Flow;

public abstract class DeviceFlowBaseRequest<P> : DeviceCVBaseRequest<FlowRequestType, P>, IDevFlowRequest, IDeviceRequest
{
	public DateTime StartTime { get; set; } = DateTime.Now;

	public DeviceFlowBaseRequest(string deviceCode, string serialNumber, FlowRequestType request, P param)
		: base(deviceCode, serialNumber, request, param)
	{
	}

	public DeviceFlowBaseRequest(string deviceCode, string serialNumber, int zindex, FlowRequestType request, P param)
		: base(deviceCode, serialNumber, zindex, request, param)
	{
	}

	public DeviceFlowBaseRequest(string deviceCode, string serialNumber, int zindex, FlowRequestType request, string version, P param)
		: base(deviceCode, serialNumber, zindex, request, version, param)
	{
	}
}
