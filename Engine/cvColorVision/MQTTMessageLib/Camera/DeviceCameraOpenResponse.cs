namespace MQTTMessageLib.Camera;

public class DeviceCameraOpenResponse : DeviceCameraResultResponse<DeviceOpenResult>
{
	public DeviceCameraOpenResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.Open, status, totalTime)
	{
	}

	public DeviceCameraOpenResponse(DeviceOpenResult result, int code, string desc, long totalTime)
		: base(CameraResultType.Open, code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraOpenResponse Unauthorized(string sn, long totalTime)
	{
		return new DeviceCameraOpenResponse(new DeviceOpenResult
		{
			SN = sn
		}, -401, "License Unauthorized", totalTime);
	}
}
