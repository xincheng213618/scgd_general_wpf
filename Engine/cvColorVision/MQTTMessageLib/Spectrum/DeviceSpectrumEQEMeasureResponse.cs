using CVCommCore.CVSpectrum;

namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumEQEMeasureResponse : DeviceSpectrumResponse
{
	public string ResultDataFilename { get; set; }

	public COLOR_PARA_EQE Data { get; set; }

	public DeviceSpectrumEQEMeasureResponse(int code, string desc, COLOR_PARA_EQE data)
		: base(code, desc)
	{
		Data = data;
	}

	public DeviceSpectrumEQEMeasureResponse(CVBaseDeviceResponse status, COLOR_PARA_EQE data)
		: base(status)
	{
		Data = data;
	}

	public static DeviceSpectrumEQEMeasureResponse Success(COLOR_PARA_EQE data)
	{
		return new DeviceSpectrumEQEMeasureResponse(CVBaseDeviceResponse.Success(), data);
	}

	public new static DeviceSpectrumEQEMeasureResponse Failed()
	{
		return new DeviceSpectrumEQEMeasureResponse(CVBaseDeviceResponse.Failed(), default(COLOR_PARA_EQE));
	}
}
