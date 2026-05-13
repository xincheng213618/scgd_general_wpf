using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.SMU;

[JsonConverter(typeof(StringEnumConverter))]
public enum Pss_Type
{
	Keithley_2400,
	Keithley_2600,
	Precise_S100,
	Vxi11Protocol,
	VictualPss
}
