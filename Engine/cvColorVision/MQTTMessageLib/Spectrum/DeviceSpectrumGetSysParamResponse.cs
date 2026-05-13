namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumGetSysParamResponse : DeviceSpectrumResponse
{
	public SpectrumSysParam Data { get; set; }

	public DeviceSpectrumGetSysParamResponse(int code, string desc, SpectrumSysParam data)
		: base(code, desc)
	{
		Data = data;
	}

	public DeviceSpectrumGetSysParamResponse(CVBaseDeviceResponse status, SpectrumSysParam data)
		: base(status)
	{
		Data = data;
	}

	public static DeviceSpectrumGetSysParamResponse Success(SpectrumSysParam data)
	{
		return new DeviceSpectrumGetSysParamResponse(CVBaseDeviceResponse.Success(), data);
	}
}
