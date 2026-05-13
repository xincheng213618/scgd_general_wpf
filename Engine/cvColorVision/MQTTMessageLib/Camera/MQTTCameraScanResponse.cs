namespace MQTTMessageLib.Camera;

public class MQTTCameraScanResponse : MQTTCVBaseResponse<DeviceGetCameraIDs>
{
	public MQTTCameraScanResponse(MQTTCVRequestHeader request, DeviceCameraScanCameraResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
