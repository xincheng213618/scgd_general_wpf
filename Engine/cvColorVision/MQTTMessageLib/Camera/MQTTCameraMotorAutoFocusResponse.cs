namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorAutoFocusResponse : MQTTCVBaseResponse<MQTTCameraMotorAutoFocusResult>
{
	public MQTTCameraMotorAutoFocusResponse(MQTTCVRequestHeader request, DeviceCameraMotorAutoFocusResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTCameraMotorAutoFocusResult(response))
	{
	}

	public MQTTCameraMotorAutoFocusResponse(MQTTCVRequestHeader request, DeviceCameraResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), (MQTTCameraMotorAutoFocusResult)null)
	{
	}
}
