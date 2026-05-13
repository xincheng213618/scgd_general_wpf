namespace MQTTMessageLib;

public class MQTTNodeServiceRegistResponse : MQTTNodeServiceHeader
{
	public int Code { get; set; }

	public string Message { get; set; }

	public NodeToken Token { get; set; }

	public MQTTNodeServiceRegistResponse()
	{
	}

	public MQTTNodeServiceRegistResponse(string version, string nodeName, string reqMsgId, int code, string message, NodeToken token)
		: base(version, nodeName, "Regist")
	{
		base.Version = version;
		base.MsgId = reqMsgId;
		Code = code;
		Message = message;
		Token = token;
		base.EventName = "Regist";
	}

	public MQTTNodeServiceRegistResponse(string nodeName, string reqMsgId, int code, string message, NodeToken token)
		: this("1.0", nodeName, reqMsgId, code, message, token)
	{
	}

	public MQTTNodeServiceRegistResponse(MQTTNodeServiceRegist request, int code, string message, NodeToken token)
		: this(request.NodeName, request.MsgId, code, message, token)
	{
		base.EventName = request.EventName;
		base.NodeName = request.NodeName;
		base.ServiceType = request.ServiceType;
	}
}
