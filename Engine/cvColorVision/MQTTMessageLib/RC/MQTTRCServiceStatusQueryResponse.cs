using System.Collections.Generic;

namespace MQTTMessageLib.RC;

public class MQTTRCServiceStatusQueryResponse : MQTTNodeServiceResponseHeader
{
	public List<MQTTNodeServiceStatus> Data { get; set; }

	public MQTTRCServiceStatusQueryResponse()
	{
	}

	public MQTTRCServiceStatusQueryResponse(MQTTNodeServiceHeader request, List<MQTTNodeServiceStatus> data)
		: base(request.MsgId, request.NodeName, request.EventName, 0, "ok")
	{
		Data = data;
	}
}
