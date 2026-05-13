using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm;

[JsonConverter(typeof(StringEnumConverter))]
public enum LayoutMarginType
{
	None = -1,
	Relative,
	Absolute
}
