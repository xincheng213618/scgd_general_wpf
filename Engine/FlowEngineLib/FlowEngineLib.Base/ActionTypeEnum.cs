using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FlowEngineLib.Base;

[JsonConverter(typeof(StringEnumConverter))]
public enum ActionTypeEnum
{
	[EnumMember(Value = "Start")]
	Start,
	[EnumMember(Value = "Pause")]
	Pause,
	[EnumMember(Value = "Stop")]
	Stop,
	[EnumMember(Value = "Fail")]
	Fail
}
