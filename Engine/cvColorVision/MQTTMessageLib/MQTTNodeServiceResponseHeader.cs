namespace MQTTMessageLib;

public class MQTTNodeServiceResponseHeader
{
	public string Version { get; set; }

	public string MsgId { get; set; }

	public string NodeName { get; set; }

	public string EventName { get; set; }

	public int Code { get; set; }

	public string Message { get; set; }

	public MQTTNodeServiceResponseHeader()
	{
		Version = "1.0";
	}

	public MQTTNodeServiceResponseHeader(string msgId, string nodeName, string eventName, int code, string message)
		: this()
	{
		MsgId = msgId;
		NodeName = nodeName;
		EventName = eventName;
		Code = code;
		Message = message;
	}
}
