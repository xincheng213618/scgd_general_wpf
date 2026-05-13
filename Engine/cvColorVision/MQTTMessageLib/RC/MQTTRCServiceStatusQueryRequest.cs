namespace MQTTMessageLib.RC;

public class MQTTRCServiceStatusQueryRequest : MQTTNodeServiceTokenHeader
{
	public MQTTRCServiceStatusQueryRequest(string nodeName, string serviceType, string token)
		: base(nodeName, serviceType, "QueryServiceStatus", token)
	{
	}
}
