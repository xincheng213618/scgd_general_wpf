namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumShutterOpen : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumShutterOpen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.ShutterOpen)
	{
	}
}
