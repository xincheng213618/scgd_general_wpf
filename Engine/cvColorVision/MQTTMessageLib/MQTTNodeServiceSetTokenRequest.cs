namespace MQTTMessageLib;

public class MQTTNodeServiceSetTokenRequest : MQTTNodeServiceHeader
{
	public string NodeAppId { get; set; }

	public string NodeKey { get; set; }

	public NodeToken Data { get; set; }

	public MQTTNodeServiceSetTokenRequest(string nodeName, string serviceType, string nodeAppId, string nodeKey, NodeToken data)
		: base(nodeName, serviceType, "SetToken")
	{
		NodeAppId = nodeAppId;
		NodeKey = nodeKey;
		Data = data;
	}
}
