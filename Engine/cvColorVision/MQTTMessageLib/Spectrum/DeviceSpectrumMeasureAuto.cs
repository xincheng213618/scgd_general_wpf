namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumMeasureAuto : DeviceCVBaseRequest<SPRequestType, SpectrumMeasureParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumMeasureAuto(string deviceName, string serialNumber, int zindex, SpectrumMeasureParam param)
		: base(deviceName, serialNumber, zindex, SPRequestType.MeasureAuto, param)
	{
	}
}
