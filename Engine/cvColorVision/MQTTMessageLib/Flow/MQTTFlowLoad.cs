namespace MQTTMessageLib.Flow;

public class MQTTFlowLoad<T> : MQTTCVBaseRequest<DeviceFlowLoadParam<T>>
{
	public MQTTFlowLoad(string serviceName, string deviceName, string serialNumber, string token, DeviceFlowLoadParam<T> data)
		: base(serviceName, deviceName, "Flow_Load", serialNumber, token, data)
	{
	}
}
