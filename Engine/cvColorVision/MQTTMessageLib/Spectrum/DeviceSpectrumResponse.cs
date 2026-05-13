namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumResponse : CVBaseDeviceResponseWithResult, IDevSpectrumResponse, IDeviceResponseWithResult, IDeviceResponse
{
	public DeviceSpectrumResponse(int code, string desc)
		: this(code, desc, -1L)
	{
	}

	public DeviceSpectrumResponse(int code, string desc, long totalTime)
		: base(300, code, desc, totalTime)
	{
	}

	public DeviceSpectrumResponse(CVBaseDeviceResponse status, long totalTime)
		: base(300, status, totalTime)
	{
	}

	public DeviceSpectrumResponse(CVBaseDeviceResponse status)
		: this(status, -1L)
	{
	}

	public static DeviceSpectrumResponse Success(long totalTime)
	{
		return new DeviceSpectrumResponse(0, "ok")
		{
			TotalTime = totalTime,
			MasterId = -1
		};
	}

	public static DeviceSpectrumResponse Failed(long totalTime)
	{
		return new DeviceSpectrumResponse(-1, "Failed")
		{
			TotalTime = totalTime,
			MasterId = -1
		};
	}

	public new static DeviceSpectrumResponse Failed(string desc)
	{
		return new DeviceSpectrumResponse(-1, desc)
		{
			MasterId = -1
		};
	}
}
