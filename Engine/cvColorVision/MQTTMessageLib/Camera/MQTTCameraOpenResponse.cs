namespace MQTTMessageLib.Camera;

public class MQTTCameraOpenResponse : MQTTCVBaseResponse<DeviceOpenResult>
{
	public MQTTCameraOpenResponse(MQTTCVRequestHeader request, DeviceCameraOpenResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
