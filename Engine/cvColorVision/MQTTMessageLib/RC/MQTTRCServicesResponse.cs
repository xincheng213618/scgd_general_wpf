namespace MQTTMessageLib.RC;

public class MQTTRCServicesResponse : MQTTNodeServiceResponseHeader
{
	public MQTTRCServicesResponse(MQTTNodeServiceHeader request, int code, string message)
		: base(request.MsgId, request.NodeName, request.EventName, code, message)
	{
	}
}
