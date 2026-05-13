namespace MQTTMessageLib;

public class MQTTNodeServiceLoadAllServicesRequest : MQTTNodeServiceTokenHeader
{
	public MQTTNodeServiceLoadAllServicesRequest(string nodeName, string serviceType, string token)
		: base(nodeName, serviceType, "LoadAllServices", token)
	{
	}
}
