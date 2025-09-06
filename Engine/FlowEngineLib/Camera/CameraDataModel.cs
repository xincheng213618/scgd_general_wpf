using Newtonsoft.Json;

namespace FlowEngineLib.Camera;

public class CameraDataModel
{
	public string ServiceName = "Camera";

	public string EventName = "GetData";

	public string MsgID;

	public string SerialNumber;

	public string ServiceID;

	[JsonProperty("params")]
	public CameraDataParam Params;
}
