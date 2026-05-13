namespace MQTTMessageLib.RC;

public class MQTTRCServicesQueryRequest : MQTTNodeServiceTokenHeader
{
	public MQTTRCServicesQueryRequest(string nodeName, string serviceType, string token)
		: base(nodeName, serviceType, "QueryServices", token)
	{
	}
}
