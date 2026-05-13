namespace MQTTMessageLib.FileServer;

public class DeviceFileServerResultResponse<T> : DeviceFileServerResponse
{
	public FileServerResultType ResultType { get; set; }

	public T Result { get; set; }

	public DeviceFileServerResultResponse(int code, string desc)
		: base(code, desc)
	{
	}

	public DeviceFileServerResultResponse(FileServerResultType resultType, int code, string desc, long totalTime)
		: base(code, desc)
	{
		ResultType = resultType;
		base.TotalTime = totalTime;
	}

	public DeviceFileServerResultResponse(FileServerResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: this(resultType, status.Code, status.Desc, totalTime)
	{
	}
}
