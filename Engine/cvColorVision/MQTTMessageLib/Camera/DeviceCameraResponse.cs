namespace MQTTMessageLib.Camera;

public class DeviceCameraResponse : DeviceCameraBaseResponse
{
	public DeviceCameraResponse(CameraResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
	}

	public DeviceCameraResponse(CameraResultType resultType, int code, string desc, long totalTime)
		: base(resultType, code, desc, totalTime)
	{
	}

	public static DeviceCameraResponse Failed(CameraResultType resultType, string desc)
	{
		return Failed(resultType, desc, -1L);
	}

	public static DeviceCameraResponse Pending(CameraResultType resultType)
	{
		return new DeviceCameraResponse(resultType, 102, "Pending", -1L);
	}

	public static DeviceCameraResponse Failed(CameraResultType resultType, string desc, long totalTime)
	{
		return new DeviceCameraResponse(resultType, -1, desc, totalTime);
	}

	public static DeviceCameraResponse Success(CameraResultType resultType, long totalTime)
	{
		return new DeviceCameraResponse(resultType, 0, "ok", totalTime);
	}
}
