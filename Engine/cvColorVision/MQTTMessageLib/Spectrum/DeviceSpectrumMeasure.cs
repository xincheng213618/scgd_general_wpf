namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumMeasure : DeviceCVBaseRequest<SPRequestType, SpectrumMeasureParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumMeasure(string deviceName, string serialNumber, int zindex, SpectrumMeasureParam param)
		: base(deviceName, serialNumber, zindex, SPRequestType.Measure, param)
	{
	}
}
