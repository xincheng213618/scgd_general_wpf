namespace MQTTMessageLib.Camera;

public class MQTTCameraResponse : MQTTCVBaseResponse<MQTTCameraResult>
{
	public MQTTCameraResponse(MQTTCVRequestHeader request, IDevCameraResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraResult(response.TotalTime))
	{
	}
}
