namespace MQTTMessageLib.Flow;

public class DeviceFlowRun<T> : DeviceFlowBaseRequest<DeviceFlowRunParam<T>>
{
	public DeviceFlowRuntimeParam RuntimeParam { get; set; }

	public DeviceFlowRun(string deviceCode, string serialNumber, DeviceFlowRunParam<T> param)
		: base(deviceCode, serialNumber, FlowRequestType.Run, param)
	{
	}
}
