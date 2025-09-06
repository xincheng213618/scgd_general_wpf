using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FlowEngineLib;

[JsonConverter(typeof(StringEnumConverter))]
public enum ActionStatusEnum
{
	[EnumMember(Value = "None")]
	None,
	[EnumMember(Value = "Pending")]
	Pending,
	[EnumMember(Value = "Failed")]
	Failed,
	[EnumMember(Value = "Finish")]
	Finish
}
