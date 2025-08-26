using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FlowEngineLib.Base;

[JsonConverter(typeof(StringEnumConverter))]
public enum StatusTypeEnum
{
	[EnumMember(Value = "Runing")]
	Runing,
	[EnumMember(Value = "Paused")]
	Paused,
	[EnumMember(Value = "Failed")]
	Failed,
	[EnumMember(Value = "Canceled")]
	Canceled,
	[EnumMember(Value = "OverTime")]
	OverTime,
	[EnumMember(Value = "Completed")]
	Completed
}
