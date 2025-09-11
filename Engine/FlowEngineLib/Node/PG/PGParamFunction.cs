using Newtonsoft.Json;

namespace FlowEngineLib.Node.PG;

public class PGParamFunction
{
	public string Name { get; set; }

	[JsonProperty("params")]
	public dynamic Params { get; set; }
}
