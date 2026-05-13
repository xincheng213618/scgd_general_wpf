namespace MQTTMessageLib.Camera;

public class MQTTCameraOpenLiveResponse : MQTTCVBaseResponse<DeviceOpenLiveResult>
{
	public MQTTCameraOpenLiveResponse(MQTTCVRequestHeader request, DeviceCameraOpenLiveResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
