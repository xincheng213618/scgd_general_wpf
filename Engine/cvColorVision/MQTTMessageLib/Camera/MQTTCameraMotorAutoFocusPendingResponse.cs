namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorAutoFocusPendingResponse : MQTTCVBaseResponse<MQTTCameraMotorAutoFocusPendingResult>
{
	public MQTTCameraMotorAutoFocusPendingResponse(MQTTCVRequestHeader request, DeviceCameraMotorAutoFocusResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraMotorAutoFocusPendingResult(response))
	{
	}
}
