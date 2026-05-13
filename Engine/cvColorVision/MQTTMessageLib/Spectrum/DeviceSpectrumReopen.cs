namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumReopen : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumReopen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.Reopen)
	{
	}
}
