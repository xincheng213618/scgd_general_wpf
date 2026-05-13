using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.RC;

public class MQTTRCServicesQueryResponse : MQTTNodeServiceResponseHeader
{
	public Dictionary<CVServiceType, List<MQTTNodeService>> Data { get; set; }

	public MQTTRCServicesQueryResponse()
	{
	}

	public MQTTRCServicesQueryResponse(MQTTNodeServiceHeader request, Dictionary<CVServiceType, List<MQTTNodeService>> data)
		: base(request.MsgId, request.NodeName, request.EventName, 0, "ok")
	{
		Data = data;
	}
}
