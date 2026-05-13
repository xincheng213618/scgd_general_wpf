namespace MQTTMessageLib.Camera;

public class DeviceCameraGetTempResponse : DeviceCameraResultResponse<DeviceGetTempResult>
{
	public DeviceCameraGetTempResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.GetTemperature, status, totalTime)
	{
	}

	public DeviceCameraGetTempResponse(DeviceGetTempResult result, int code, string desc, long totalTime)
		: base(CameraResultType.GetTemperature, code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraGetTempResponse Success(DeviceGetTempResult result, long totalTime)
	{
		return new DeviceCameraGetTempResponse(result, 0, "ok", totalTime);
	}
}
