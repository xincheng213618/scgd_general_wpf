namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumSelfAdaptionInitDarkPendingResponse : CVBaseDeviceResponse
{
	public Init_Auto_Dark_PendingMsg Result { get; set; }

	public long TotalTime { get; set; }

	public DeviceSpectrumSelfAdaptionInitDarkPendingResponse(int code, string desc, Init_Auto_Dark_PendingMsg result, long totalTime)
		: base(code, desc)
	{
		Result = result;
		TotalTime = totalTime;
	}

	public static DeviceSpectrumSelfAdaptionInitDarkPendingResponse Pending(long totalTime, Init_Auto_Dark_PendingMsg result)
	{
		return new DeviceSpectrumSelfAdaptionInitDarkPendingResponse(102, "Pending", result, totalTime);
	}
}
