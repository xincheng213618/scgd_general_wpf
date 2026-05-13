namespace MQTTMessageLib.Camera;

public class MQTTCameraGetAutoExpTimeResponse : MQTTCVBaseResponse<CameraGetAutoExpTimeRespResult>
{
	public MQTTCameraGetAutoExpTimeResponse(MQTTCVRequestHeader request, DeviceCameraGetAutoExpTimeResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
