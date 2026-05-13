using System.Collections.Generic;

namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumScanResponse : DeviceSpectrumResponse
{
	public List<string> Result { get; set; }

	public DeviceSpectrumScanResponse(int code, string desc, List<string> data)
		: base(code, desc)
	{
		Result = data;
	}

	public DeviceSpectrumScanResponse(CVBaseDeviceResponse status, List<string> data, long totalTime)
		: base(status, totalTime)
	{
		Result = data;
	}

	public static DeviceSpectrumScanResponse Success(List<string> data, long totalTime)
	{
		return new DeviceSpectrumScanResponse(CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
