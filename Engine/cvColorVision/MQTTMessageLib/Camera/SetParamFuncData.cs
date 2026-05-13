using Newtonsoft.Json;

namespace MQTTMessageLib.Camera;

public struct SetParamFuncData
{
	public string Name { get; set; }

	[JsonProperty("params")]
	public dynamic Params { get; set; }
}
