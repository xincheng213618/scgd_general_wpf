namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumMeasureAutoStop : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumMeasureAutoStop(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.MeasureAutoStop)
	{
	}
}
