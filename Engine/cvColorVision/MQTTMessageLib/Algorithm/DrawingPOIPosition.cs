using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm;

[JsonConverter(typeof(StringEnumConverter))]
public enum DrawingPOIPosition
{
	[Description("线上")]
	LineOn,
	[Description("内切")]
	Internal,
	[Description("外切")]
	External
}
