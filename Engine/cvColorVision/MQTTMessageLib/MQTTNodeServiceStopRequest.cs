using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTNodeServiceStopRequest : MQTTNodeServiceTokenHeader
{
	public List<string> Data { get; set; }

	public MQTTNodeServiceStopRequest(string nodeName, string serviceType, string token)
		: base(nodeName, serviceType, "StopService", token)
	{
	}
}
