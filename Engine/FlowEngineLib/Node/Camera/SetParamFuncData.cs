using Newtonsoft.Json;

namespace FlowEngineLib.Node.Camera;

public struct SetParamFuncData
{
	public string Name { get; set; }

	[JsonProperty("params")]
	public dynamic Params { get; set; }
}
