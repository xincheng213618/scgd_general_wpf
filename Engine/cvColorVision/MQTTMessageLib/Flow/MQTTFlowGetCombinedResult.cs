namespace MQTTMessageLib.Flow;

public class MQTTFlowGetCombinedResult : MQTTCVBaseRequest<CVTemplateParam>
{
	public MQTTFlowGetCombinedResult(string serviceName, string deviceName, string serialNumber, string token, CVTemplateParam data)
		: base(serviceName, deviceName, "Flow_GetCombinedResult", serialNumber, token, data)
	{
	}
}
