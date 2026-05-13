namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorGetPositionResponse : MQTTCVBaseResponse<MotorPositionResult>
{
	public MQTTCameraMotorGetPositionResponse(MQTTCVRequestHeader request, DeviceCameraMotorGetPositionResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
