using System;

namespace MQTTMessageLib.Flow;

public interface IDevFlowRequest : IDeviceRequest
{
	FlowRequestType DeviceRequestType { get; }

	DateTime StartTime { get; set; }
}
