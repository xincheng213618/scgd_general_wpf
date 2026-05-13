using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.RC;

public class MQTTRCServicesAdminQueryResponse : MQTTNodeServiceResponseHeader
{
	public Dictionary<string, Dictionary<CVServiceType, List<MQTTNodeService>>> Data { get; set; }

	public MQTTRCServicesAdminQueryResponse()
	{
	}

	public MQTTRCServicesAdminQueryResponse(MQTTNodeServiceHeader request, Dictionary<string, Dictionary<CVServiceType, List<MQTTNodeService>>> services)
		: base(request.MsgId, request.NodeName, request.EventName, 0, "ok")
	{
		Data = services;
	}
}
