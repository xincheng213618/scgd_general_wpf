namespace MQTTMessageLib.Camera;

public class DeviceCameraBaseResponse : CVBaseDeviceResponse, IDevCameraResponse, IDeviceResponse
{
	public CameraResultType ResultType { get; set; }

	public long TotalTime { get; set; }

	public int MasterId { get; set; }

	public DeviceCameraBaseResponse(CameraResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: base(status)
	{
		ResultType = resultType;
		TotalTime = totalTime;
	}

	public DeviceCameraBaseResponse(CameraResultType resultType, int code, string desc, long totalTime)
		: base(code, desc)
	{
		ResultType = resultType;
		TotalTime = totalTime;
	}
}
