namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumClose : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumClose(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.Close)
	{
	}
}
