namespace MQTTMessageLib.Camera;

public class DeviceCameraScanCameraResponse : DeviceCameraResultResponse<DeviceGetCameraIDs>
{
	public DeviceCameraScanCameraResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.Scan, status, totalTime)
	{
	}

	public DeviceCameraScanCameraResponse(DeviceGetCameraIDs result, int code, string desc, long totalTime)
		: base(CameraResultType.Scan, code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraScanCameraResponse Success(DeviceGetCameraIDs result, long totalTime)
	{
		return new DeviceCameraScanCameraResponse(result, 0, "ok", totalTime);
	}
}
