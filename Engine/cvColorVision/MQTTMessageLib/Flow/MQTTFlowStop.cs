namespace MQTTMessageLib.Flow;

public class MQTTFlowStop : MQTTCVRequestTokenHeader
{
	public MQTTFlowStop(string serviceName, string deviceName, string serialNumber, string token)
		: base(serviceName, deviceName, "Flow_Stop", serialNumber, token)
	{
	}
}
