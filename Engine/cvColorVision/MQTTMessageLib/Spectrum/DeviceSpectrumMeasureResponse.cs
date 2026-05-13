using CVCommCore.CVSpectrum;

namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumMeasureResponse : DeviceSpectrumResponse
{
	public string ResultDataFilename { get; set; }

	public SpectrumColorData Data { get; set; }

	public DeviceSpectrumMeasureResponse(int code, string desc, SpectrumColorData data)
		: base(code, desc)
	{
		Data = data;
	}

	public DeviceSpectrumMeasureResponse(CVBaseDeviceResponse status, SpectrumColorData data)
		: base(status)
	{
		Data = data;
	}

	public static DeviceSpectrumMeasureResponse Success(SpectrumColorData data)
	{
		return new DeviceSpectrumMeasureResponse(CVBaseDeviceResponse.Success(), data);
	}

	public new static DeviceSpectrumMeasureResponse Failed()
	{
		return new DeviceSpectrumMeasureResponse(CVBaseDeviceResponse.Failed(), default(SpectrumColorData));
	}
}
