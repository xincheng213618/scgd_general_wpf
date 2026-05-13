using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTNodeServiceAddRequest : MQTTNodeServiceTokenHeader
{
	public List<string> Data { get; set; }

	public MQTTNodeServiceAddRequest(string nodeName, string serviceType, string token)
		: base(nodeName, serviceType, "AddService", token)
	{
	}
}
