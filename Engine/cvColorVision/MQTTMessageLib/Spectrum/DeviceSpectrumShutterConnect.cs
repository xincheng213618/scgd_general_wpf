namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumShutterConnect : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumShutterConnect(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.ShutterConnect)
	{
	}
}
