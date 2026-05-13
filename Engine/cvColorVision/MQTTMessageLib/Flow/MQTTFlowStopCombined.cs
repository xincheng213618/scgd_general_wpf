namespace MQTTMessageLib.Flow;

public class MQTTFlowStopCombined : MQTTCVBaseRequest<CVTemplateParam>
{
	public MQTTFlowStopCombined(string serviceName, string deviceName, string serialNumber, string token, CVTemplateParam data)
		: base(serviceName, deviceName, "Flow_CombinedStop", serialNumber, token, data)
	{
	}
}
