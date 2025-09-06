using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FlowEngineLib.MQTT;

[JsonConverter(typeof(StringEnumConverter))]
public enum EventTypeEnum
{
	[EnumMember(Value = "None")]
	None,
	[EnumMember(Value = "MsgRecv")]
	MsgRecv,
	[EnumMember(Value = "ClientConnected")]
	ClientConnected,
	[EnumMember(Value = "ClientDisconnected")]
	ClientDisconnected,
	[EnumMember(Value = "ClientReconnected")]
	ClientReconnected,
	[EnumMember(Value = "Publish")]
	Publish,
	[EnumMember(Value = "Subscribe")]
	Subscribe,
	[EnumMember(Value = "Unsubscribe")]
	Unsubscribe
}
