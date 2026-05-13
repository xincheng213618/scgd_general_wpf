namespace MQTTMessageLib.Flow;

public class MQTTFlowCombinedRun<T> : MQTTCVBaseRequest<DeviceFlowCombinedRunParam<T>>
{
	public MQTTFlowCombinedRun(string serviceName, string deviceName, string serialNumber, string token, DeviceFlowCombinedRunParam<T> data)
		: base(serviceName, deviceName, "Flow_CombinedRun", serialNumber, token, data)
	{
	}
}
