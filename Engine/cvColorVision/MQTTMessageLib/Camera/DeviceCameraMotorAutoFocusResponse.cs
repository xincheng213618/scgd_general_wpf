namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorAutoFocusResponse : DeviceCameraResultResponse<MotorPositionResult>
{
	public DeviceCameraMotorAutoFocusResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.Motor_AutoFocus, status, totalTime)
	{
	}

	public DeviceCameraMotorAutoFocusResponse(MotorPositionResult result, int code, string desc, long totalTime)
		: base(CameraResultType.Motor_AutoFocus, result, code, desc, totalTime)
	{
	}

	public static DeviceCameraMotorAutoFocusResponse Success(MotorPositionResult result, long totalTime)
	{
		return new DeviceCameraMotorAutoFocusResponse(result, 0, "ok", totalTime);
	}

	public new static DeviceCameraResponse Failed(string desc)
	{
		return DeviceCameraResponse.Failed(CameraResultType.Motor_AutoFocus, desc);
	}

	public new static DeviceCameraResponse Pending()
	{
		return DeviceCameraResponse.Pending(CameraResultType.Motor_AutoFocus);
	}

	public static DeviceCameraMotorAutoFocusResponse Pending(MotorPositionResult result)
	{
		return new DeviceCameraMotorAutoFocusResponse(result, 102, "Pending", 0L);
	}

	public static DeviceCameraMotorAutoFocusResponse Pending(int timeout)
	{
		return new DeviceCameraMotorAutoFocusResponse(new MotorPositionResult
		{
			nPosition = -1
		}, 102, "Pending", 0L);
	}
}
