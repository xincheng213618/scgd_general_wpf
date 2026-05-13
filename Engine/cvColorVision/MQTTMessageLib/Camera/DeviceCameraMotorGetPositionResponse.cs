namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorGetPositionResponse : DeviceCameraResultResponse<MotorPositionResult>
{
	public DeviceCameraMotorGetPositionResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.Motor_GetPosition, status, totalTime)
	{
	}

	public DeviceCameraMotorGetPositionResponse(int code, string desc, long totalTime)
		: base(CameraResultType.Motor_GetPosition, code, desc, totalTime)
	{
	}

	public DeviceCameraMotorGetPositionResponse(MotorPositionResult result, int code, string desc, long totalTime)
		: this(code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraMotorGetPositionResponse Success(MotorPositionResult result, long totalTime)
	{
		return new DeviceCameraMotorGetPositionResponse(result, 0, "ok", totalTime);
	}
}
