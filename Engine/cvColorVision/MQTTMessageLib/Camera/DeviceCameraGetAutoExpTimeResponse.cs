namespace MQTTMessageLib.Camera;

public class DeviceCameraGetAutoExpTimeResponse : DeviceCameraResultResponse<CameraGetAutoExpTimeRespResult>
{
	public DeviceCameraGetAutoExpTimeResponse(CVBaseDeviceResponse status, long totalTime)
		: base(CameraResultType.GetAutoExpTime, status, totalTime)
	{
	}

	public DeviceCameraGetAutoExpTimeResponse(int code, string desc, long totalTime)
		: base(CameraResultType.GetAutoExpTime, code, desc, totalTime)
	{
	}

	public DeviceCameraGetAutoExpTimeResponse(CameraGetAutoExpTimeRespResult result, int code, string desc, long totalTime)
		: this(code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceCameraGetAutoExpTimeResponse Success(CameraGetAutoExpTimeRespResult result, long totalTime)
	{
		return new DeviceCameraGetAutoExpTimeResponse(result, 0, "ok", totalTime);
	}

	public static DeviceCameraGetAutoExpTimeResponse Failed(string desc, long totalTime)
	{
		return new DeviceCameraGetAutoExpTimeResponse(-1, desc, totalTime);
	}
}
