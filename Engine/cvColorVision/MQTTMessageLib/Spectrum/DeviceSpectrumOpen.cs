namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumOpen : DeviceCVBaseRequest<SPRequestType, SpectrumOpenParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumOpen(string deviceName, string serialNumber, SpectrumOpenParam param)
		: base(deviceName, serialNumber, SPRequestType.Open, param)
	{
	}
}
