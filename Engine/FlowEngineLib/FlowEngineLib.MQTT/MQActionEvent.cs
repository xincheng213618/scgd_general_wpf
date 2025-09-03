using Newtonsoft.Json;

namespace FlowEngineLib.MQTT;

public class MQActionEvent
{
	public string Topic { get; set; }

	public string MsgID { get; set; }

	[JsonProperty("ServiceName")]
	public string ServiceCode { get; set; }

	public string DeviceCode { get; set; }

	public string EventName { get; set; }

	public string Token { get; set; }

	public string Message { get; set; }

	public MQActionEvent()
		: this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
	{
	}

	public MQActionEvent(string msgId, string serviceCode, string deviceCode, string topic, string eventName, string message, string token)
	{
		MsgID = msgId;
		ServiceCode = serviceCode;
		DeviceCode = deviceCode;
		Topic = topic;
		EventName = eventName;
		Token = token;
		Message = message;
	}
}
