namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumShutterClose : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumShutterClose(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.ShutterClose)
	{
	}
}
