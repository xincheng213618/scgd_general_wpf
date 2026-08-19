namespace MQTTMessageLib;

public class MQTTNodeServiceTokenHeader : MQTTNodeServiceHeader
{
	public string Token { get; set; }

	public MQTTNodeServiceTokenHeader(string nodeName, string serviceType, string eventName, string token)
		: base(nodeName, serviceType, eventName)
	{
		Token = token;
	}

}
