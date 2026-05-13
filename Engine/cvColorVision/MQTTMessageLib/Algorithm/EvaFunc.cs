using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm;

[JsonConverter(typeof(StringEnumConverter))]
public enum EvaFunc
{
	Variance,
	Tenengrad,
	Laplace,
	CalResol
}
