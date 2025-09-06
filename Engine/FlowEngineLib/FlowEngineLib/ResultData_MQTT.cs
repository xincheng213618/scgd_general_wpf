using FlowEngineLib.MQTT;
using Newtonsoft.Json;

namespace FlowEngineLib;

public class ResultData_MQTT
{
	public int ResultCode { get; set; }

	public EventTypeEnum EventType { get; set; }

	public string ResultMsg { get; set; } = string.Empty;

	[JsonIgnore]
	public object ResultObject1 { get; set; } = string.Empty;

	[JsonIgnore]
	public object ResultObject2 { get; set; } = string.Empty;
}
