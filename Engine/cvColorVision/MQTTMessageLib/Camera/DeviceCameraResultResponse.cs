namespace MQTTMessageLib.Camera;

public class DeviceCameraResultResponse<T> : DeviceCameraBaseResponse
{
	public T Result { get; set; }

	public DeviceCameraResultResponse(CameraResultType resultType, T result, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
		Result = result;
	}

	public DeviceCameraResultResponse(CameraResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
	}

	public DeviceCameraResultResponse(CameraResultType resultType, int code, string desc, long totalTime)
		: base(resultType, code, desc, totalTime)
	{
	}

	public DeviceCameraResultResponse(CameraResultType resultType, T result, int code, string desc, long totalTime)
		: this(resultType, code, desc, totalTime)
	{
		Result = result;
	}
}
