namespace MQTTMessageLib.Flow;

public class MQTTFlowRun<T> : MQTTCVBaseRequest<DeviceFlowRunParam<T>>
{
	public MQTTFlowRun(string serviceName, string deviceName, string serialNumber, string token, DeviceFlowRunParam<T> data)
		: base(serviceName, deviceName, "Flow_Run", serialNumber, token, data)
	{
	}
}
