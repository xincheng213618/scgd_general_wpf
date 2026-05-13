namespace MQTTMessageLib.Flow;

public class DeviceFlowLoad<T> : DeviceFlowBaseRequest<DeviceFlowLoadParam<T>>
{
	public DeviceFlowRuntimeParam RuntimeParam { get; set; }

	public DeviceFlowLoad(string deviceCode, string serialNumber, DeviceFlowLoadParam<T> param)
		: base(deviceCode, serialNumber, FlowRequestType.Load, param)
	{
	}
}
