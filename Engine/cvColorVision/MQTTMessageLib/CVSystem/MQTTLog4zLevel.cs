namespace MQTTMessageLib.CVSystem;

public class MQTTLog4zLevel : MQTTCVBaseRequest<Log4zLevelParam>
{
	public MQTTLog4zLevel(string serviceName, string deviceName, Log4zLevelParam data)
		: base(serviceName, deviceName, "Log4zLevel", data)
	{
	}
}
