namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumShutterDisconnect : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumShutterDisconnect(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.ShutterDisconnect)
	{
	}
}
