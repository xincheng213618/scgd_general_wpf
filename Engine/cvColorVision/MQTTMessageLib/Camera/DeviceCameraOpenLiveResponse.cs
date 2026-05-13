namespace MQTTMessageLib.Camera;

public class DeviceCameraOpenLiveResponse : DeviceCameraResultResponse<DeviceOpenLiveResult>
{
	public DeviceCameraOpenLiveResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.OpenLive, status, totalTime)
	{
	}

	public DeviceCameraOpenLiveResponse(DeviceOpenLiveResult result, int code, string desc, long totalTime)
		: base(CameraResultType.OpenLive, code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraOpenLiveResponse Success(DeviceOpenLiveResult result, long totalTime)
	{
		return new DeviceCameraOpenLiveResponse(result, 0, "ok", totalTime);
	}
}
